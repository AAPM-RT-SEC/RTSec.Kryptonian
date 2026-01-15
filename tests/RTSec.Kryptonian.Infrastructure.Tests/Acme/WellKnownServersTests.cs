using FluentAssertions;
using RTSec.Kryptonian.Infrastructure.Acme;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Acme;

public class WellKnownServersTests
{
    [Fact]
    public void LetsEncryptV2HasCorrectUrl()
    {
        WellKnownServers.LetsEncryptV2.ToString()
            .Should().Be("https://acme-v02.api.letsencrypt.org/directory");
    }

    [Fact]
    public void LetsEncryptStagingV2HasCorrectUrl()
    {
        WellKnownServers.LetsEncryptStagingV2.ToString()
            .Should().Be("https://acme-staging-v02.api.letsencrypt.org/directory");
    }

    [Fact]
    public void ZeroSslHasCorrectUrl()
    {
        WellKnownServers.ZeroSsl.ToString()
            .Should().Be("https://acme.zerossl.com/v2/DV90");
    }

    [Fact]
    public void BuyPassHasCorrectUrl()
    {
        WellKnownServers.BuyPass.ToString()
            .Should().Be("https://api.buypass.com/acme/directory");
    }

    [Fact]
    public void BuyPassTestHasCorrectUrl()
    {
        WellKnownServers.BuyPassTest.ToString()
            .Should().Be("https://api.test4.buypass.no/acme/directory");
    }
}
