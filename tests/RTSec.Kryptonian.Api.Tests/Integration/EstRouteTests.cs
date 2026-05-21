using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using RTSec.Kryptonian.Api.Controllers;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Api.Tests.Integration;

public class EstRouteTests
{
    public static IEnumerable<object[]> ReenrollRoutes()
    {
        yield return new object[] { "/.well-known/est/simplereenroll" };
        yield return new object[] { "/.well-known/est/devices/simplereenroll" };
        yield return new object[] { "/.well-known/est/simpleReenroll" };
        yield return new object[] { "/.well-known/est/devices/simpleReenroll" };
    }

    // Regression test for #17: case-variant URLs (simplereenroll vs simpleReenroll,
    // with and without {label}) must all resolve to the SAME endpoint without
    // triggering AmbiguousMatchException. The route attributes on the controller
    // must rely on ASP.NET's case-insensitive matching, not stack case-variant
    // [HttpPost] attributes.
    [Theory]
    [MemberData(nameof(ReenrollRoutes))]
    public async Task SimpleReenrollRoutesReturnJsonErrorsInsteadOfNotAcceptable(string route)
    {
        // Arrange
        using var host = await CreateHostAsync();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pkcs7-mime"));
        request.Content = new ByteArrayContent(Convert.FromBase64String("AQID"));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        // Act
        using var response = await client.SendAsync(request);

        // Assert: proves the route matched and the real EST error survived content negotiation.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Client certificate required for re-enrollment");
    }

    [Fact]
    public async Task SimpleReenrollWhenProfileIsMissingReturnsJsonNotFoundInsteadOfNotAcceptable()
    {
        // Arrange
        using var host = await CreateHostAsync(hasProfile: false);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/.well-known/est/simplereenroll");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pkcs7-mime"));
        request.Content = new ByteArrayContent(Convert.FromBase64String("AQID"));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("EST profile not found");
    }

    [Fact]
    public async Task SimpleReenrollUsesAzureForwardedClientCertificateHeader()
    {
        // Arrange
        using var clientCert = CreateClientCertificate("ForwardedDevice");
        using var host = await CreateHostAsync(
            useCertificateForwarding: true,
            configureMocks: (orchestrator, pkcsService, profile) =>
            {
                var csrBytes = new byte[] { 0x01, 0x02, 0x03 };
                pkcsService
                    .Setup(p => p.DecodeEstRequestBodyAsync(
                        It.IsAny<Stream>(),
                        It.IsAny<string?>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(csrBytes);

                orchestrator
                    .Setup(o => o.ReenrollAsync(
                        profile.Id,
                        csrBytes,
                        It.Is<X509Certificate2>(c => c.Thumbprint == clientCert.Thumbprint),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(EnrollmentResult.Failed("forwarded certificate reached orchestrator", 403));
            });
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/.well-known/est/simplereenroll");
        request.Headers.Add("X-ARR-ClientCert", Convert.ToBase64String(clientCert.RawData));
        request.Content = new ByteArrayContent(Convert.FromBase64String("AQID"));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("forwarded certificate reached orchestrator");
    }

    private static async Task<IHost> CreateHostAsync(
        bool hasProfile = true,
        bool useCertificateForwarding = false,
        Action<Mock<IEnrollmentOrchestrator>, Mock<IPkcsService>, EstProfile>? configureMocks = null)
    {
        var profile = new EstProfile
        {
            Id = Guid.NewGuid(),
            Name = "Route Test",
            PathPrefix = "/.well-known/est",
            CaBackendId = Guid.NewGuid(),
            ValidityDays = 365,
            RequireClientCertificate = false,
            IsEnabled = true
        };
        profile.Hostnames.Add("localhost");

        var estProfiles = new Mock<IEstProfileRepository>();
        estProfiles
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasProfile ? profile : null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.EstProfiles).Returns(estProfiles.Object);

        var orchestrator = new Mock<IEnrollmentOrchestrator>();
        var pkcsService = new Mock<IPkcsService>();
        configureMocks?.Invoke(orchestrator, pkcsService, profile);

        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(EstController).Assembly);

                    if (useCertificateForwarding)
                    {
                        services.AddCertificateForwarding(options =>
                        {
                            options.CertificateHeader = "X-ARR-ClientCert";
                        });
                    }

                    services.AddSingleton(unitOfWork.Object);
                    services.AddSingleton(orchestrator.Object);
                    services.AddSingleton(pkcsService.Object);
                });
                webBuilder.Configure(app =>
                {
                    if (useCertificateForwarding)
                    {
                        app.UseCertificateForwarding();
                    }

                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static X509Certificate2 CreateClientCertificate(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={commonName}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));

        return new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable);
    }
}
