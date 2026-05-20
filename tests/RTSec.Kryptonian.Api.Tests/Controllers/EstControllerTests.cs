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
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(EnrollmentResult.Successful(pkcs7Response, Guid.NewGuid()));

        // Act
        var result = await _sut.SimpleEnroll(null, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be(Pkcs7MimeType);
        fileResult.FileContents.Should().BeEquivalentTo(pkcs7Response);
    }

    [Fact]
    public async Task SimpleEnrollWithInvalidCsrReturns400()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var testProfile = CreateTestProfile(profileId, requireClientCert: false);

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
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
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
            .Setup(o => o.EnrollAsync(profileId, csrBytes, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
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

    #endregion
}

