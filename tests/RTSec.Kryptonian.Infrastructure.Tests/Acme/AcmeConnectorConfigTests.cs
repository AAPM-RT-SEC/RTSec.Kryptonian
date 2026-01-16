using FluentAssertions;
using RTSec.Kryptonian.Infrastructure.Acme;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Acme;

public class AcmeConnectorConfigTests
{
    [Fact]
    public void DefaultConfigHasLetsEncryptDirectory()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.DirectoryUrl.Should().Be(WellKnownServers.LetsEncryptV2.ToString());
    }

    [Fact]
    public void DefaultConfigHasEmptyEmail()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.Email.Should().BeEmpty();
    }

    [Fact]
    public void DefaultConfigHasNullEabSettings()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.EabKeyId.Should().BeNull();
        config.EabHmacKey.Should().BeNull();
    }

    [Fact]
    public void DefaultConfigPrefersHttp01Challenge()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.PreferredChallengeType.Should().Be("http-01");
    }

    [Fact]
    public void ConfigCanBeConfiguredForZeroSsl()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.ZeroSsl.ToString(),
            Email = "test@example.com",
            EabKeyId = "eab-key-id",
            EabHmacKey = "eab-hmac-key"
        };

        // Assert
        config.DirectoryUrl.Should().Contain("zerossl.com");
        config.EabKeyId.Should().NotBeNull();
        config.EabHmacKey.Should().NotBeNull();
    }

    [Fact]
    public void ConfigCanBeConfiguredForLetsEncryptStaging()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "test@example.com"
        };

        // Assert
        config.DirectoryUrl.Should().Contain("staging");
    }
}
