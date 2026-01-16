using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Infrastructure.Acme;
using Xunit;

namespace RTSec.Kryptonian.Infrastructure.Tests.Acme;

public class Http01ChallengeStoreTests
{
    private readonly Mock<ILogger<Http01ChallengeStore>> _loggerMock;
    private readonly Http01ChallengeStore _sut;

    public Http01ChallengeStoreTests()
    {
        _loggerMock = new Mock<ILogger<Http01ChallengeStore>>();
        _sut = new Http01ChallengeStore(_loggerMock.Object);
    }

    [Fact]
    public void ChallengeTypeReturnsHttp01()
    {
        // Act & Assert
        _sut.ChallengeType.Should().Be("http-01");
    }

    [Fact]
    public async Task PrepareAsyncStoresChallenge()
    {
        // Arrange
        var domain = "example.com";
        var token = "test-token-123";
        var keyAuth = "test-key-authorization";

        // Act
        await _sut.PrepareAsync(domain, token, keyAuth);

        // Assert
        var result = _sut.GetKeyAuthorization(token);
        result.Should().Be(keyAuth);
    }

    [Fact]
    public void GetKeyAuthorizationReturnsNullWhenTokenNotFound()
    {
        // Act
        var result = _sut.GetKeyAuthorization("nonexistent-token");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CleanupAsyncRemovesChallenge()
    {
        // Arrange
        var domain = "example.com";
        var token = "test-token-456";
        var keyAuth = "test-key-authorization";
        await _sut.PrepareAsync(domain, token, keyAuth);

        // Act
        await _sut.CleanupAsync(domain, token);

        // Assert
        var result = _sut.GetKeyAuthorization(token);
        result.Should().BeNull();
    }

    [Fact]
    public async Task PrepareAsyncOverwritesExistingChallenge()
    {
        // Arrange
        var domain = "example.com";
        var token = "test-token-789";
        var keyAuth1 = "key-auth-1";
        var keyAuth2 = "key-auth-2";

        // Act
        await _sut.PrepareAsync(domain, token, keyAuth1);
        await _sut.PrepareAsync(domain, token, keyAuth2);

        // Assert
        var result = _sut.GetKeyAuthorization(token);
        result.Should().Be(keyAuth2);
    }

    [Fact]
    public async Task MultipleTokensAreStoredIndependently()
    {
        // Arrange
        var domain = "example.com";
        var token1 = "token-1";
        var token2 = "token-2";
        var keyAuth1 = "key-auth-1";
        var keyAuth2 = "key-auth-2";

        // Act
        await _sut.PrepareAsync(domain, token1, keyAuth1);
        await _sut.PrepareAsync(domain, token2, keyAuth2);

        // Assert
        _sut.GetKeyAuthorization(token1).Should().Be(keyAuth1);
        _sut.GetKeyAuthorization(token2).Should().Be(keyAuth2);
    }

    [Fact]
    public async Task CleanupAsyncRemovesOnlySpecificToken()
    {
        // Arrange
        var domain = "example.com";
        var token1 = "token-keep";
        var token2 = "token-remove";
        var keyAuth1 = "key-auth-1";
        var keyAuth2 = "key-auth-2";

        await _sut.PrepareAsync(domain, token1, keyAuth1);
        await _sut.PrepareAsync(domain, token2, keyAuth2);

        // Act
        await _sut.CleanupAsync(domain, token2);

        // Assert
        _sut.GetKeyAuthorization(token1).Should().Be(keyAuth1);
        _sut.GetKeyAuthorization(token2).Should().BeNull();
    }

    [Fact]
    public async Task CleanupAsyncDoesNotThrowWhenTokenNotFound()
    {
        // Act
        var act = () => _sut.CleanupAsync("example.com", "nonexistent-token");

        // Assert
        await act.Should().NotThrowAsync();
    }
}
