using System.Security.Cryptography.X509Certificates;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Interface for CA backend connectors.
/// </summary>
public interface ICaConnector
{
    /// <summary>
    /// The type of CA backend this connector handles.
    /// </summary>
    CaBackendType Type { get; }

    /// <summary>
    /// Gets the CA certificate chain.
    /// </summary>
    Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Issues a certificate based on the provided CSR.
    /// </summary>
    Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr,
        Entities.EstProfile profile,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes a certificate by serial number when the backend has a native mutation API.
    /// Local CRL-backed revocation is coordinated through <see cref="GenerateCrlAsync"/>, which receives the full issuer-scoped set.
    /// </summary>
    Task<bool> RevokeCertificateAsync(
        string serial,
        RevocationReason reason,
        CancellationToken ct = default);

    /// <summary>Builds a complete signed CRL for the connector's current issuer.</summary>
    Task<CrlGenerationResult> GenerateCrlAsync(CrlGenerationRequest request, CancellationToken ct = default) =>
        Task.FromResult(CrlGenerationResult.Unsupported());

    /// <summary>
    /// Tests the connection to the CA backend.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
