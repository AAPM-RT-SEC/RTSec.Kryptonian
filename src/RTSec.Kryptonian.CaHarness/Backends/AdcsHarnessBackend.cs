using System.Collections.Concurrent;
using RTSec.Kryptonian.CaHarness.Models;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed class AdcsHarnessBackend : IHarnessBackend
{
    private static readonly HashSet<string> AllowedTemplates =
        new(StringComparer.OrdinalIgnoreCase) { "DicomDeviceAuthentication", "DicomBridgeMtls" };

    private static readonly HashSet<string> PendingTemplates =
        new(StringComparer.OrdinalIgnoreCase) { "PendingApprovalTemplate" };

    private static readonly HashSet<string> RejectedTemplates =
        new(StringComparer.OrdinalIgnoreCase) { "RejectedTemplate" };

    private readonly InMemoryCaEngine _engine = new("ADCS");
    private readonly ConcurrentDictionary<string, (IssueRequest Request, string RequestId)> _pending = new();

    public string BackendId => "adcs";

    public void Reset()
    {
        _engine.Reset();
        _pending.Clear();
    }

    public string GetCaCertificatePem() => _engine.GetCaCertificatePem();

    public IssueResponse Issue(IssueRequest request)
    {
        var template = request.TemplateName;

        if (string.IsNullOrWhiteSpace(template))
            return SelfSignedHarnessBackend.Rejected(BackendId, "TemplateName is required for ADCS backend", "missing-template");

        if (RejectedTemplates.Contains(template))
            return SelfSignedHarnessBackend.Rejected(BackendId,
                $"Template {template} is not permitted on this CA",
                "template-policy-rejected");

        if (!AllowedTemplates.Contains(template) && !PendingTemplates.Contains(template))
            return SelfSignedHarnessBackend.Rejected(BackendId,
                $"Unknown template: {template}",
                "unknown-template");

        if (PendingTemplates.Contains(template))
        {
            var requestId = Guid.NewGuid().ToString("N");
            _pending[requestId] = (request, requestId);
            return new IssueResponse
            {
                Status = "pending",
                Backend = BackendId,
                RequestId = requestId,
                Message = $"Template {template} requires manual administrator approval. Use POST /api/backends/adcs/approve/{requestId} to release."
            };
        }

        return SignRequest(request);
    }

    public IssueResponse? Approve(string requestId)
    {
        if (!_pending.TryRemove(requestId, out var entry))
            return null;

        return SignRequest(entry.Request);
    }

    public bool Revoke(string serialNumber) => _engine.Revoke(serialNumber);

    public IReadOnlyList<IssuedCertRecord> GetIssued() => _engine.GetIssued();

    public IssueResponse IssueViaEst(string csrPkcs10Base64, string deviceId = "")
    {
        byte[] csrDer;
        try
        {
            csrDer = Convert.FromBase64String(csrPkcs10Base64);
        }
        catch
        {
            return SelfSignedHarnessBackend.Rejected(BackendId, "CSR body is not valid base64", "invalid-csr");
        }

        var (record, error) = _engine.Sign(csrDer, 7, deviceId, estEnrolled: true);
        if (record is null)
            return SelfSignedHarnessBackend.Rejected(BackendId, error ?? "Signing failed", "signing-failed");

        return SelfSignedHarnessBackend.Issued(BackendId, record, _engine.GetCaCertificatePem(), _engine.IssuerName);
    }

    private IssueResponse SignRequest(IssueRequest request)
    {
        byte[] csrDer;
        try
        {
            csrDer = Convert.FromBase64String(request.CsrBase64Der);
        }
        catch
        {
            return SelfSignedHarnessBackend.Rejected(BackendId, "csrBase64Der is not valid base64", "invalid-csr");
        }

        var (record, error) = _engine.Sign(csrDer, request.ValidityDays > 0 ? request.ValidityDays : 7, request.DeviceId ?? "");
        if (record is null)
            return SelfSignedHarnessBackend.Rejected(BackendId, error ?? "Signing failed", "signing-failed");

        return SelfSignedHarnessBackend.Issued(BackendId, record, _engine.GetCaCertificatePem(), _engine.IssuerName);
    }
}
