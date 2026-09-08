using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;
using RTSec.Kryptonian.Domain.ValueObjects;

namespace RTSec.Kryptonian.Application.Services;

public class CertificateRevocationService(
    IUnitOfWork unitOfWork,
    ICaConnectorFactory connectorFactory,
    ILogger<CertificateRevocationService> logger) : ICertificateRevocationService
{
    // Prototype cadence: peers can refresh an empty CRL within a practical demo window.
    private static readonly TimeSpan PrototypeCrlLifetime = TimeSpan.FromSeconds(60);

    public async Task<CertificateRevocationResult> RevokeAsync(Guid certificateId, RevocationReason reason, CancellationToken ct = default)
    {
        var certificate = await unitOfWork.Certificates.GetByIdAsync(certificateId, ct);
        if (certificate == null) return CertificateRevocationResult.Failed(404, "Certificate not found");
        if (certificate.Status == CertificateStatus.Revoked) return CertificateRevocationResult.Failed(409, "Certificate is already revoked");
        if (!certificate.CaBackendId.HasValue) return CertificateRevocationResult.Failed(501, "Certificate authority does not support revocation");

        var backend = await unitOfWork.CaBackends.GetByIdAsync(certificate.CaBackendId.Value, ct);
        if (backend == null || !backend.IsEnabled) return CertificateRevocationResult.Failed(503, "Certificate authority is unavailable");

        ICaConnector connector;
        try { connector = connectorFactory.CreateConnector(backend); }
        catch (NotSupportedException) { return CertificateRevocationResult.Failed(501, "Certificate authority does not support revocation"); }

        var issuers = await connector.GetCaCertificatesAsync(ct);
        var issuer = issuers.FirstOrDefault();
        if (issuer == null || !string.Equals(issuer.Subject, certificate.IssuerDn, StringComparison.OrdinalIgnoreCase))
            return CertificateRevocationResult.Failed(409, "Certificate issuer does not match the configured CA");
        if (!IsLeafIssuedBy(certificate, issuer))
            return CertificateRevocationResult.Failed(409, "Stored certificate does not chain to the configured CA certificate");

        var fingerprint = Convert.ToHexString(SHA256.HashData(issuer.RawData));
        var state = await unitOfWork.IssuerCrlStates.GetByIssuerFingerprintAsync(fingerprint, ct);
        if (state != null && state.CaBackendId != backend.Id)
            return CertificateRevocationResult.Failed(409, "Issuer fingerprint belongs to a different CA backend");
        var backendState = await unitOfWork.IssuerCrlStates.GetByCaBackendIdAsync(backend.Id, ct);
        if (backendState != null && !string.Equals(backendState.IssuerFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            return CertificateRevocationResult.Failed(409, "Configured CA certificate has rotated; create a new CA backend before revoking certificates");

        var now = DateTime.UtcNow;
        var nextUpdate = GetNextUpdate(issuer, now);
        if (nextUpdate <= now) return CertificateRevocationResult.Failed(503, "Certificate authority is expired");
        var entries = await GetRevokedEntriesAsync(backend.Id, issuer.Subject, ct);
        entries.Add(new CrlEntry(certificate.SerialNumber, now, reason));
        var generated = await connector.GenerateCrlAsync(new CrlGenerationRequest(
            fingerprint,
            (state?.CrlNumber ?? 0) + 1,
            now,
            nextUpdate,
            entries), ct);
        if (!generated.Supported || generated.Der == null ||
            !string.Equals(generated.IssuerFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            return CertificateRevocationResult.Failed(501, generated.ErrorMessage ?? "Certificate authority does not support revocation");

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            certificate.Status = CertificateStatus.Revoked;
            certificate.RevokedAt = now;
            certificate.RevocationReason = reason;
            unitOfWork.Certificates.Update(certificate);

            state ??= new IssuerCrlState { Id = Guid.NewGuid(), CaBackendId = backend.Id, IssuerFingerprint = fingerprint };
            state.CrlNumber++;
            state.ThisUpdate = now;
            state.NextUpdate = nextUpdate;
            state.CrlDerBase64 = Convert.ToBase64String(generated.Der);
            if (state.CreatedAt == default) unitOfWork.IssuerCrlStates.Add(state);
            else unitOfWork.IssuerCrlStates.Update(state);

            if (!await unitOfWork.TryPersistRevocationStateAsync(ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return CertificateRevocationResult.Failed(409, "CRL changed concurrently; retry revocation");
            }
            await unitOfWork.CommitTransactionAsync(ct);
            return CertificateRevocationResult.Successful();
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            logger.LogError(ex, "Failed to persist certificate revocation {CertificateId}", certificateId);
            return CertificateRevocationResult.Failed(500, "Failed to persist certificate revocation");
        }
    }

    public async Task<CrlDocumentResult?> GetCrlAsync(string issuerFingerprint, CancellationToken ct = default)
    {
        var state = await unitOfWork.IssuerCrlStates.GetByIssuerFingerprintAsync(issuerFingerprint, ct);
        if (state?.NextUpdate > DateTime.UtcNow)
        {
            try { return new CrlDocumentResult(Convert.FromBase64String(state.CrlDerBase64), state.NextUpdate); }
            catch (FormatException) { return null; }
        }

        CaBackend? backend = state == null ? null : await unitOfWork.CaBackends.GetByIdAsync(state.CaBackendId, ct);
        ICaConnector? connector = null;
        X509Certificate2? issuer = null;
        if (backend != null && backend.IsEnabled)
        {
            try
            {
                connector = connectorFactory.CreateConnector(backend);
                issuer = (await connector.GetCaCertificatesAsync(ct)).FirstOrDefault();
            }
            catch (NotSupportedException) { return null; }
        }
        else if (state == null)
        {
            foreach (var candidate in await unitOfWork.CaBackends.GetAllAsync(ct))
            {
                if (!candidate.IsEnabled) continue;
                try
                {
                    var candidateConnector = connectorFactory.CreateConnector(candidate);
                    var candidateIssuer = (await candidateConnector.GetCaCertificatesAsync(ct)).FirstOrDefault();
                    if (candidateIssuer != null && string.Equals(Convert.ToHexString(SHA256.HashData(candidateIssuer.RawData)), issuerFingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        backend = candidate;
                        connector = candidateConnector;
                        issuer = candidateIssuer;
                        break;
                    }
                }
                catch (NotSupportedException) { }
            }
        }

        if (backend == null || connector == null || issuer == null ||
            !string.Equals(Convert.ToHexString(SHA256.HashData(issuer.RawData)), issuerFingerprint, StringComparison.OrdinalIgnoreCase))
            return null;

        var backendState = await unitOfWork.IssuerCrlStates.GetByCaBackendIdAsync(backend.Id, ct);
        if (backendState != null && !string.Equals(backendState.IssuerFingerprint, issuerFingerprint, StringComparison.OrdinalIgnoreCase)) return null;

        var now = DateTime.UtcNow;
        var nextUpdate = GetNextUpdate(issuer, now);
        if (nextUpdate <= now) return null;
        var generated = await connector.GenerateCrlAsync(new CrlGenerationRequest(
            issuerFingerprint, (state?.CrlNumber ?? 0) + 1, now, nextUpdate,
            await GetRevokedEntriesAsync(backend.Id, issuer.Subject, ct)), ct);
        if (!generated.Supported || generated.Der == null ||
            !string.Equals(generated.IssuerFingerprint, issuerFingerprint, StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            state ??= new IssuerCrlState { Id = Guid.NewGuid(), CaBackendId = backend.Id, IssuerFingerprint = issuerFingerprint };
            state.CrlNumber++;
            state.ThisUpdate = now;
            state.NextUpdate = nextUpdate;
            state.CrlDerBase64 = Convert.ToBase64String(generated.Der);
            if (state.CreatedAt == default) unitOfWork.IssuerCrlStates.Add(state);
            else unitOfWork.IssuerCrlStates.Update(state);
            if (!await unitOfWork.TryPersistRevocationStateAsync(ct))
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                return null;
            }
            await unitOfWork.CommitTransactionAsync(ct);
            return new CrlDocumentResult(generated.Der, state.NextUpdate);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            logger.LogError(ex, "Failed to persist CRL for issuer {IssuerFingerprint}", issuerFingerprint);
            return null;
        }
    }

    private async Task<List<CrlEntry>> GetRevokedEntriesAsync(
        Guid backendId,
        string issuerDn,
        CancellationToken ct)
    {
        var entries = (await unitOfWork.Certificates.GetAllAsync(ct))
            .Where(c => c.CaBackendId == backendId && c.Status == CertificateStatus.Revoked &&
                        string.Equals(c.IssuerDn, issuerDn, StringComparison.OrdinalIgnoreCase) && c.RevokedAt.HasValue)
            .Select(c => new CrlEntry(c.SerialNumber, c.RevokedAt!.Value, c.RevocationReason ?? RevocationReason.Unspecified))
            .ToList();
        return entries;
    }

    private static bool IsLeafIssuedBy(Certificate certificate, X509Certificate2 issuer)
    {
        try
        {
            using var leaf = string.IsNullOrWhiteSpace(certificate.CertificateDerBase64)
                ? X509Certificate2.CreateFromPem(certificate.CertificatePem)
                : new X509Certificate2(Convert.FromBase64String(certificate.CertificateDerBase64));
            if (!string.Equals(leaf.Issuer, issuer.Subject, StringComparison.OrdinalIgnoreCase)) return false;

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(issuer);
            return chain.Build(leaf) && chain.ChainElements.Count > 1 &&
                CryptographicOperations.FixedTimeEquals(chain.ChainElements[^1].Certificate.RawData, issuer.RawData);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }

    private static DateTime GetNextUpdate(X509Certificate2 issuer, DateTime thisUpdate) =>
        DateTime.Compare(thisUpdate.Add(PrototypeCrlLifetime), issuer.NotAfter.ToUniversalTime()) < 0
            ? thisUpdate.Add(PrototypeCrlLifetime)
            : issuer.NotAfter.ToUniversalTime();
}
