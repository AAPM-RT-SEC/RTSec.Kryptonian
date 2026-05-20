using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Kryptonian.MedicalDevice;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new();

    public MainWindow()
    {
        InitializeComponent();
        foreach (var box in new[] { GatewayText, CommonNameText, ManufacturerText, ModelText, SerialText })
        {
            box.TextChanged += OnInputChanged;
        }
        OnInputChanged(this, EventArgs.Empty!);
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        EnrollButton.IsEnabled = AllFieldsFilled() && Uri.TryCreate(GatewayText.Text.Trim().TrimEnd('/'), UriKind.Absolute, out _);
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

        var commonName = CommonNameText.Text.Trim();
        var manufacturer = ManufacturerText.Text.Trim();
        var model = ModelText.Text.Trim();
        var serial = SerialText.Text.Trim();
        var activationCode = ActivationCodeBox.Password;
        var install = InstallToggle.IsChecked == true;

        SetBusy(true);
        try
        {
            SetStatus("Generating key pair and submitting CSR...");
            using var rsa = RSA.Create(2048);
            var csrDer = BuildCsr(rsa, commonName);

            SetStatus("Enrolling with activation code...");
            var certificate = await EnrollAsync(gatewayUri, rsa, csrDer, activationCode, manufacturer, model, serial);

            if (install)
            {
                InstallCertificate(certificate);
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

    private static async Task<X509Certificate2> EnrollAsync(
        Uri gateway,
        RSA privateKey,
        byte[] csrDer,
        string activationCode,
        string manufacturer,
        string model,
        string serial)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(gateway, "/.well-known/est/simpleenroll"));
        request.Headers.Add("X-Activation-Code", activationCode);
        request.Headers.Add("X-Device-Manufacturer", manufacturer);
        request.Headers.Add("X-Device-Model", model);
        request.Headers.Add("X-Device-Serial-Number", serial);
        request.Headers.Add("Content-Transfer-Encoding", "base64");
        request.Content = new StringContent(Convert.ToBase64String(csrDer), Encoding.ASCII);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"EST enrollment failed ({(int)response.StatusCode}): {body}");
        }

        var pkcs7Der = Convert.FromBase64String(body.Trim());
        var signedCms = new SignedCms();
        signedCms.Decode(pkcs7Der);
        if (signedCms.Certificates.Count == 0)
        {
            throw new InvalidOperationException("Gateway response did not contain any certificates.");
        }

        var leaf = FindLeafCertificate(signedCms.Certificates);
        return leaf.CopyWithPrivateKey(privateKey);
    }

    private static X509Certificate2 FindLeafCertificate(X509Certificate2Collection certs)
    {
        // Prefer end-entity certs (BasicConstraints CA=false / not a CA). Fall back to the first.
        foreach (var cert in certs)
        {
            foreach (var ext in cert.Extensions)
            {
                if (ext is X509BasicConstraintsExtension bc && !bc.CertificateAuthority)
                {
                    return cert;
                }
            }
        }
        return certs[0];
    }

    private static byte[] BuildCsr(RSA rsa, string commonName)
    {
        var subject = new X500DistinguishedName($"CN={commonName}");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSigningRequest();
    }

    private static void InstallCertificate(X509Certificate2 certificate)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(certificate);
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
        EnrollButton.IsEnabled = !busy && AllFieldsFilled();
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
