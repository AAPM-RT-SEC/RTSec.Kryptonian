using RTSec.Kryptonian.CaHarness.Models;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed class SelfSignedHarnessBackend : IHarnessBackend
{
    private readonly InMemoryCaEngine _engine = new("SelfSigned");

    public string BackendId => "selfsigned";

    public void Reset() => _engine.Reset();

    public string GetCaCertificatePem() => _engine.GetCaCertificatePem();

    public IssueResponse Issue(IssueRequest request)
    {
        byte[] csrDer;
        try
        {
            csrDer = Convert.FromBase64String(request.CsrBase64Der);
        }
        catch
        {
            return Rejected(BackendId, "csrBase64Der is not valid base64", "invalid-csr");
        }

        var (record, error) = _engine.Sign(csrDer, request.ValidityDays > 0 ? request.ValidityDays : 7, request.DeviceId ?? "");
        if (record is null)
            return Rejected(BackendId, error ?? "Signing failed", "signing-failed");

        return Issued(BackendId, record, _engine.GetCaCertificatePem(), _engine.IssuerName);
    }

    public IssueResponse IssueViaEst(string csrPkcs10Base64, string deviceId = "")
    {
        byte[] csrDer;
        try
        {
            csrDer = Convert.FromBase64String(csrPkcs10Base64);
        }
        catch
        {
            return Rejected(BackendId, "CSR body is not valid base64", "invalid-csr");
        }

        var (record, error) = _engine.Sign(csrDer, 7, deviceId, enrollmentProtocol: "est");
        if (record is null)
            return Rejected(BackendId, error ?? "Signing failed", "signing-failed");

        return Issued(BackendId, record, _engine.GetCaCertificatePem(), _engine.IssuerName);
    }

    public bool Revoke(string serialNumber) => _engine.Revoke(serialNumber);

    public IReadOnlyList<IssuedCertRecord> GetIssued() => _engine.GetIssued();

    internal static IssueResponse Issued(string backend, IssuedCertRecord r, string caPem, string issuer) =>
        new()
        {
            Status = "issued",
            Backend = backend,
            Issuer = issuer,
            SerialNumber = r.SerialNumber,
            Thumbprint = r.Thumbprint,
            NotBeforeUtc = r.NotBeforeUtc,
            NotAfterUtc = r.NotAfterUtc,
            CertificatePem = r.CertificatePem,
            CertificateDerBase64 = r.CertificateDerBase64,
            CaChainPem = new[] { caPem },
            Message = $"Certificate issued by {backend} harness"
        };

    internal static IssueResponse Rejected(string backend, string message, string reasonCode) =>
        new()
        {
            Status = "rejected",
            Backend = backend,
            Message = message,
            ReasonCode = reasonCode
        };
}
