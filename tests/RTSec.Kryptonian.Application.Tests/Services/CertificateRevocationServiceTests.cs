using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTSec.Kryptonian.Application.Services;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;
using Xunit;

namespace RTSec.Kryptonian.Application.Tests.Services;

public class CertificateRevocationServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICertificateRepository> _certificates = new();
    private readonly Mock<ICaBackendRepository> _backends = new();
    private readonly Mock<IIssuerCrlStateRepository> _crls = new();
    private readonly Mock<ICaConnectorFactory> _factory = new();
    private readonly Mock<ICaConnector> _connector = new();

    public CertificateRevocationServiceTests()
    {
        _uow.SetupGet(x => x.Certificates).Returns(_certificates.Object);
        _uow.SetupGet(x => x.CaBackends).Returns(_backends.Object);
        _uow.SetupGet(x => x.IssuerCrlStates).Returns(_crls.Object);
        _uow.Setup(x => x.TryPersistRevocationStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task RevokeAsyncDoesNotMarkCertificateWhenBackendDoesNotSupportCrls()
    {
        using var issuer = CreateIssuer();
        var backend = new CaBackend { Id = Guid.NewGuid(), IsEnabled = true };
        var certificate = CreateCertificate(backend.Id, issuer);
        _certificates.Setup(x => x.GetByIdAsync(certificate.Id, It.IsAny<CancellationToken>())).ReturnsAsync(certificate);
        _backends.Setup(x => x.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backend);
        _factory.Setup(x => x.CreateConnector(backend)).Returns(_connector.Object);
        _connector.Setup(x => x.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([issuer]);
        _connector.Setup(x => x.GenerateCrlAsync(It.IsAny<CrlGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrlGenerationResult.Unsupported());
        _certificates.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().RevokeAsync(certificate.Id, RevocationReason.KeyCompromise);

        result.StatusCode.Should().Be(501);
        certificate.Status.Should().Be(CertificateStatus.Valid);
        _certificates.Verify(x => x.Update(It.IsAny<Certificate>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAsyncPersistsRevocationOnlyAfterSignedCrlIsReturned()
    {
        using var issuer = CreateIssuer();
        var backend = new CaBackend { Id = Guid.NewGuid(), IsEnabled = true };
        var certificate = CreateCertificate(backend.Id, issuer);
        var fingerprint = Convert.ToHexString(SHA256.HashData(issuer.RawData));
        _certificates.Setup(x => x.GetByIdAsync(certificate.Id, It.IsAny<CancellationToken>())).ReturnsAsync(certificate);
        _backends.Setup(x => x.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backend);
        _factory.Setup(x => x.CreateConnector(backend)).Returns(_connector.Object);
        _connector.Setup(x => x.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([issuer]);
        _certificates.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _crls.Setup(x => x.GetByIssuerFingerprintAsync(fingerprint, It.IsAny<CancellationToken>())).ReturnsAsync((IssuerCrlState?)null);
        _connector.Setup(x => x.GenerateCrlAsync(It.IsAny<CrlGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrlGenerationResult.Successful(fingerprint, [1, 2, 3]));

        var result = await CreateSut().RevokeAsync(certificate.Id, RevocationReason.KeyCompromise);

        result.Success.Should().BeTrue();
        certificate.Status.Should().Be(CertificateStatus.Revoked);
        certificate.RevokedAt.Should().NotBeNull();
        certificate.RevocationReason.Should().Be(RevocationReason.KeyCompromise);
        _crls.Verify(x => x.Add(It.Is<IssuerCrlState>(s => s.CrlNumber == 1 && s.CrlDerBase64 == "AQID")), Times.Once);
        _uow.Verify(x => x.TryPersistRevocationStateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsyncIncludesOnlySerialsFromTheSameBackendAndIssuer()
    {
        using var issuer = CreateIssuer();
        var backend = new CaBackend { Id = Guid.NewGuid(), IsEnabled = true };
        var certificate = CreateCertificate(backend.Id, issuer);
        var fingerprint = Convert.ToHexString(SHA256.HashData(issuer.RawData));
        _certificates.Setup(x => x.GetByIdAsync(certificate.Id, It.IsAny<CancellationToken>())).ReturnsAsync(certificate);
        _backends.Setup(x => x.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backend);
        _factory.Setup(x => x.CreateConnector(backend)).Returns(_connector.Object);
        _connector.Setup(x => x.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([issuer]);
        _certificates.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            RevokedCertificate(backend.Id, issuer.Subject, "0A"),
            RevokedCertificate(Guid.NewGuid(), issuer.Subject, "0B"),
            RevokedCertificate(backend.Id, "CN=Other CA", "0C")]);
        _connector.Setup(x => x.GenerateCrlAsync(It.IsAny<CrlGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrlGenerationResult.Successful(fingerprint, [1]));

        var result = await CreateSut().RevokeAsync(certificate.Id, RevocationReason.KeyCompromise);

        result.Success.Should().BeTrue();
        _connector.Verify(x => x.GenerateCrlAsync(It.Is<CrlGenerationRequest>(r =>
            r.Entries.Select(e => e.SerialNumber).Order().SequenceEqual(new[] { "010203", "0A" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsyncRejectsSameDnCaRotationWithoutCrlStateBeforeSigning()
    {
        using var issuer = CreateIssuer();
        var backend = new CaBackend { Id = Guid.NewGuid(), IsEnabled = true };
        using var oldIssuer = CreateIssuer();
        var certificate = CreateCertificate(backend.Id, oldIssuer);
        _certificates.Setup(x => x.GetByIdAsync(certificate.Id, It.IsAny<CancellationToken>())).ReturnsAsync(certificate);
        _backends.Setup(x => x.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backend);
        _factory.Setup(x => x.CreateConnector(backend)).Returns(_connector.Object);
        _connector.Setup(x => x.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([issuer]);
        var result = await CreateSut().RevokeAsync(certificate.Id, RevocationReason.KeyCompromise);

        result.StatusCode.Should().Be(409);
        _connector.Verify(x => x.GenerateCrlAsync(It.IsAny<CrlGenerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAsyncReturnsConflictWhenCrlNumberChangesConcurrently()
    {
        using var issuer = CreateIssuer();
        var backend = new CaBackend { Id = Guid.NewGuid(), IsEnabled = true };
        var certificate = CreateCertificate(backend.Id, issuer);
        var fingerprint = Convert.ToHexString(SHA256.HashData(issuer.RawData));
        _certificates.Setup(x => x.GetByIdAsync(certificate.Id, It.IsAny<CancellationToken>())).ReturnsAsync(certificate);
        _backends.Setup(x => x.GetByIdAsync(backend.Id, It.IsAny<CancellationToken>())).ReturnsAsync(backend);
        _factory.Setup(x => x.CreateConnector(backend)).Returns(_connector.Object);
        _connector.Setup(x => x.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([issuer]);
        _certificates.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _connector.Setup(x => x.GenerateCrlAsync(It.IsAny<CrlGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrlGenerationResult.Successful(fingerprint, [1]));
        _uow.Setup(x => x.TryPersistRevocationStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().RevokeAsync(certificate.Id, RevocationReason.KeyCompromise);

        result.StatusCode.Should().Be(409);
        result.Success.Should().BeFalse();
        _uow.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCrlAsyncCreatesInitialEmptyCrl()
    {
        using var issuer = CreateIssuer();
        var backend = new CaBackend { Id = Guid.NewGuid(), IsEnabled = true };
        var fingerprint = Convert.ToHexString(SHA256.HashData(issuer.RawData));
        _crls.Setup(x => x.GetByIssuerFingerprintAsync(fingerprint, It.IsAny<CancellationToken>())).ReturnsAsync((IssuerCrlState?)null);
        _backends.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([backend]);
        _factory.Setup(x => x.CreateConnector(backend)).Returns(_connector.Object);
        _connector.Setup(x => x.GetCaCertificatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([issuer]);
        _certificates.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _connector.Setup(x => x.GenerateCrlAsync(It.IsAny<CrlGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrlGenerationResult.Successful(fingerprint, [1, 2, 3]));

        var result = await CreateSut().GetCrlAsync(fingerprint);

        result!.Der.Should().Equal(1, 2, 3);
        _connector.Verify(x => x.GenerateCrlAsync(It.Is<CrlGenerationRequest>(r =>
            r.CrlNumber == 1 && !r.Entries.Any() && r.NextUpdate == r.ThisUpdate.AddSeconds(60)), It.IsAny<CancellationToken>()), Times.Once);
        _crls.Verify(x => x.Add(It.Is<IssuerCrlState>(s => s.CrlNumber == 1)), Times.Once);
    }

    private CertificateRevocationService CreateSut() => new(_uow.Object, _factory.Object, Mock.Of<ILogger<CertificateRevocationService>>());

    private static Certificate CreateCertificate(Guid backendId, X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=device", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var leaf = request.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), [1, 2, 3]);
        return new Certificate
        {
            Id = Guid.NewGuid(), SerialNumber = Convert.ToHexString(leaf.SerialNumberBytes.Span), SubjectDn = leaf.Subject, IssuerDn = leaf.Issuer,
            Thumbprint = leaf.GetCertHashString(), CertificatePem = leaf.ExportCertificatePem(), CertificateDerBase64 = Convert.ToBase64String(leaf.RawData),
            Status = CertificateStatus.Valid, CaBackendId = backendId
        };
    }

    private static Certificate RevokedCertificate(Guid backendId, string issuer, string serial) => new()
    {
        Id = Guid.NewGuid(), SerialNumber = serial, SubjectDn = "CN=other", IssuerDn = issuer, Thumbprint = serial,
        CertificatePem = "pem", Status = CertificateStatus.Revoked, CaBackendId = backendId,
        RevokedAt = DateTime.UtcNow, RevocationReason = RevocationReason.KeyCompromise
    };

    private static X509Certificate2 CreateIssuer()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Local CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }
}
