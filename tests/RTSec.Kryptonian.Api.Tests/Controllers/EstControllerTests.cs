using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Api.Controllers;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using Xunit;

namespace RTSec.Kryptonian.Api.Tests.Controllers;

public class EstControllerTests
{
    private readonly Mock<IEnrollmentOrchestrator> _orchestratorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPkcsService> _pkcsServiceMock;
    private readonly Mock<ILogger<EstController>> _loggerMock;
    private readonly Mock<IEstProfileRepository> _estProfileRepoMock;
    private readonly EstController _sut;

    private const string Pkcs7MimeType = "application/pkcs7-mime; smime-type=certs-only";

    public EstControllerTests()
    {
        _orchestratorMock = new Mock<IEnrollmentOrchestrator>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _pkcsServiceMock = new Mock<IPkcsService>();
        _loggerMock = new Mock<ILogger<EstController>>();
        _estProfileRepoMock = new Mock<IEstProfileRepository>();

        _unitOfWorkMock.Setup(u => u.EstProfiles).Returns(_estProfileRepoMock.Object);

        _sut = new EstController(
            _orchestratorMock.Object,
            _unitOfWorkMock.Object,
            _pkcsServiceMock.Object,
            _loggerMock.Object);

        // Setup default HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #region /cacerts Tests

    [Fact]
    public async Task GetCaCertsWithValidProfileReturns200WithPkcs7()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId);
        var pkcs7Response = new byte[] { 0x30, 0x82, 0x01, 0x00 };
        var encodedResponse = Encoding.ASCII.GetBytes(Convert.ToBase64String(pkcs7Response));

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _orchestratorMock
            .Setup(o => o.GetCaCertsAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pkcs7Response);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(pkcs7Response))
            .Returns(encodedResponse);

        // Act
        var result = await _sut.GetCaCerts(null, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be(Pkcs7MimeType);
        fileResult.FileContents.Should().BeEquivalentTo(encodedResponse);
    }

    [Fact]
    public async Task GetCaCertsWithLabelReturnsCorrectProfile()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId);
        var pkcs7Response = new byte[] { 0x30, 0x82, 0x01, 0x00 };
        var encodedResponse = Encoding.ASCII.GetBytes(Convert.ToBase64String(pkcs7Response));

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est/myprofile", "localhost", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _orchestratorMock
            .Setup(o => o.GetCaCertsAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pkcs7Response);

        _pkcsServiceMock
            .Setup(p => p.EncodeEstResponseBody(pkcs7Response))
            .Returns(encodedResponse);

        // Act
        var result = await _sut.GetCaCerts("myprofile", CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        _estProfileRepoMock.Verify(r => r.GetByPathAndHostnameAsync(
            "/.well-known/est/myprofile", "localhost", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCaCertsWithNoProfileReturns404()
    {
        // Arrange
        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.GetCaCerts(null, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetCaCertsWhenOrchestratorThrowsReturns404()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId);

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _orchestratorMock
            .Setup(o => o.GetCaCertsAsync(profileId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Profile disabled"));

        // Act
        var result = await _sut.GetCaCerts(null, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetCaCertsWithUnknownLabelReturns404WithoutFallingBackToDefaultProfile()
    {
        // Arrange
        var defaultProfileId = Guid.NewGuid();
        var defaultProfile = CreateTestProfile(defaultProfileId);

        // Only the unlabeled default profile exists; the requested label does not.
        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est", "localhost", It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est/gone", "localhost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.GetCaCerts("gone", CancellationToken.None);

        // Assert: a mistyped or revoked label must not silently enroll against the default CA.
        result.Should().BeOfType<NotFoundObjectResult>();
        _orchestratorMock.Verify(
            o => o.GetCaCertsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _estProfileRepoMock.Verify(r => r.GetByPathAndHostnameAsync(
            "/.well-known/est", "localhost", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimpleEnrollWithUnknownLabelReturns404WithoutFallingBackToDefaultProfile()
    {
        // Arrange
        var defaultProfile = CreateTestProfile(Guid.NewGuid(), requireClientCert: false);

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est", "localhost", It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est/typo", "localhost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.SimpleEnroll("typo", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        _orchestratorMock.Verify(
            o => o.EnrollAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task SimpleEnrollWithUnlabeledRequestStillResolvesDefaultPathPrefix()
    {
        // Arrange: the real default path devices use must keep working.
        var profileId = Guid.NewGuid();
        var defaultProfile = CreateTestProfile(profileId, requireClientCert: false);
        var csrBytes = CreateTestCsrBytes();

        _sut.HttpContext.Request.Body = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(csrBytes)));
        _sut.HttpContext.Request.Headers["X-Activation-Code"] = "ACTIVATE123";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync("/.well-known/est", "localhost", It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);

        _orchestratorMock
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(EnrollmentResult.Successful(new byte[] { 0x30 }, Guid.NewGuid()));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        _estProfileRepoMock.Verify(r => r.GetByPathAndHostnameAsync(
            "/.well-known/est", "localhost", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region /simpleenroll Tests

    [Fact]
    public async Task SimpleEnrollWithValidCsrReturns200WithCertificate()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        var csrBytes = CreateTestCsrBytes();
        var pkcs7Response = new byte[] { 0x30, 0x82, 0x01, 0x00 };

        // Setup request body
        var bodyStream = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(csrBytes)));
        _sut.HttpContext.Request.Body = bodyStream;
        _sut.HttpContext.Request.Headers["X-Activation-Code"] = "ACTIVATE123";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);

        _orchestratorMock
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(EnrollmentResult.Successful(pkcs7Response, Guid.NewGuid()));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be(Pkcs7MimeType);
        fileResult.FileContents.Should().BeEquivalentTo(pkcs7Response);
    }

    [Fact]
    public async Task SimpleEnrollWithBasicBootstrapCredentialsPassesUuidAndCodeWithoutPrivateMetadataHeaders()
    {
        var profileId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var activationCode = "ACTIVATE123";
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        var csrBytes = CreateTestCsrBytes();
        _sut.HttpContext.Request.Scheme = "https";
        _sut.HttpContext.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{deviceId:D}:{activationCode}"));

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);
        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);
        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);
        _orchestratorMock
            .Setup(o => o.EnrollAsync(
                profileId,
                csrBytes,
                deviceId.ToString("D"),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                activationCode,
                null,
                null,
                null))
            .ReturnsAsync(EnrollmentResult.Successful(new byte[] { 0x30 }, Guid.NewGuid()));

        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        result.Should().BeOfType<FileContentResult>();
        _orchestratorMock.VerifyAll();
    }

    [Fact]
    public async Task SimpleEnrollWithMalformedBasicCredentialsReturns401BeforeCsrDecode()
    {
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        _sut.HttpContext.Request.Scheme = "https";
        _sut.HttpContext.Request.Headers.Authorization = "Basic not-base64";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);
        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _sut.HttpContext.Response.Headers["WWW-Authenticate"].ToString().Should().Contain("Basic");
        _pkcsServiceMock.Verify(p => p.DecodeEstRequestBodyAsync(
            It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimpleEnrollWithoutActivationCredentialsReturnsBasicChallengeBeforeCsrDecode()
    {
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);
        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _sut.HttpContext.Response.Headers["WWW-Authenticate"].ToString().Should().Contain("Basic");
        _pkcsServiceMock.Verify(p => p.DecodeEstRequestBodyAsync(
            It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimpleEnrollWithBasicCredentialsOverHttpReturns403BeforeCsrDecode()
    {
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        _sut.HttpContext.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{Guid.NewGuid():D}:ACTIVATE123"));

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);
        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _pkcsServiceMock.Verify(p => p.DecodeEstRequestBodyAsync(
            It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimpleEnrollWithOptionalExpiredClientCertificateReturns403BeforeCsrDecode()
    {
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        using var clientCert = CreateClientCertificate(
            "TestDevice",
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));
        _sut.HttpContext.Connection.ClientCertificate = clientCert;

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);
        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _pkcsServiceMock.Verify(p => p.DecodeEstRequestBodyAsync(
            It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _orchestratorMock.Verify(o => o.EnrollAsync(
            It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SimpleEnrollWithInvalidCsrReturns400()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        _sut.HttpContext.Request.Headers["X-Activation-Code"] = "ACTIVATE123";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid CSR format"));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SimpleEnrollWithNoProfileReturns404()
    {
        // Arrange
        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task SimpleEnrollWhenOrchestratorFailsReturnsAppropriateStatusCode()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        var csrBytes = CreateTestCsrBytes();

        var bodyStream = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(csrBytes)));
        _sut.HttpContext.Request.Body = bodyStream;
        _sut.HttpContext.Request.Headers["X-Activation-Code"] = "ACTIVATE123";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);

        _orchestratorMock
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(EnrollmentResult.Failed("CA backend unavailable", 503));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task SimpleEnrollWithOversizedBodyReturns413()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        _sut.HttpContext.Request.Headers["X-Activation-Code"] = "ACTIVATE123";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Request body exceeds maximum allowed size"));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(413);
    }

    [Fact]
    public async Task SimpleEnrollWhenEnrollmentPendingReturns202WithRetryAfter()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);
        var csrBytes = CreateTestCsrBytes();

        var bodyStream = new MemoryStream(Encoding.ASCII.GetBytes(Convert.ToBase64String(csrBytes)));
        _sut.HttpContext.Request.Body = bodyStream;
        _sut.HttpContext.Request.Headers["X-Activation-Code"] = "ACTIVATE123";

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);

        _orchestratorMock
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(EnrollmentResult.Pending(60, Guid.NewGuid()));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(202);
        _sut.HttpContext.Response.Headers["Retry-After"].ToString().Should().Be("60");
    }

    #endregion

    #region /simplereenroll Tests

    [Fact]
    public async Task SimpleReenrollWithoutClientCertReturns401()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId);

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        // Act
        var result = await _sut.SimpleReenroll(null, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(401);
        _pkcsServiceMock.Verify(p => p.DecodeEstRequestBodyAsync(
            It.IsAny<Stream>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimpleReenrollWithNoProfileReturns404()
    {
        // Arrange
        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstProfile?)null);

        // Act
        var result = await _sut.SimpleReenroll(null, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task SimpleReenrollWithInvalidCsrReturns400()
    {
        // Arrange
        using var clientCert = CreateClientCertificate("TestDevice");
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, validateClientCertChain: false);
        _sut.HttpContext.Connection.ClientCertificate = clientCert;

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid CSR format"));

        // Act
        var result = await _sut.SimpleReenroll(null, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SimpleReenrollWithOversizedBodyReturns413()
    {
        // Arrange
        using var clientCert = CreateClientCertificate("TestDevice");
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, validateClientCertChain: false);
        _sut.HttpContext.Connection.ClientCertificate = clientCert;

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Request body exceeds maximum allowed size"));

        // Act
        var result = await _sut.SimpleReenroll(null, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(413);
    }

    [Fact]
    public async Task SimpleReenrollWhenOrchestratorFailsReturnsAppropriateStatusCode()
    {
        // Arrange
        using var clientCert = CreateClientCertificate("TestDevice");
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, validateClientCertChain: false);
        var csrBytes = CreateTestCsrBytes();
        _sut.HttpContext.Connection.ClientCertificate = clientCert;

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);

        _orchestratorMock
            .Setup(o => o.ReenrollAsync(profileId, csrBytes, clientCert, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentResult.Failed("Device is not active", 403));

        // Act
        var result = await _sut.SimpleReenroll(null, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SimpleReenrollWithValidCsrReturnsPkcs7WithBase64TransferEncoding()
    {
        // Arrange
        using var clientCert = CreateClientCertificate("TestDevice");
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, validateClientCertChain: false);
        var csrBytes = CreateTestCsrBytes();
        var pkcs7Response = new byte[] { 0x30, 0x82, 0x01, 0x00 };
        _sut.HttpContext.Connection.ClientCertificate = clientCert;

        _estProfileRepoMock
            .Setup(r => r.GetByPathAndHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _estProfileRepoMock
            .Setup(r => r.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProfile);

        _pkcsServiceMock
            .Setup(p => p.DecodeEstRequestBodyAsync(It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csrBytes);

        _orchestratorMock
            .Setup(o => o.ReenrollAsync(profileId, csrBytes, clientCert, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentResult.Successful(pkcs7Response, Guid.NewGuid()));

        // Act
        var result = await _sut.SimpleReenroll(null, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be(Pkcs7MimeType);
        fileResult.FileContents.Should().BeEquivalentTo(pkcs7Response);
        _sut.HttpContext.Response.Headers["Content-Transfer-Encoding"].ToString().Should().Be("base64");
    }

    #endregion

    #region Helper Methods

    private static EstProfile CreateTestProfile(
        Guid id,
        bool requireClientCert = true,
        bool isEnabled = true,
        bool validateClientCertChain = false,
        List<string>? trustedThumbprints = null)
    {
        var profile = new EstProfile
        {
            Id = id,
            Name = "Test Profile",
            PathPrefix = "/.well-known/est",
            HostnameMatchType = HostnameMatchType.Exact,
            CaBackendId = Guid.NewGuid(),
            ValidityDays = 365,
            RequireClientCertificate = requireClientCert,
            ValidateClientCertificateChain = validateClientCertChain,
            IsEnabled = isEnabled
        };
        profile.Hostnames.Clear();
        profile.Hostnames.Add("localhost");
        profile.TrustedClientCaThumbprints.Clear();
        if (trustedThumbprints != null)
        {
            foreach (var thumbprint in trustedThumbprints)
            {
                profile.TrustedClientCaThumbprints.Add(thumbprint);
            }
        }
        return profile;
    }

    private static byte[] CreateTestCsrBytes()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=TestDevice"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSigningRequest();
    }

    private static X509Certificate2 CreateClientCertificate(
        string commonName,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={commonName}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-5),
            notAfter ?? DateTimeOffset.UtcNow.AddYears(1));

        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "test"),
            "test",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    #endregion
}
