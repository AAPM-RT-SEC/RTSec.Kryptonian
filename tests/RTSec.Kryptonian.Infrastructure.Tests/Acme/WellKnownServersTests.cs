using FluentAssertions;
using RTSec.Kryptonian.Infrastructure.Acme;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Acme;

public class WellKnownServersTests
{
    [Fact]
    public void LetsEncryptV2_HasCorrectUrl()
    {
        WellKnownServers.LetsEncryptV2.ToString()
            .Should().Be("https://acme-v02.api.letsencrypt.org/directory");
    }

    [Fact]
    public void LetsEncryptStagingV2_HasCorrectUrl()
    {
        WellKnownServers.LetsEncryptStagingV2.ToString()
            .Should().Be("https://acme-staging-v02.api.letsencrypt.org/directory");
    }

    [Fact]
    public void ZeroSsl_HasCorrectUrl()
    {
        WellKnownServers.ZeroSsl.ToString()
            .Should().Be("https://acme.zerossl.com/v2/DV90");
    }

    [Fact]
    public void BuyPass_HasCorrectUrl()
    {
        WellKnownServers.BuyPass.ToString()
            .Should().Be("https://api.buypass.com/acme/directory");
    }

    [Fact]
    public void BuyPassTest_HasCorrectUrl()
    {
        WellKnownServers.BuyPassTest.ToString()
            .Should().Be("https://api.test4.buypass.no/acme/directory");
    }
}
