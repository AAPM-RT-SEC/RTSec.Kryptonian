using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Kryptonian.MedicalDevice.Enrollment;

namespace Kryptonian.MedicalDevice.Enrollment.Tests;

/// <summary>
/// Replays scripted EST responses with no network and no server, recording every request
/// so pending-retry behaviour can be asserted on the wire.
/// </summary>
internal sealed class FakeEstHandler : HttpMessageHandler
{
    private readonly Func<int, HttpResponseMessage> _responder;

    public FakeEstHandler(Func<int, HttpResponseMessage> responder) => _responder = responder;

    public List<string> Bodies { get; } = new();
    public List<string> Paths { get; } = new();
    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        Paths.Add(request.RequestUri!.AbsolutePath);
        Bodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responder(RequestCount);
    }
}

public class EstEnrollmentClientPendingTests
{
    [Fact]
    public async Task PendingThenRealCertificateKeepsKeyAliveAcrossAwait()
    {
        using var issuerKey = RSA.Create(2048);
        var issuerRequest = new CertificateRequest("CN=Test CA", issuerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var issuer = issuerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        FakeEstHandler? handler = null;
        handler = new FakeEstHandler(n =>
        {
            if (n == 1) return WithRetryAfter(HttpStatusCode.Accepted, "0");
            var csr = CertificateRequest.LoadSigningRequest(Convert.FromBase64String(handler!.Bodies.Last()), HashAlgorithmName.SHA256,
                CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions, RSASignaturePadding.Pkcs1);
            csr.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            using var leaf = csr.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            var chain = new X509Certificate2Collection { leaf, issuer };
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Convert.ToBase64String(chain.Export(X509ContentType.Pkcs7)!)) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs7-mime");
            return response;
        });
        using var http = new HttpClient(handler);
        var client = new EstEnrollmentClient(http, delay: async (_, _) => await Task.Yield());
        var result = await client.EnrollAsync(Gateway, Request());
        using var certificate = result.Certificate;
        using var privateKey = certificate.GetRSAPrivateKey()!;
        using var publicKey = certificate.GetRSAPublicKey()!;
        var data = new byte[] { 1, 2, 3 };
        Assert.True(publicKey.VerifyData(data, privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
        foreach (var cert in result.IssuedCertificates) cert.Dispose();
    }

    [Fact]
    public void HttpEndpointCannotReceiveEnrollmentCredentials() =>
        Assert.Throws<ArgumentException>(() => EstEnrollmentClient.BuildEstUri(new Uri("http://localhost"), "simpleenroll"));

    private static readonly Uri Gateway = new("https://localhost:8443");

    private static DeviceEnrollmentRequest Request() =>
        new("pending-test-device", "RTSec", "Model X", "SN-1", "ACTCODE");

    private static HttpResponseMessage WithRetryAfter(HttpStatusCode status, string? value)
    {
        var response = new HttpResponseMessage(status);
        if (value is not null)
        {
            response.Headers.TryAddWithoutValidation("Retry-After", value);
        }
        return response;
    }

    /// <summary>Body is deliberately undecodable: these tests stop at status handling.</summary>
    private static HttpResponseMessage OkPkcs7()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("bm90LXBrY3M3", Encoding.ASCII)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs7-mime");
        return response;
    }

    private static EstEnrollmentClient ClientWith(FakeEstHandler handler, int maxAttempts = 3) =>
        new(new HttpClient(handler), maxAttempts, (_, _) => Task.CompletedTask);

    [Fact]
    public async Task TwoTwentyThenTwoZeroRepeatsIdenticalCsrToSameEndpoint()
    {
        var handler = new FakeEstHandler(n => n == 1
            ? WithRetryAfter(HttpStatusCode.Accepted, "5")
            : OkPkcs7());

        // The stub body cannot decode, so only the retry path up to attempt 2 matters.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            ClientWith(handler).EnrollAsync(Gateway, Request(), CancellationToken.None));

        handler.RequestCount.Should().Be(2);
        handler.Bodies[0].Should().Be(handler.Bodies[1],
            "RFC 7030 requires repeating the identical original request while pending");
        handler.Paths.Should().AllBeEquivalentTo("/.well-known/est/simpleenroll");
    }

    [Fact]
    public async Task RetryAfterHttpDateIsAcceptedAndRescheduled()
    {
        var when = DateTimeOffset.UtcNow.AddSeconds(2).ToString("R");
        TimeSpan? observed = null;
        var handler = new FakeEstHandler(n => n == 1
            ? WithRetryAfter(HttpStatusCode.Accepted, when)
            : OkPkcs7());

        var client = new EstEnrollmentClient(
            new HttpClient(handler), 3,
            (delay, _) => { observed = delay; return Task.CompletedTask; });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.EnrollAsync(Gateway, Request(), CancellationToken.None));

        handler.RequestCount.Should().Be(2);
        observed.Should().NotBeNull();
        observed!.Value.Should().BeGreaterThan(TimeSpan.Zero);
        observed!.Value.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task MissingRetryAfterFailsImmediatelyWithoutTightPolling()
    {
        var handler = new FakeEstHandler(_ => WithRetryAfter(HttpStatusCode.Accepted, null));

        var act = () => ClientWith(handler).EnrollAsync(Gateway, Request(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*without a Retry-After*");
        handler.RequestCount.Should().Be(1,
            "a missing Retry-After must not be polled in a loop");
    }

    [Fact]
    public async Task UnparseableRetryAfterFailsImmediatelyWithoutTightPolling()
    {
        var handler = new FakeEstHandler(_ => WithRetryAfter(HttpStatusCode.Accepted, "not-a-delay"));

        var act = () => ClientWith(handler).EnrollAsync(Gateway, Request(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Retry-After*");
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task PendingForeverStopsAtBoundedMaxAttempts()
    {
        const int maxAttempts = 2;
        var handler = new FakeEstHandler(_ => WithRetryAfter(HttpStatusCode.Accepted, "0"));

        var act = () => ClientWith(handler, maxAttempts)
            .EnrollAsync(Gateway, Request(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*still pending after*");
        handler.RequestCount.Should().Be(maxAttempts + 1);
    }

    [Fact]
    public async Task CancellationDuringPendingWaitStopsWithoutFurtherRequests()
    {
        using var cts = new CancellationTokenSource();
        // Small delay: this test is about cancellation, not the ceiling check.
        var handler = new FakeEstHandler(_ => WithRetryAfter(HttpStatusCode.Accepted, "1"));

        var client = new EstEnrollmentClient(
            new HttpClient(handler),
            maxPendingAttempts: 10,
            delay: (_, token) =>
            {
                cts.Cancel();
                return Task.Delay(TimeSpan.FromSeconds(30), token);
            });

        var act = () => client.EnrollAsync(Gateway, Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task NonPkcs7ContentTypeIsRejectedOnSuccessPath()
    {
        var handler = new FakeEstHandler(_ =>
        {
            var response = OkPkcs7();
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        var act = () => ClientWith(handler).EnrollAsync(Gateway, Request(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*unexpected content type*");
    }

    [Fact]
    public async Task ErrorStatusThrowsWithStatusCodeAndBody()
    {
        var handler = new FakeEstHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("device not active") });

        var act = () => ClientWith(handler).EnrollAsync(Gateway, Request(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*(403)*device not active*");
    }

    [Fact]
    public void DelayBeyondClientCeilingIsRejected()
    {
        var header = RetryConditionHeaderValue.Parse("86400");

        var act = () => EstEnrollmentClient.GetPendingDelay(header, "EST enrollment");

        act.Should().Throw<InvalidOperationException>().WithMessage("*above the*ceiling*");
    }

    [Fact]
    public void PastHttpDateClampsToZeroInsteadOfNegativeDelay()
    {
        var header = RetryConditionHeaderValue.Parse(DateTimeOffset.UtcNow.AddMinutes(-10).ToString("R"));

        EstEnrollmentClient.GetPendingDelay(header, "EST enrollment").Should().Be(TimeSpan.Zero);
    }
}
