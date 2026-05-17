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

    [Fact]
    public void ValidateAcceptsMinimalValidConfig()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "admin@example.com",
            PreferredChallengeType = "http-01"
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateRejectsMissingEmail()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = ""
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void ValidateRejectsUnsupportedChallengeType()
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "admin@example.com",
            PreferredChallengeType = "tls-alpn-01"
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported ACME challenge type*");
    }

    [Theory]
    [InlineData("kid", null)]
    [InlineData(null, "hmac")]
    public void ValidateRejectsPartialEabConfig(string? keyId, string? hmacKey)
    {
        // Arrange
        var config = new AcmeConnectorConfig
        {
            DirectoryUrl = WellKnownServers.LetsEncryptStagingV2.ToString(),
            Email = "admin@example.com",
            EabKeyId = keyId,
            EabHmacKey = hmacKey
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*External Account Binding*");
    }
}
