using FluentAssertions;
using RTSec.Kryptonian.Infrastructure.Acme;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Acme;

public class AcmeConnectorConfigTests
{
    [Fact]
    public void DefaultConfig_HasLetsEncryptDirectory()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.DirectoryUrl.Should().Be(WellKnownServers.LetsEncryptV2.ToString());
    }

    [Fact]
    public void DefaultConfig_HasEmptyEmail()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.Email.Should().BeEmpty();
    }

    [Fact]
    public void DefaultConfig_HasNullEabSettings()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.EabKeyId.Should().BeNull();
        config.EabHmacKey.Should().BeNull();
    }

    [Fact]
    public void DefaultConfig_PrefersHttp01Challenge()
    {
        // Arrange & Act
        var config = new AcmeConnectorConfig();

        // Assert
        config.PreferredChallengeType.Should().Be("http-01");
    }

    [Fact]
    public void Config_CanBeConfiguredForZeroSsl()
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
    public void Config_CanBeConfiguredForLetsEncryptStaging()
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
