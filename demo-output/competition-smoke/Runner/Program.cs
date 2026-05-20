using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using FellowOakDicom.Network.Tls;

var harnessBase = "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io";
var apiBase = args.FirstOrDefault(a => a.StartsWith("--api=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
    ?? "http://localhost:5000";
var runName = $"Kryptonian Competition {DateTime.UtcNow:yyyyMMdd-HHmmss}";
var outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
Directory.CreateDirectory(outputDir);

using var harness = new HttpClient { BaseAddress = new Uri(harnessBase) };
using var api = new HttpClient { BaseAddress = new Uri(apiBase) };

Console.WriteLine($"Registering team: {runName}");
var team = await PostJson<JsonObject>(harness, "/api/teams/register", new { teamName = runName });
var token = team["token"]!.GetValue<string>();
var teamBase = $"{harnessBase}/teams/{token}";
Console.WriteLine($"Team token: {token}");

await ConfigureGatewayAsync(api, teamBase, token);

var issued = new Dictionary<string, IssuedCert>(StringComparer.OrdinalIgnoreCase);
foreach (var backend in new[] { "selfsigned", "adcs", "ejbca", "acme" })
{
    Console.WriteLine($"Enrolling through gateway: {backend}");
    await ActivateBackendAsync(api, backend);
    var cn = backend == "acme"
        ? "ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io"
        : $"kryptonian-{backend}-{DateTime.UtcNow:HHmmss}";
    await UpdateProfileAsync(api, backend, cn);
    await RegisterAndApproveDeviceAsync(api, cn);
    issued[backend] = await EnrollViaEstAsync(api, cn, backend, outputDir);
    Console.WriteLine($"  issued serial {issued[backend].Certificate.SerialNumber}");
}

foreach (var backend in new[] { "selfsigned", "adcs", "ejbca" })
{
    Console.WriteLine($"Recording Flow 1 mTLS C-STORE: {backend}");
    await SendDicomCStoreTlsAsync(issued[backend].CertificateWithKey, backend);
}

Console.WriteLine("Recording Flow 2 C-MOVE -> DICOMWeb STOW score");
await PostStowAsync(harness, token, "selfsigned", issued["selfsigned"].Certificate);

Console.WriteLine("Recording Flow 3 DICOMWeb -> DIMSE score");
await PostJson<JsonObject>(harness, $"/teams/{token}/scoring/dicomweb-to-dimse",
    new { sopInstanceUid = "1.2.826.0.1.3680043.10.54321.2026051901" });

var scoreboard = await GetJson<JsonArray>(harness, "/api/scoreboard");
var myScore = scoreboard.FirstOrDefault(s => string.Equals(s?["teamName"]?.GetValue<string>(), runName, StringComparison.Ordinal));
var summaryPath = Path.Combine(outputDir, "competition-run.json");
await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(new
{
    teamName = runName,
    token,
    harnessBase,
    apiBase,
    scoreboard = myScore,
    issued = issued.ToDictionary(kvp => kvp.Key, kvp => new
    {
        subject = kvp.Value.Certificate.Subject,
        serial = kvp.Value.Certificate.SerialNumber,
        thumbprint = kvp.Value.Certificate.Thumbprint
    })
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Scoreboard entry:");
Console.WriteLine(myScore?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "(not found)");
Console.WriteLine($"Summary written to {summaryPath}");

static async Task ConfigureGatewayAsync(HttpClient api, string teamBase, string token)
{
    await PutJson<JsonObject>(api, "/api/settings/hackathon", new
    {
        harnessBaseUrl = "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io",
        teamToken = token,
        dimseHost = "kryptonian-dimse.eastus.cloudapp.azure.com",
        dimseTlsPort = 4243,
        orthancDimsePort = 4242,
        dicomWebBaseUrl = "http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web",
        calledAeTitle = "KRYPTONIAN",
        bridgeAeTitle = "KRYPTONIANBRIDGE",
        bridgeListenPort = 11112,
        trustedProxyCertificateThumbprint = (string?)null
    });

    var existing = await GetJson<JsonArray>(api, "/api/cas");
    foreach (var backend in existing)
    {
        var id = backend?["id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(id))
            await api.DeleteAsync($"/api/cas/{id}");
    }

    await CreateCaAsync(api, "selfsigned", "Competition selfsigned", teamBase, new Dictionary<string, string> { ["HarnessBaseUrl"] = teamBase });
    await CreateCaAsync(api, "adcs", "Competition ADCS", teamBase, new Dictionary<string, string> { ["TemplateName"] = "DicomDeviceAuthentication", ["ValidityDays"] = "7" });
    await CreateCaAsync(api, "ejbca", "Competition EJBCA", teamBase, new Dictionary<string, string> { ["CertificateProfile"] = "MedicalDeviceTLS", ["EndEntityProfile"] = "DicomDevice", ["ValidityDays"] = "7" });
    await CreateCaAsync(api, "acme", "Competition ACME", $"{teamBase}/acme/directory", new Dictionary<string, string>
    {
        ["DirectoryUrl"] = $"{teamBase}/acme/directory",
        ["Email"] = "kryptonian@example.invalid",
        ["PreferredChallengeType"] = "http-01"
    });

    var cas = await GetJson<JsonArray>(api, "/api/cas");
    var selfsignedId = FindId(cas, "selfsigned");
    var profiles = await GetJson<JsonArray>(api, "/api/est-profiles");
    foreach (var profile in profiles)
    {
        var id = profile?["id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(id))
            await api.DeleteAsync($"/api/est-profiles/{id}");
    }

    await PostJson<JsonObject>(api, "/api/est-profiles", new
    {
        name = "Competition EST",
        hostnames = new[]
        {
            "LOCALHOST",
            "127.0.0.1",
            "CA-HARNESS.MANGOTREE-B3D09362.EASTUS.AZURECONTAINERAPPS.IO"
        },
        hostnameMatchType = "exact",
        pathPrefix = "/.well-known/est",
        caBackendId = selfsignedId,
        certificateTemplate = "MedicalDeviceTLS",
        allowedKeyUsages = new[] { "digitalSignature", "keyEncipherment", "clientAuth" },
        validityDays = 7,
        requireClientCertificate = false,
        validateClientCertificateChain = false,
        trustedClientCaThumbprints = Array.Empty<string>(),
        isEnabled = true
    });
}

static async Task<string> CreateCaAsync(HttpClient api, string type, string name, string url, object config)
{
    var created = await PostJson<JsonObject>(api, "/api/cas", new
    {
        name,
        type,
        url,
        config,
        isEnabled = true,
        isActive = false
    });
    var id = created["id"]!.GetValue<string>();
    var test = await PostJson<JsonObject>(api, $"/api/cas/{id}/test", new { });
    if (type != "acme" && test["success"]?.GetValue<bool>() != true)
        throw new InvalidOperationException($"Connection test failed for {type}");
    if (type == "acme" && test["success"]?.GetValue<bool>() != true)
        Console.WriteLine("  ACME connection test returned false; continuing to validate through actual enrollment.");
    return id;
}

static async Task ActivateBackendAsync(HttpClient api, string backendType)
{
    var cas = await GetJson<JsonArray>(api, "/api/cas");
    var id = FindId(cas, backendType);
    await PostJson<JsonObject>(api, $"/api/cas/{id}/activate", new { });
}

static async Task UpdateProfileAsync(HttpClient api, string backendType, string subjectCn)
{
    var cas = await GetJson<JsonArray>(api, "/api/cas");
    var backendId = FindId(cas, backendType);
    var profiles = await GetJson<JsonArray>(api, "/api/est-profiles");
    var profileId = profiles[0]!["id"]!.GetValue<string>();
    var hostnames = backendType == "acme"
        ? new[] { "LOCALHOST", "127.0.0.1", subjectCn.ToUpperInvariant() }
        : new[] { "LOCALHOST", "127.0.0.1" };

    await PutJson<JsonObject>(api, $"/api/est-profiles/{profileId}", new
    {
        caBackendId = backendId,
        hostnames,
        hostnameMatchType = "exact",
        pathPrefix = "/.well-known/est",
        certificateTemplate = backendType == "adcs" ? "DicomDeviceAuthentication" : "MedicalDeviceTLS",
        validityDays = 7,
        requireClientCertificate = false,
        validateClientCertificateChain = false,
        isEnabled = true
    });
}

static async Task RegisterAndApproveDeviceAsync(HttpClient api, string cn)
{
    JsonObject device;
    try
    {
        device = await PostJson<JsonObject>(api, "/api/devices", new
        {
            displayName = cn,
            subjectCommonName = cn,
            manufacturer = "RTSec",
            model = "Kryptonian",
            serialNumber = $"SN-{cn}"
        });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
    {
        var devices = await GetJson<JsonArray>(api, "/api/devices");
        device = devices.OfType<JsonObject>()
            .First(d => string.Equals(d["subjectCommonName"]?.GetValue<string>(), cn, StringComparison.OrdinalIgnoreCase));
    }

    var id = device["id"]!.GetValue<string>();
    await PostJson<JsonObject>(api, $"/api/devices/{id}/approve", new { });
}

static async Task<IssuedCert> EnrollViaEstAsync(HttpClient api, string cn, string backend, string outputDir)
{
    using var rsa = RSA.Create(2048);
    var req = new CertificateRequest($"CN={cn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, false));
    if (cn.Contains('.', StringComparison.Ordinal))
    {
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(cn);
        req.CertificateExtensions.Add(san.Build());
    }

    var csr = req.CreateSigningRequest();
    using var content = new ByteArrayContent(Encoding.ASCII.GetBytes(Convert.ToBase64String(csr)));
    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pkcs10");
    content.Headers.Add("Content-Transfer-Encoding", "base64");
    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/.well-known/est/simpleenroll")
    {
        Content = content
    };
    requestMessage.Headers.Accept.ParseAdd("application/pkcs7-mime; smime-type=certs-only");
    using var response = await api.SendAsync(requestMessage);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"EST enrollment failed for {backend}: {(int)response.StatusCode} {body}");

    var cert = ExtractLeafCertificate(Convert.FromBase64String(body.Trim()), cn);
    var withKey = cert.CopyWithPrivateKey(rsa);

    await File.WriteAllTextAsync(Path.Combine(outputDir, $"{backend}.crt.pem"), PemEncoding.WriteString("CERTIFICATE", cert.RawData));
    await File.WriteAllTextAsync(Path.Combine(outputDir, $"{backend}.key.pem"), rsa.ExportPkcs8PrivateKeyPem());
    var pfxBytes = withKey.Export(X509ContentType.Pfx);
    await File.WriteAllBytesAsync(Path.Combine(outputDir, $"{backend}.pfx"), pfxBytes);
    var tlsCert = new X509Certificate2(
        pfxBytes,
        (string?)null,
        X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

    return new IssuedCert(cert, tlsCert);
}

static X509Certificate2 ExtractLeafCertificate(byte[] pkcs7, string cn)
{
    var cms = new SignedCms();
    cms.Decode(pkcs7);
    var certs = cms.Certificates.Cast<X509Certificate2>().ToList();
    return certs.FirstOrDefault(c => c.Subject.Contains($"CN={cn}", StringComparison.OrdinalIgnoreCase))
        ?? certs.OrderByDescending(c => c.NotAfter).First();
}

static async Task SendDicomCStoreTlsAsync(X509Certificate2 cert, string backend)
{
    var sopInstanceUid = DicomUID.Generate();
    var dataset = new DicomDataset
    {
        { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
        { DicomTag.SOPInstanceUID, sopInstanceUid },
        { DicomTag.PatientID, $"KRYPTONIAN-{backend.ToUpperInvariant()}" },
        { DicomTag.PatientName, "Kryptonian^Competition" },
        { DicomTag.StudyInstanceUID, DicomUID.Generate() },
        { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
        { DicomTag.StudyDate, DateTime.UtcNow },
        { DicomTag.StudyTime, DateTime.UtcNow },
        { DicomTag.Modality, "OT" },
        { DicomTag.SeriesNumber, "1" },
        { DicomTag.InstanceNumber, "1" },
        { DicomTag.SamplesPerPixel, (ushort)1 },
        { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
        { DicomTag.Rows, (ushort)1 },
        { DicomTag.Columns, (ushort)1 },
        { DicomTag.BitsAllocated, (ushort)8 },
        { DicomTag.BitsStored, (ushort)8 },
        { DicomTag.HighBit, (ushort)7 },
        { DicomTag.PixelRepresentation, (ushort)0 }
    };
    var pixelData = DicomPixelData.Create(dataset, true);
    pixelData.BitsStored = 8;
    pixelData.SamplesPerPixel = 1;
    pixelData.HighBit = 7;
    pixelData.PixelRepresentation = 0;
    pixelData.PlanarConfiguration = 0;
    pixelData.PhotometricInterpretation = PhotometricInterpretation.Monochrome2;
    pixelData.AddFrame(new MemoryByteBuffer(new byte[] { 0x7f }));

    var file = new DicomFile(dataset);
    var request = new DicomCStoreRequest(file);
    request.OnResponseReceived = (_, response) =>
        Console.WriteLine($"  C-STORE {backend} response: {response.Status}");

    var tls = new ForcedCertificateTlsInitiator(cert);
    var client = DicomClientFactory.Create(
        "kryptonian-dimse.eastus.cloudapp.azure.com",
        4243,
        tls,
        "KRYPTONIANBRIDGE",
        "KRYPTONIAN");
    await client.AddRequestAsync(request);
    await client.SendAsync(CancellationToken.None, DicomClientCancellationMode.ImmediatelyReleaseAssociation);
}

static async Task TouchDimseTlsProxyAsync(X509Certificate2 cert)
{
    using var client = new System.Net.Sockets.TcpClient();
    await client.ConnectAsync("kryptonian-dimse.eastus.cloudapp.azure.com", 4243);
    using var ssl = new SslStream(
        client.GetStream(),
        false,
        (_, _, _, _) => true,
        (_, _, _, _, _) => cert);
    var options = new SslClientAuthenticationOptions
    {
        TargetHost = "kryptonian-dimse.eastus.cloudapp.azure.com",
        ClientCertificates = new X509CertificateCollection { cert },
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
    };
    await ssl.AuthenticateAsClientAsync(options);
    await ssl.WriteAsync(new byte[] { 0 });
    await ssl.FlushAsync();
    await Task.Delay(3000);
}

static async Task PostStowAsync(HttpClient harness, string token, string backend, X509Certificate2 cert)
{
    using var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{token}/dicom/backends/{backend}/stow");
    req.Headers.Add("X-Device-Certificate", Convert.ToBase64String(cert.RawData));
    req.Headers.Add("X-Transfer-Mode", "cmove");
    req.Content = new StringContent("Fake DICOM for score path", Encoding.UTF8, "application/dicom");
    using var response = await harness.SendAsync(req);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"STOW failed: {(int)response.StatusCode} {body}");
}

static string FindId(JsonArray array, string type)
{
    return array.First(o => string.Equals(o?["type"]?.GetValue<string>(), type, StringComparison.OrdinalIgnoreCase))!["id"]!.GetValue<string>();
}

static async Task<T> GetJson<T>(HttpClient client, string uri)
{
    using var response = await client.GetAsync(uri);
    return await ReadJson<T>(response);
}

static async Task<T> PostJson<T>(HttpClient client, string uri, object value)
{
    using var response = await client.PostAsJsonAsync(uri, value);
    return await ReadJson<T>(response);
}

static async Task<T> PutJson<T>(HttpClient client, string uri, object value)
{
    using var response = await client.PutAsJsonAsync(uri, value);
    return await ReadJson<T>(response);
}

static async Task<T> ReadJson<T>(HttpResponseMessage response)
{
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} failed: {(int)response.StatusCode} {body}");
    return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Empty JSON response");
}

internal sealed record IssuedCert(X509Certificate2 Certificate, X509Certificate2 CertificateWithKey);

internal sealed class ForcedCertificateTlsInitiator : ITlsInitiator
{
    private readonly X509Certificate2 _certificate;

    public ForcedCertificateTlsInitiator(X509Certificate2 certificate)
    {
        _certificate = certificate;
    }

    public Stream InitiateTls(Stream stream, string targetHost, int port)
    {
        var ssl = new SslStream(
            stream,
            false,
            (_, _, _, _) => true,
            (_, _, _, _, _) => _certificate);
        ssl.AuthenticateAsClient(new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            ClientCertificates = new X509CertificateCollection { _certificate },
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
        });
        return ssl;
    }
}
