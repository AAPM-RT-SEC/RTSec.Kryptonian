using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using FellowOakDicom.Network.Tls;
using Kryptonian.DICOMTls;
using Kryptonian.MedicalDevice.Enrollment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var options = DemoOptions.Parse(args);
if (options.ShowHelp)
{
    DemoOptions.PrintHelp();
    return 0;
}

if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine();
    DemoOptions.PrintHelp();
    return 2;
}

var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
var outputRoot = Path.GetFullPath(options.OutputPath);
var generatedPath = Path.Combine(outputRoot, "generated");
var receivedPath = Path.Combine(outputRoot, "received");
Directory.CreateDirectory(generatedPath);
Directory.CreateDirectory(receivedPath);

using var serviceProvider = new ServiceCollection()
    .AddLogging(builder => builder.AddSimpleConsole(static console =>
    {
        console.SingleLine = true;
        console.TimestampFormat = "HH:mm:ss ";
    }).SetMinimumLevel(LogLevel.Warning))
    .AddFellowOakDicom()
    .BuildServiceProvider();

var serverFactory = serviceProvider.GetRequiredService<IDicomServerFactory>();
var clientFactory = serviceProvider.GetRequiredService<IDicomClientFactory>();
var estClient = new EstEnrollmentClient();
using var adminClient = string.IsNullOrWhiteSpace(options.AdminApiKey)
    ? null
    : new GatewayAdminClient(options.Gateway!, options.AdminApiKey);

Console.WriteLine("Kryptonian DICOM TLS demo");
Console.WriteLine($"Gateway: {options.Gateway}");
Console.WriteLine($"Artifacts: {outputRoot}");
Console.WriteLine();

if (options.HasManagementCommand)
{
    return await RunManagementCommandAsync(options, adminClient!);
}

var createdDevices = new List<ProvisionedActivation>();

try
{
    var activationCodeA = options.ActivationCodeA;
    if (string.IsNullOrWhiteSpace(activationCodeA))
    {
        activationCodeA = await ProvisionActivationAsync(
            adminClient!,
            $"DICOM TLS Demo Sender {timestamp}",
            options.ActivationValidMinutes,
            createdDevices);
    }

    var activationCodeB = options.ActivationCodeB;
    if (string.IsNullOrWhiteSpace(activationCodeB))
    {
        activationCodeB = await ProvisionActivationAsync(
            adminClient!,
            $"DICOM TLS Demo Receiver {timestamp}",
            options.ActivationValidMinutes,
            createdDevices);
    }

    var deviceA = await EnrollNodeAsync(
        estClient,
        options.Gateway!,
        "Device A",
        options.DeviceACn ?? $"kryptonian-dicom-scu-{timestamp}",
        options.DeviceASerial ?? $"DICOM-SCU-{timestamp}",
        options.DeviceAAeTitle,
        activationCodeA!,
        outputRoot);

    var deviceB = await EnrollNodeAsync(
        estClient,
        options.Gateway!,
        "Device B",
        options.DeviceBCn ?? $"kryptonian-dicom-scp-{timestamp}",
        options.DeviceBSerial ?? $"DICOM-SCP-{timestamp}",
        options.DeviceBAeTitle,
        activationCodeB!,
        outputRoot);

    Console.WriteLine();
    PrintCertificate("Device A", deviceA.Certificate);
    PrintCertificate("Device B", deviceB.Certificate);
    Console.WriteLine();

    var aTrustsB = CertificateTrustValidator.Validate(deviceB.Certificate, deviceA.IssuedCertificates);
    var bTrustsA = CertificateTrustValidator.Validate(deviceA.Certificate, deviceB.IssuedCertificates);
    Console.WriteLine($"A trusts B through gateway CA: {FormatTrust(aTrustsB)}");
    Console.WriteLine($"B trusts A through gateway CA: {FormatTrust(bTrustsA)}");
    if (!aTrustsB.IsTrusted || !bTrustsA.IsTrusted)
    {
        return 1;
    }

    var receiverState = new ReceivedDicomStore(deviceB, receivedPath);
    var serverValidationResults = new ConcurrentQueue<CertificateTrustResult>();
    var clientValidationResults = new ConcurrentQueue<CertificateTrustResult>();

    using var server = serverFactory.Create<DemoStoreScp>(
        "127.0.0.1",
        options.Port,
        CreateTlsAcceptor(deviceB, serverValidationResults),
        Encoding.UTF8,
        logger: null,
        userState: receiverState,
        configure: null);

    Console.WriteLine();
    Console.WriteLine($"Started {deviceB.AeTitle} TLS Store SCP on 127.0.0.1:{options.Port}");

    var generatedFiles = await DicomFixtureGenerator.CreateAsync(options.Count, generatedPath);
    var expectedSopInstanceUids = generatedFiles.Select(file => file.SopInstanceUid).ToArray();

    var tlsInitiator = CreateTlsInitiator(deviceA, deviceB, clientValidationResults);
    var cstoreStatuses = new ConcurrentDictionary<string, DicomStatus>();

    foreach (var generated in generatedFiles)
    {
        var request = new DicomCStoreRequest(generated.Path);
        request.OnResponseReceived = (_, response) =>
        {
            cstoreStatuses[generated.SopInstanceUid] = response.Status;
            Console.WriteLine($"C-STORE response {generated.SopInstanceUid}: {response.Status}");
        };

        var client = clientFactory.Create(
            "127.0.0.1",
            options.Port,
            tlsInitiator,
            deviceA.AeTitle,
            deviceB.AeTitle);

        await client.AddRequestAsync(request);
        Console.WriteLine($"Sending {generated.SopInstanceUid} from {deviceA.AeTitle} to {deviceB.AeTitle}");
        await client.SendAsync(CancellationToken.None, DicomClientCancellationMode.ImmediatelyReleaseAssociation);
    }

    var received = await receiverState.WaitForAsync(expectedSopInstanceUids.Length, TimeSpan.FromSeconds(10));
    var receivedSops = received.Select(item => item.SopInstanceUid).Order(StringComparer.Ordinal).ToArray();
    var expectedSops = expectedSopInstanceUids.Order(StringComparer.Ordinal).ToArray();
    var allResponsesSucceeded = expectedSopInstanceUids.All(uid =>
        cstoreStatuses.TryGetValue(uid, out var status) && status == DicomStatus.Success);
    var allObjectsArrived = expectedSops.SequenceEqual(receivedSops, StringComparer.Ordinal);

    Console.WriteLine();
    Console.WriteLine($"Client TLS validation: {FormatTrustChecked(clientValidationResults.LastOrDefault())}");
    Console.WriteLine($"Server TLS validation: {FormatTrustChecked(serverValidationResults.LastOrDefault())}");
    foreach (var item in received)
    {
        Console.WriteLine($"Received {item.SopInstanceUid} -> {item.Path}");
    }

    if (!allResponsesSucceeded || !allObjectsArrived)
    {
        Console.Error.WriteLine("DICOM TLS demo failed: response statuses or received SOP Instance UIDs did not match.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine($"Success: {received.Count} DICOM file(s) transported over DIMSE TLS with mutual certificate authentication.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"DICOM TLS demo failed: {ex.Message}");
    return 1;
}
finally
{
    if (createdDevices.Count > 0 && adminClient != null && (options.ArchiveCreatedDevices || options.DeleteCreatedDevices))
    {
        await CleanupCreatedDevicesAsync(adminClient, createdDevices, options.DeleteCreatedDevices);
    }
}

static async Task<int> RunManagementCommandAsync(DemoOptions options, GatewayAdminClient adminClient)
{
    try
    {
        if (!string.IsNullOrWhiteSpace(options.CreateActivationDisplayName))
        {
            var activation = await adminClient.CreateActivationAsync(
                options.CreateActivationDisplayName,
                options.ActivationValidMinutes);
            PrintProvisionedActivation(activation);
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(options.ArchiveDeviceId))
        {
            var device = await adminClient.ArchiveDeviceAsync(options.ArchiveDeviceId);
            Console.WriteLine($"Archived device {device.Id} ({device.DisplayName}). Status: {device.Status}");
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(options.DeleteDeviceId))
        {
            await adminClient.DeleteDeviceAsync(options.DeleteDeviceId);
            Console.WriteLine($"Deleted device {options.DeleteDeviceId}.");
            return 0;
        }

        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Admin API command failed: {ex.Message}");
        return 1;
    }
}

static async Task<string> ProvisionActivationAsync(
    GatewayAdminClient adminClient,
    string displayName,
    int validForMinutes,
    ICollection<ProvisionedActivation> createdDevices)
{
    var activation = await adminClient.CreateActivationAsync(displayName, validForMinutes);
    createdDevices.Add(activation);
    PrintProvisionedActivation(activation);
    return activation.ActivationCode;
}

static void PrintProvisionedActivation(ProvisionedActivation activation)
{
    Console.WriteLine($"Created activation for {activation.DisplayName}");
    Console.WriteLine($"  Device ID: {activation.DeviceId}");
    Console.WriteLine($"  Activation code: {activation.ActivationCode}");
    Console.WriteLine($"  Expires: {activation.ExpiresAt:O}");
}

static async Task CleanupCreatedDevicesAsync(
    GatewayAdminClient adminClient,
    IReadOnlyCollection<ProvisionedActivation> createdDevices,
    bool deleteAfterArchive)
{
    Console.WriteLine();
    Console.WriteLine(deleteAfterArchive
        ? "Cleaning up created devices: archive then delete."
        : "Cleaning up created devices: archive.");

    foreach (var device in createdDevices.Reverse())
    {
        try
        {
            var archived = await adminClient.ArchiveDeviceAsync(device.DeviceId);
            Console.WriteLine($"Archived {archived.Id} ({archived.DisplayName}). Status: {archived.Status}");

            if (deleteAfterArchive)
            {
                await adminClient.DeleteDeviceAsync(device.DeviceId);
                Console.WriteLine($"Deleted {device.DeviceId} ({device.DisplayName}).");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Cleanup failed for {device.DeviceId} ({device.DisplayName}): {ex.Message}");
        }
    }
}

static async Task<MedicalDeviceNode> EnrollNodeAsync(
    EstEnrollmentClient estClient,
    Uri gateway,
    string label,
    string commonName,
    string serial,
    string aeTitle,
    string activationCode,
    string outputRoot)
{
    Console.WriteLine($"Enrolling {label}: CN={commonName}, serial={serial}, AE={aeTitle}");
    var result = await estClient.EnrollAsync(
        gateway,
        new DeviceEnrollmentRequest(
            commonName,
            "Kryptonian Demo",
            "DICOM TLS Prototype",
            serial,
            activationCode));

    var node = new MedicalDeviceNode(label, commonName, serial, aeTitle, result.Certificate, result.IssuedCertificates);
    await File.WriteAllTextAsync(
        Path.Combine(outputRoot, $"{Sanitize(commonName)}.certificate.pem"),
        result.Certificate.ExportCertificatePem());

    return node;
}

static DefaultTlsAcceptor CreateTlsAcceptor(
    MedicalDeviceNode serverNode,
    ConcurrentQueue<CertificateTrustResult> validationResults)
{
    return new DefaultTlsAcceptor(serverNode.Certificate)
    {
        RequireMutualAuthentication = true,
        CheckCertificateRevocation = false,
        Protocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateValidationCallback = (_, certificate, _, _) =>
        {
            var result = CertificateTrustValidator.Validate(certificate, serverNode.IssuedCertificates);
            validationResults.Enqueue(result);
            return result.IsTrusted;
        }
    };
}

static DefaultTlsInitiator CreateTlsInitiator(
    MedicalDeviceNode clientNode,
    MedicalDeviceNode serverNode,
    ConcurrentQueue<CertificateTrustResult> validationResults)
{
    var initiator = new DefaultTlsInitiator
    {
        IgnoreSslPolicyErrors = false,
        CheckCertificateRevocation = false,
        Protocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateValidationCallback = (_, certificate, _, _) =>
        {
            var result = CertificateTrustValidator.Validate(certificate, clientNode.IssuedCertificates);
            validationResults.Enqueue(result);
            using var peerCertificate = certificate == null
                ? null
                : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
            return result.IsTrusted && certificate is not null
                && string.Equals(
                    peerCertificate?.Thumbprint,
                    serverNode.Certificate.Thumbprint,
                    StringComparison.OrdinalIgnoreCase);
        }
    };
    initiator.Certificates.Add(clientNode.Certificate);
    return initiator;
}

static void PrintCertificate(string label, X509Certificate2 certificate)
{
    Console.WriteLine($"{label}:");
    Console.WriteLine($"  Subject: {certificate.Subject}");
    Console.WriteLine($"  Issuer: {certificate.Issuer}");
    Console.WriteLine($"  Serial: {certificate.SerialNumber}");
    Console.WriteLine($"  Thumbprint: {certificate.Thumbprint}");
}

static string FormatTrust(CertificateTrustResult result)
    => result.IsTrusted
        ? $"trusted ({result.Subject}; issuer {result.Issuer})"
        : $"not trusted ({result.Reason})";

static string FormatTrustChecked(CertificateTrustResult? result)
    => result == null ? "not checked" : FormatTrust(result);

static string Sanitize(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
    return new string(chars);
}
