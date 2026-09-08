using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTSec.Kryptonian.Api.Controllers;
using Xunit;

namespace RTSec.Kryptonian.Api.Tests.Controllers;

public class CertificateRevocationControllerAuthorizationTests
{
    [Fact]
    public void RevokeRequiresDeviceAdmin()
    {
        var policy = typeof(CertificatesController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Policy;

        Assert.Equal("DeviceAdmin", policy);
    }

    [Fact]
    public void CrlEndpointIsAnonymousAndUsesCrlRoute()
    {
        Assert.Single(typeof(CrlController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        var route = typeof(CrlController).GetMethod(nameof(CrlController.Get))!
            .GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template;

        Assert.Equal("{issuerFingerprint}.crl", route);
    }
}
