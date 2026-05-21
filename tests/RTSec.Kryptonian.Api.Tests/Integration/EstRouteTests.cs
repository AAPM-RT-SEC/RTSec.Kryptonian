using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using RTSec.Kryptonian.Api.Controllers;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;

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
    public async Task SimpleReenrollRoutesDoNotAmbiguouslyMatch(string route)
    {
        // Arrange
        using var host = await CreateHostAsync();
        using var client = host.GetTestClient();
        using var content = new ByteArrayContent(Convert.FromBase64String("AQID"));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");

        // Act
        using var response = await client.PostAsync(route, content);

        // Assert: must not be 500 (which is how AmbiguousMatchException surfaces),
        // and must not be 404 (which would mean the URL didn't route at all).
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    private static async Task<IHost> CreateHostAsync()
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
            .ReturnsAsync(profile);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.EstProfiles).Returns(estProfiles.Object);

        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(EstController).Assembly);

                    services.AddSingleton(unitOfWork.Object);
                    services.AddSingleton(Mock.Of<IEnrollmentOrchestrator>());
                    services.AddSingleton(Mock.Of<IPkcsService>());
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }
}
