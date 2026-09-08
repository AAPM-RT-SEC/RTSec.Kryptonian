using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RTSec.Kryptonian.Api.Middleware;

namespace RTSec.Kryptonian.Api.Tests.Middleware;

public class CertificateForwardingGuardTests
{
    [Theory]
    [InlineData(null, "127.0.0.1", true, 403, false)]
    [InlineData("127.0.0.2", "127.0.0.1", true, 403, false)]
    [InlineData("127.0.0.1", "127.0.0.1", true, 200, true)]
    [InlineData("127.0.0.1", "::ffff:127.0.0.1", true, 200, true)]
    [InlineData(null, "127.0.0.1", false, 200, true)]
    public async Task ForwardingRequiresExplicitPeerTrust(
        string? trusted, string remote, bool forwarded, int status, bool expectedCalled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kryptonian:CertificateForwarding:KnownProxies:0"] = trusted
        }.Where(pair => pair.Value != null)).Build();
        var called = false;
        var guard = new CertificateForwardingGuard(_ => { called = true; return Task.CompletedTask; }, config);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (forwarded)
            context.Request.Headers["X-Forwarded-Client-Cert"] = "Cert=\"untrusted-input\"";

        await guard.InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(expectedCalled, called);
    }

    [Fact]
    public async Task MultipleForwardedIdentitiesAreRejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kryptonian:CertificateForwarding:KnownProxies:0"] = "127.0.0.1"
        }).Build();
        var called = false;
        var guard = new CertificateForwardingGuard(_ => { called = true; return Task.CompletedTask; }, config);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers.Append("X-Forwarded-Client-Cert", "Cert=\"first\"");
        context.Request.Headers.Append("X-Forwarded-Client-Cert", "Cert=\"second\"");

        await guard.InvokeAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        Assert.False(called);
    }
}
