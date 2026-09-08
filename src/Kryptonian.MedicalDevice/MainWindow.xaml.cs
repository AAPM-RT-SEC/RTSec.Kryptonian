using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Kryptonian.MedicalDevice.Enrollment;
using Microsoft.Win32;

namespace Kryptonian.MedicalDevice;

public partial class MainWindow : Window
{
    private readonly EstEnrollmentClient _estEnrollment = new();
    private readonly CancellationTokenSource _renewalCancellation = new();
    private readonly InstalledCertificateRenewal _renewal = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kryptonian", "renewal.json"));

    public MainWindow()
    {
        InitializeComponent();
        var renewalTask = _renewal.RunAsync(message => Dispatcher.BeginInvoke(() => SetStatus(message)), _renewalCancellation.Token);
        Closed += async (_, _) =>
        {
            _renewalCancellation.Cancel();
            await renewalTask;
            _renewalCancellation.Dispose();
        };
        foreach (var box in new[] { GatewayText, CommonNameText, ManufacturerText, ModelText, SerialText })
        {
            box.TextChanged += OnInputChanged;
        }
        OnInputChanged(this, EventArgs.Empty!);
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        var gatewayValid = Uri.TryCreate(GatewayText.Text.Trim().TrimEnd('/'), UriKind.Absolute, out _);
        EnrollButton.IsEnabled = AllFieldsFilled() && gatewayValid;
        RenewButton.IsEnabled = gatewayValid;
    }

    private bool AllFieldsFilled()
    {
        return !string.IsNullOrWhiteSpace(GatewayText.Text)
            && !string.IsNullOrWhiteSpace(CommonNameText.Text)
            && !string.IsNullOrWhiteSpace(ManufacturerText.Text)
            && !string.IsNullOrWhiteSpace(ModelText.Text)
            && !string.IsNullOrWhiteSpace(SerialText.Text)
            && ActivationCodeBox.Password.Length > 0;
    }

    private async void OnEnrollClicked(object sender, RoutedEventArgs e)
    {
        if (!AllFieldsFilled())
        {
            SetStatus("All fields are required.", error: true);
            return;
        }

        if (!Uri.TryCreate(GatewayText.Text.Trim().TrimEnd('/'), UriKind.Absolute, out var gatewayUri)
            || (gatewayUri.Scheme != Uri.UriSchemeHttp && gatewayUri.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("Gateway must be an absolute HTTPS URL, e.g. https://localhost:8443.", error: true);
            return;
        }

        if (gatewayUri.Scheme != Uri.UriSchemeHttps)
        {
            SetStatus("Device activation uses EST and requires HTTPS.", error: true);
            return;
        }

        var commonName = NormalizeCommonName(CommonNameText.Text);
        var manufacturer = ManufacturerText.Text.Trim();
        var model = ModelText.Text.Trim();
        var serial = SerialText.Text.Trim();
        var activationCode = ActivationCodeBox.Password;
        var install = InstallToggle.IsChecked == true;

        SetBusy(true);
        try
        {
            SetStatus("Generating key pair and submitting CSR...");

            SetStatus("Enrolling with activation code...");
            var result = await _estEnrollment.EnrollAsync(
                gatewayUri,
                new DeviceEnrollmentRequest(commonName, manufacturer, model, serial, activationCode));
            var certificate = result.Certificate;

            if (install)
            {
                InstallCertificate(certificate);
                await _renewal.TrackAsync(gatewayUri, certificate, _renewalCancellation.Token);
                SetStatus($"Certificate installed to CurrentUser\\My. Thumbprint: {certificate.Thumbprint}", success: true);
            }
            else
            {
                if (SaveCertificateToFile(certificate, commonName))
                {
                    SetStatus($"Certificate saved. Thumbprint: {certificate.Thumbprint}", success: true);
                }
                else
                {
                    SetStatus("Save cancelled. Certificate was issued but not persisted.", error: true);
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Enrollment failed: {ex.Message}", error: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRenewClicked(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(GatewayText.Text.Trim().TrimEnd('/'), UriKind.Absolute, out var gatewayUri)
            || gatewayUri.Scheme != Uri.UriSchemeHttps)
        {
            SetStatus("Gateway must be an absolute HTTPS URL for re-enrollment.", error: true);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select existing certificate (.pfx) to renew",
            Filter = "PKCS#12 (.pfx)|*.pfx|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) { return; }

        X509Certificate2? existing = null;
        try
        {
            existing = EstEnrollmentClient.LoadPfx(dialog.FileName, ActivationCodeBox.Password);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not load .pfx: {ex.Message} (enter password in Activation Code field if needed).", error: true);
            return;
        }

        if (existing.GetRSAPrivateKey() is null)
        {
            SetStatus("Selected certificate has no accessible private key; cannot perform mTLS re-enroll.", error: true);
            existing.Dispose();
            return;
        }

        var install = InstallToggle.IsChecked == true;
        SetBusy(true);
        try
        {
            var commonName = EstEnrollmentClient.ExtractCommonName(existing.SubjectName) ?? existing.Subject;
            SetStatus($"Generating new key pair for {commonName} and submitting re-enrollment CSR...");

            var result = await EstEnrollmentClient.ReenrollAsync(gatewayUri, existing, commonName);
            var renewed = result.Certificate;

            if (install)
            {
                InstallCertificate(renewed);
                await _renewal.TrackAsync(gatewayUri, renewed, _renewalCancellation.Token);
                SetStatus($"Renewed certificate installed to CurrentUser\\My. New thumbprint: {renewed.Thumbprint}", success: true);
            }
            else
            {
                if (SaveCertificateToFile(renewed, commonName + "-renewed"))
                {
                    SetStatus($"Renewed certificate saved. New thumbprint: {renewed.Thumbprint}", success: true);
                }
                else
                {
                    SetStatus("Save cancelled. Renewed certificate was issued but not persisted.", error: true);
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Re-enrollment failed: {ex.Message}", error: true);
        }
        finally
        {
            existing.Dispose();
            SetBusy(false);
        }
    }

    private static string NormalizeCommonName(string value)
        => EstEnrollmentClient.NormalizeCommonName(value);

    private static void InstallCertificate(X509Certificate2 certificate)
    {
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        using var persisted = X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, password), password,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(persisted);
        store.Close();
    }

    private static bool SaveCertificateToFile(X509Certificate2 certificate, string commonName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{Sanitize(commonName)}.pfx",
            DefaultExt = ".pfx",
            Filter = "PKCS#12 (.pfx)|*.pfx|PEM certificate + key (.pem)|*.pem"
        };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (dialog.FilterIndex == 2)
        {
            var sb = new StringBuilder();
            sb.AppendLine(certificate.ExportCertificatePem());
            if (certificate.GetRSAPrivateKey() is { } rsa)
            {
                sb.AppendLine(rsa.ExportPkcs8PrivateKeyPem());
            }
            File.WriteAllText(dialog.FileName, sb.ToString());
        }
        else
        {
            var pfx = certificate.Export(X509ContentType.Pkcs12);
            File.WriteAllBytes(dialog.FileName, pfx);
        }
        return true;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private void SetBusy(bool busy)
    {
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        var gatewayValid = Uri.TryCreate(GatewayText.Text.Trim().TrimEnd('/'), UriKind.Absolute, out _);
        EnrollButton.IsEnabled = !busy && AllFieldsFilled() && gatewayValid;
        RenewButton.IsEnabled = !busy && gatewayValid;
        foreach (var control in new Control[] { GatewayText, CommonNameText, ManufacturerText, ModelText, SerialText, ActivationCodeBox, InstallToggle })
        {
            control.IsEnabled = !busy;
        }
    }

    private void SetStatus(string text, bool error = false, bool success = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = error
            ? System.Windows.Media.Brushes.Crimson
            : success
                ? System.Windows.Media.Brushes.DarkGreen
                : System.Windows.Media.Brushes.DimGray;
    }

}
