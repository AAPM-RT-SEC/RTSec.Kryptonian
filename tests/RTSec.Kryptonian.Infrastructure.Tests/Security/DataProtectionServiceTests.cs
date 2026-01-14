using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Infrastructure.Security;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Security;

public class DataProtectionServiceTests
{
    private readonly DataProtectionService _sut;

    public DataProtectionServiceTests()
    {
        // Create a real data protection provider for testing
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("TestApp");

        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
        var logger = new Mock<ILogger<DataProtectionService>>();

        _sut = new DataProtectionService(provider, logger.Object);
    }

    [Fact]
    public void Protect_ReturnsEncryptedString()
    {
        // Arrange
        var plaintext = "This is a secret";

        // Act
        var ciphertext = _sut.Protect(plaintext);

        // Assert
        ciphertext.Should().NotBe(plaintext);
        ciphertext.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Unprotect_ReturnsOriginalString()
    {
        // Arrange
        var plaintext = "This is a secret";
        var ciphertext = _sut.Protect(plaintext);

        // Act
        var decrypted = _sut.Unprotect(ciphertext);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Protect_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = _sut.Protect(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Unprotect_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = _sut.Unprotect(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Protect_WithLongText_WorksCorrectly()
    {
        // Arrange
        var longText = new string('x', 10000);

        // Act
        var ciphertext = _sut.Protect(longText);
        var decrypted = _sut.Unprotect(ciphertext);

        // Assert
        decrypted.Should().Be(longText);
    }

    [Fact]
    public void Protect_WithSpecialCharacters_WorksCorrectly()
    {
        // Arrange
        var specialText = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAK...\n-----END RSA PRIVATE KEY-----";

        // Act
        var ciphertext = _sut.Protect(specialText);
        var decrypted = _sut.Unprotect(ciphertext);

        // Assert
        decrypted.Should().Be(specialText);
    }

    [Fact]
    public void Unprotect_WithInvalidCiphertext_ThrowsException()
    {
        // Arrange
        var invalidCiphertext = "this-is-not-valid-ciphertext";

        // Act
        var act = () => _sut.Unprotect(invalidCiphertext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Data unprotection failed*");
    }
}
