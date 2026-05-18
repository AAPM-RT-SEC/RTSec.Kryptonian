using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RTSec.Kryptonian.CaHarness.Models;
using Xunit;

namespace RTSec.Kryptonian.CaHarness.Tests;

public sealed class HarnessApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private string _token = "";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HarnessApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _token = await RegisterTeamAsync("test-team");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- /health ----------

    [Fact]
    public async Task Health_Returns200()
    {
        var resp = await _client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- /api/backends ----------

    [Fact]
    public async Task Backends_ListsAllBackends()
    {
        var backends = await _client.GetFromJsonAsync<string[]>("/api/backends");
        backends.Should().BeEquivalentTo("selfsigned", "adcs", "ejbca", "acme");
    }

    // ---------- /api/teams/register ----------

    [Fact]
    public async Task Register_Returns_Token_And_TeamName()
    {
        var resp = await _client.PostAsJsonAsync("/api/teams/register", new { teamName = "My Team" }, JsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("teamName").GetString().Should().Be("My Team");
    }

    [Fact]
    public async Task Register_EmptyName_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/teams/register", new { teamName = "" }, JsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- unknown token returns 404 ----------

    [Fact]
    public async Task UnknownToken_Returns404()
    {
        var resp = await _client.GetAsync("/teams/00000000-0000-0000-0000-000000000000/api/backends/selfsigned/cacerts");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- /teams/{token}/api/backends/{backend}/cacerts ----------

    [Theory]
    [InlineData("selfsigned")]
    [InlineData("adcs")]
    [InlineData("ejbca")]
    public async Task CaCerts_ReturnsPem(string backend)
    {
        var pems = await _client.GetFromJsonAsync<string[]>($"/teams/{_token}/api/backends/{backend}/cacerts");
        pems.Should().HaveCount(1);
        pems![0].Should().Contain("BEGIN CERTIFICATE");
        X509Certificate2.CreateFromPem(pems[0]).Should().NotBeNull();
    }

    // ---------- selfsigned/issue ----------

    [Fact]
    public async Task SelfSigned_Issue_ValidCsr_Succeeds()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=linac-001"),
            ValidityDays = 7,
            DeviceId = "linac-001"
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/selfsigned/issue", request, JsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);
        result!.Status.Should().Be("issued");
        result.Backend.Should().Be("selfsigned");
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
        result.CaChainPem.Should().HaveCount(1);
        result.SerialNumber.Should().NotBeNullOrWhiteSpace();
        result.Thumbprint.Should().NotBeNullOrWhiteSpace();

        var leafCert = X509Certificate2.CreateFromPem(result.CertificatePem!);
        var caCert = X509Certificate2.CreateFromPem(result.CaChainPem![0]);
        leafCert.Issuer.Should().Be(caCert.Subject);
    }

    [Fact]
    public async Task SelfSigned_Issue_MalformedCsr_ReturnsRejected()
    {
        var request = new IssueRequest { CsrBase64Der = "not-valid-base64!!!" };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/selfsigned/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("rejected");
        result.ReasonCode.Should().Be("invalid-csr");
    }

    // ---------- adcs/issue ----------

    [Fact]
    public async Task Adcs_Issue_AllowedTemplate_Succeeds()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=linac-001"),
            TemplateName = "DicomDeviceAuthentication",
            ValidityDays = 7
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/adcs/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("issued");
        result.Backend.Should().Be("adcs");
        result.Issuer.Should().Contain("ADCS");
    }

    [Fact]
    public async Task Adcs_Issue_RejectedTemplate_ReturnsRejected()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=linac-001"),
            TemplateName = "RejectedTemplate"
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/adcs/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("rejected");
        result.ReasonCode.Should().Be("template-policy-rejected");
    }

    [Fact]
    public async Task Adcs_Issue_UnknownTemplate_ReturnsRejected()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=linac-001"),
            TemplateName = "NonExistentTemplate"
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/adcs/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("rejected");
        result.ReasonCode.Should().Be("unknown-template");
    }

    [Fact]
    public async Task Adcs_Issue_MissingTemplate_ReturnsRejected()
    {
        var request = new IssueRequest { CsrBase64Der = MakeCsrBase64("CN=linac-001") };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/adcs/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("rejected");
        result.ReasonCode.Should().Be("missing-template");
    }

    [Fact]
    public async Task Adcs_Issue_PendingTemplate_ReturnsPending_ThenApproveSucceeds()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=linac-pending"),
            TemplateName = "PendingApprovalTemplate"
        };

        var issueResp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/adcs/issue", request, JsonOpts);
        var pending = await issueResp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        pending!.Status.Should().Be("pending");
        pending.RequestId.Should().NotBeNullOrWhiteSpace();

        var approveResp = await _client.PostAsync(
            $"/teams/{_token}/api/backends/adcs/approve/{pending.RequestId}", null);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var issued = await approveResp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);
        issued!.Status.Should().Be("issued");
        issued.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task Adcs_Approve_UnknownRequestId_Returns404()
    {
        var resp = await _client.PostAsync($"/teams/{_token}/api/backends/adcs/approve/does-not-exist", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- ejbca/issue ----------

    [Fact]
    public async Task Ejbca_Issue_AllowedProfile_Succeeds()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=bridge-001"),
            TemplateName = "MedicalDeviceTLS",
            ValidityDays = 7
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/ejbca/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("issued");
        result.Backend.Should().Be("ejbca");
        result.Issuer.Should().Contain("EJBCA");
    }

    [Fact]
    public async Task Ejbca_Issue_RejectedProfile_ReturnsRejected()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=bridge-001"),
            TemplateName = "RejectedProfile"
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/ejbca/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("rejected");
        result.ReasonCode.Should().Be("profile-policy-rejected");
    }

    [Fact]
    public async Task Ejbca_Issue_UnknownProfile_ReturnsRejected()
    {
        var request = new IssueRequest
        {
            CsrBase64Der = MakeCsrBase64("CN=bridge-001"),
            TemplateName = "NoSuchProfile"
        };

        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/ejbca/issue", request, JsonOpts);
        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        result!.Status.Should().Be("rejected");
        result.ReasonCode.Should().Be("unknown-profile");
    }

    // ---------- revoke ----------

    [Fact]
    public async Task Revoke_IssuedCert_Succeeds()
    {
        var issueResp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/selfsigned/issue",
            new IssueRequest { CsrBase64Der = MakeCsrBase64("CN=revoke-test"), ValidityDays = 1 },
            JsonOpts);
        var issued = await issueResp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        var revokeResp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/selfsigned/revoke",
            new RevokeRequest { SerialNumber = issued!.SerialNumber! }, JsonOpts);
        revokeResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoke_UnknownSerial_Returns404()
    {
        var resp = await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/selfsigned/revoke",
            new RevokeRequest { SerialNumber = "DEADBEEF" }, JsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- issued ----------

    [Fact]
    public async Task Issued_ReflectsIssuedCerts()
    {
        await _client.PostAsJsonAsync($"/teams/{_token}/api/backends/adcs/issue",
            new IssueRequest
            {
                CsrBase64Der = MakeCsrBase64("CN=issued-list-test"),
                TemplateName = "DicomDeviceAuthentication"
            }, JsonOpts);

        var resp = await _client.GetAsync($"/teams/{_token}/api/backends/adcs/issued");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        items.Should().NotBeEmpty();
    }

    // ---------- reset ----------

    [Fact]
    public async Task Reset_ClearsIssuedAndRegeneratesCa()
    {
        var pemsBefore = await _client.GetFromJsonAsync<string[]>(
            $"/teams/{_token}/api/backends/selfsigned/cacerts");
        var caSubjectBefore = X509Certificate2.CreateFromPem(pemsBefore![0]).Subject;

        var resetResp = await _client.PostAsync($"/teams/{_token}/api/backends/selfsigned/reset", null);
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var pemsAfter = await _client.GetFromJsonAsync<string[]>(
            $"/teams/{_token}/api/backends/selfsigned/cacerts");
        var caSubjectAfter = X509Certificate2.CreateFromPem(pemsAfter![0]).Subject;

        caSubjectAfter.Should().Be(caSubjectBefore);
        pemsAfter[0].Should().NotBe(pemsBefore[0]);
    }

    // ---------- team isolation ----------

    [Fact]
    public async Task Reset_OnOneTeam_DoesNotAffectAnotherTeam()
    {
        var tokenA = await RegisterTeamAsync("isolation-team-a");
        var tokenB = await RegisterTeamAsync("isolation-team-b");

        // Issue on team B so it has a cert
        await _client.PostAsJsonAsync($"/teams/{tokenB}/api/backends/selfsigned/issue",
            new IssueRequest { CsrBase64Der = MakeCsrBase64("CN=team-b-device"), ValidityDays = 1 },
            JsonOpts);

        var pemBeforeReset = await _client.GetFromJsonAsync<string[]>(
            $"/teams/{tokenB}/api/backends/selfsigned/cacerts");

        // Reset team A — should have no effect on team B
        await _client.PostAsync($"/teams/{tokenA}/api/backends/selfsigned/reset", null);

        var pemAfterReset = await _client.GetFromJsonAsync<string[]>(
            $"/teams/{tokenB}/api/backends/selfsigned/cacerts");
        pemAfterReset![0].Should().Be(pemBeforeReset![0]);
    }

    // ---------- scoreboard ----------

    [Fact]
    public async Task Scoreboard_Returns_JsonArray()
    {
        var resp = await _client.GetAsync("/api/scoreboard");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var scores = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        scores.Should().NotBeNull();
    }

    [Fact]
    public async Task Scoreboard_Html_Returns200()
    {
        var resp = await _client.GetAsync("/scoreboard");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    // ---------- DICOM stow ----------

    [Fact]
    public async Task DicomStow_MissingCertHeader_Returns400()
    {
        var resp = await _client.PostAsync(
            $"/teams/{_token}/dicom/backends/selfsigned/stow",
            new StringContent("fake-dicom", System.Text.Encoding.UTF8, "application/dicom"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DicomStow_InvalidCert_Returns401()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/dicom/backends/selfsigned/stow");
        req.Content = new StringContent("fake-dicom", System.Text.Encoding.UTF8, "application/dicom");
        req.Headers.Add("X-Device-Certificate", "not-a-valid-pem");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DicomStow_UnknownToken_Returns404()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/teams/00000000-0000-0000-0000-000000000000/dicom/backends/selfsigned/stow");
        req.Content = new StringContent("fake-dicom", System.Text.Encoding.UTF8, "application/dicom");
        req.Headers.Add("X-Device-Certificate", "not-a-valid-pem");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DicomStow_ValidCert_IsAccepted()
    {
        // Issue a cert for this team
        var issueResp = await _client.PostAsJsonAsync(
            $"/teams/{_token}/api/backends/selfsigned/issue",
            new IssueRequest { CsrBase64Der = MakeCsrBase64("CN=dicom-device"), ValidityDays = 7, DeviceId = "dicom-device" },
            JsonOpts);
        var issued = await issueResp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        // Submit DICOM with the cert as base64 DER (no newlines — PEM is not valid in HTTP headers)
        var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/dicom/backends/selfsigned/stow");
        req.Content = new StringContent("fake-dicom-payload", System.Text.Encoding.UTF8, "application/dicom");
        req.Headers.Add("X-Device-Certificate", issued!.CertificateDerBase64!);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        // Cert from /issue has no EST OID — direct-issue path, not gateway-enrolled
        body.GetProperty("usedGateway").GetBoolean().Should().BeFalse();
    }

    // ---------- EST enrollment ----------

    [Fact]
    public async Task Est_Simpleenroll_Selfsigned_ValidCsr_ReturnsIssued()
    {
        var csr = MakeCsrBase64("CN=est-device");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/est/selfsigned/simpleenroll");
        req.Content = new StringContent(csr, System.Text.Encoding.UTF8, "application/pkcs10");
        req.Headers.Add("X-Device-Id", "est-device-001");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);
        result!.Status.Should().Be("issued");
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Theory]
    [InlineData("adcs")]
    [InlineData("ejbca")]
    public async Task Est_Simpleenroll_AdcsEjbca_Returns410(string backend)
    {
        var csr = MakeCsrBase64("CN=est-device");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/est/{backend}/simpleenroll");
        req.Content = new StringContent(csr, System.Text.Encoding.UTF8, "application/pkcs10");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Est_Simpleenroll_WrongContentType_Returns415()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/est/selfsigned/simpleenroll");
        req.Content = new StringContent(MakeCsrBase64("CN=device"), System.Text.Encoding.UTF8, "application/json");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Est_Simpleenroll_UnknownToken_Returns404()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/teams/00000000-0000-0000-0000-000000000000/est/selfsigned/simpleenroll");
        req.Content = new StringContent(MakeCsrBase64("CN=device"), System.Text.Encoding.UTF8, "application/pkcs10");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Est_DicomStow_EstCert_UsedGatewayTrue()
    {
        // Enroll via EST — cert will carry the OID extension
        var estReq = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/est/selfsigned/simpleenroll");
        estReq.Content = new StringContent(MakeCsrBase64("CN=est-dicom-device"), System.Text.Encoding.UTF8, "application/pkcs10");
        estReq.Headers.Add("X-Device-Id", "est-dicom-device");
        var estResp = await _client.SendAsync(estReq);
        var enrolled = await estResp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);

        // Submit DICOM with the EST-enrolled cert
        var stowReq = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/dicom/backends/selfsigned/stow");
        stowReq.Content = new StringContent("fake-dicom", System.Text.Encoding.UTF8, "application/dicom");
        stowReq.Headers.Add("X-Device-Certificate", enrolled!.CertificateDerBase64!);
        var stowResp = await _client.SendAsync(stowReq);
        stowResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await stowResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        body.GetProperty("usedGateway").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Est_Simplereenroll_ValidCsr_ReturnsIssued()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/est/selfsigned/simplereenroll");
        req.Content = new StringContent(MakeCsrBase64("CN=renewal-device"), System.Text.Encoding.UTF8, "application/pkcs10");
        req.Headers.Add("X-Device-Id", "renewal-device");

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.Content.ReadFromJsonAsync<IssueResponse>(JsonOpts);
        result!.Status.Should().Be("issued");
    }

    // ---------- SCEP (ADCS backend) ----------

    [Fact]
    public async Task Scep_GetCACert_Returns200_WithBinaryContent()
    {
        var resp = await _client.GetAsync($"/teams/{_token}/scep/adcs?operation=GetCACert");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/x-x509-ca-cert");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Scep_GetCACaps_Returns200_ContainsSha256()
    {
        var resp = await _client.GetAsync($"/teams/{_token}/scep/adcs?operation=GetCACaps");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await resp.Content.ReadAsStringAsync();
        text.Should().Contain("SHA-256");
    }

    [Fact]
    public async Task Scep_UnknownOperation_Returns400()
    {
        var resp = await _client.GetAsync($"/teams/{_token}/scep/adcs?operation=UnknownOp");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Scep_UnknownToken_Returns404()
    {
        var resp = await _client.GetAsync("/teams/00000000-0000-0000-0000-000000000000/scep/adcs?operation=GetCACert");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- EJBCA REST API ----------

    [Fact]
    public async Task EjbcaRest_Enroll_AllowedProfile_Returns200WithCertificate()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var csrDer = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=ejbca-device", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1)
            .CreateSigningRequest();
        var pem = "-----BEGIN CERTIFICATE REQUEST-----\n"
            + Convert.ToBase64String(csrDer, Base64FormattingOptions.InsertLineBreaks)
            + "\n-----END CERTIFICATE REQUEST-----";

        var body = new
        {
            certificate_request = pem,
            certificate_profile_name = "MedicalDeviceTLS",
            username = "ejbca-test-device"
        };

        var resp = await _client.PostAsJsonAsync(
            $"/teams/{_token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        result.GetProperty("certificate").GetString().Should().NotBeNullOrWhiteSpace();
        result.GetProperty("serial_number").GetString().Should().NotBeNullOrWhiteSpace();
        result.GetProperty("response_type").GetString().Should().Be("CERTIFICATE");
    }

    [Fact]
    public async Task EjbcaRest_Enroll_UnknownProfile_Returns422()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var csrDer = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=ejbca-device", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1)
            .CreateSigningRequest();
        var pem = "-----BEGIN CERTIFICATE REQUEST-----\n"
            + Convert.ToBase64String(csrDer, Base64FormattingOptions.InsertLineBreaks)
            + "\n-----END CERTIFICATE REQUEST-----";

        var body = new
        {
            certificate_request = pem,
            certificate_profile_name = "NoSuchProfile"
        };

        var resp = await _client.PostAsJsonAsync(
            $"/teams/{_token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EjbcaRest_Enroll_MissingProfile_Returns422()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var csrDer = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=ejbca-device", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1)
            .CreateSigningRequest();
        var pem = "-----BEGIN CERTIFICATE REQUEST-----\n"
            + Convert.ToBase64String(csrDer, Base64FormattingOptions.InsertLineBreaks)
            + "\n-----END CERTIFICATE REQUEST-----";

        var body = new
        {
            certificate_request = pem
            // no certificate_profile_name
        };

        var resp = await _client.PostAsJsonAsync(
            $"/teams/{_token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll", body);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EjbcaRest_Enroll_UnknownToken_Returns404()
    {
        var body = new { certificate_request = "pem", certificate_profile_name = "MedicalDeviceTLS" };
        var resp = await _client.PostAsJsonAsync(
            "/teams/00000000-0000-0000-0000-000000000000/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll", body);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- EJBCA REST confirms DICOM gateway enrollment ----------

    [Fact]
    public async Task EjbcaRest_DicomStow_EnrolledCert_UsedGatewayTrue()
    {
        // Enroll via EJBCA REST
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var csrDer = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=ejbca-dicom-device", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1)
            .CreateSigningRequest();
        var pem = "-----BEGIN CERTIFICATE REQUEST-----\n"
            + Convert.ToBase64String(csrDer, Base64FormattingOptions.InsertLineBreaks)
            + "\n-----END CERTIFICATE REQUEST-----";

        var enrollResp = await _client.PostAsJsonAsync(
            $"/teams/{_token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll",
            new { certificate_request = pem, certificate_profile_name = "MedicalDeviceTLS", username = "ejbca-dicom-device" });

        enrollResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var enrolled = await enrollResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var certDerBase64 = enrolled.GetProperty("certificate").GetString()!;

        // Submit DICOM with the EJBCA-enrolled cert
        var stowReq = new HttpRequestMessage(HttpMethod.Post, $"/teams/{_token}/dicom/backends/ejbca/stow");
        stowReq.Content = new StringContent("fake-dicom", System.Text.Encoding.UTF8, "application/dicom");
        stowReq.Headers.Add("X-Device-Certificate", certDerBase64);
        var stowResp = await _client.SendAsync(stowReq);
        stowResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await stowResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        body.GetProperty("usedGateway").GetBoolean().Should().BeTrue();
    }

    // ---------- unknown backend ----------

    [Fact]
    public async Task UnknownBackend_Returns404()
    {
        var resp = await _client.GetAsync($"/teams/{_token}/api/backends/nonexistent/cacerts");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private async Task<string> RegisterTeamAsync(string teamName)
    {
        var resp = await _client.PostAsJsonAsync("/api/teams/register", new { teamName }, JsonOpts);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("token").GetString()!;
    }

    private static string MakeCsrBase64(string subjectDn)
    {
        using var key = RSA.Create(2048);
        var csr = new CertificateRequest(subjectDn, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(csr.CreateSigningRequest());
    }
}
