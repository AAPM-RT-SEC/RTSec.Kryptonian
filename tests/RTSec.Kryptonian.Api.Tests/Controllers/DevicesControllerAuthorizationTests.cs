using Microsoft.AspNetCore.Authorization;
using RTSec.Kryptonian.Api.Controllers;
using Xunit;

namespace RTSec.Kryptonian.Api.Tests.Controllers;

public class DevicesControllerAuthorizationTests
{
    [Fact]
    public void DemoEnrollRequiresDeviceAdmin()
    {
        var policy = typeof(DevicesController)
            .GetMethod(nameof(DevicesController.DemoEnroll))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy;

        Assert.Equal("DeviceAdmin", policy);
    }
}
