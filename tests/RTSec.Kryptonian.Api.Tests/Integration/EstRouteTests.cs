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

    private static async Task<IHost> CreateHostAsync(bool hasProfile = true)
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
