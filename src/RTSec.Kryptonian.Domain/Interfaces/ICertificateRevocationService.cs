using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Domain.Interfaces;

public interface ICertificateRevocationService
{
    Task<CertificateRevocationResult> RevokeAsync(Guid certificateId, RevocationReason reason, CancellationToken ct = default);
    Task<CrlDocumentResult?> GetCrlAsync(string issuerFingerprint, CancellationToken ct = default);
}

public sealed record CertificateRevocationResult(bool Success, int StatusCode, string? ErrorMessage = null)
{
    public static CertificateRevocationResult Failed(int statusCode, string error) => new(false, statusCode, error);
    public static CertificateRevocationResult Successful() => new(true, 200);
}

public sealed record CrlDocumentResult(byte[] Der, DateTime NextUpdate);
