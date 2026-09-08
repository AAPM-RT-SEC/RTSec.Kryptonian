using System.Net;

namespace RTSec.Kryptonian.Api.Middleware;

/// <summary>Only explicitly trusted ingress peers may supply client TLS identity.</summary>
public sealed class CertificateForwardingGuard
{
    private readonly RequestDelegate _next;
    private readonly HashSet<IPAddress> _proxies;

    public CertificateForwardingGuard(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _proxies = (configuration.GetSection("Kryptonian:CertificateForwarding:KnownProxies")
            .Get<string[]>() ?? Array.Empty<string>())
            .Select(value => IPAddress.Parse(value).MapToIPv6()).ToHashSet();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (headers.ContainsKey("X-Forwarded-Client-Cert"))
        {
            var remote = context.Connection.RemoteIpAddress;
            if (remote == null || !_proxies.Contains(remote.MapToIPv6())
                || context.Connection.ClientCertificate != null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // The trusted ingress must replace incoming forwarding headers and verify TLS
            // possession. It must not append an untrusted client's supplied identity.
            if (headers["X-Forwarded-Client-Cert"].Count != 1)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
        }

        await _next(context);
    }
}
