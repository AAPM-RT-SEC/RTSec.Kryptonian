using RTSec.Kryptonian.CaHarness.Models;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed class EjbcaHarnessBackend : IHarnessBackend
{
    private static readonly HashSet<string> AllowedProfiles =
        new(StringComparer.OrdinalIgnoreCase) { "MedicalDeviceTLS", "DicomWebBridgeMTLS" };

    private static readonly HashSet<string> RejectedProfiles =
        new(StringComparer.OrdinalIgnoreCase) { "RejectedProfile" };

    private readonly InMemoryCaEngine _engine = new("EJBCA");

    public string BackendId => "ejbca";

    public void Reset() => _engine.Reset();

    public string GetCaCertificatePem() => _engine.GetCaCertificatePem();

    public IssueResponse Issue(IssueRequest request)
    {
        var profile = request.TemplateName ?? request.ProfileName;

        if (string.IsNullOrWhiteSpace(profile))
            return SelfSignedHarnessBackend.Rejected(BackendId, "TemplateName or ProfileName is required for EJBCA backend", "missing-profile");

        if (RejectedProfiles.Contains(profile))
            return SelfSignedHarnessBackend.Rejected(BackendId,
                $"Certificate profile {profile} is not permitted on this CA",
                "profile-policy-rejected");

        if (!AllowedProfiles.Contains(profile))
            return SelfSignedHarnessBackend.Rejected(BackendId,
                $"Unknown certificate profile: {profile}",
                "unknown-profile");

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

    public IssueResponse IssueViaEst(string csrPkcs10Base64, string deviceId = "")
    {
        // EJBCA no longer uses EST — return a rejection with a clear message.
        // EJBCA REST API is the enrollment protocol for EJBCA. Use IssueViaRest() instead.
        return SelfSignedHarnessBackend.Rejected(BackendId,
            "EJBCA backend uses the EJBCA REST API, not EST. Use the EJBCA REST endpoint.",
            "protocol-mismatch");
    }

    // Called by the EJBCA REST handler after parsing the PEM CSR.
    public (IssuedCertRecord? Record, string? Error) IssueViaRest(byte[] csrDer, string? profileName, string deviceId = "")
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return (null, "certificate_profile_name is required");
        if (RejectedProfiles.Contains(profileName))
            return (null, $"Certificate profile {profileName} is not permitted on this CA");
        if (!AllowedProfiles.Contains(profileName))
            return (null, $"Unknown certificate profile: {profileName}");

        return _engine.Sign(csrDer, 7, deviceId, enrollmentProtocol: "ejbca-rest");
    }

    public bool Revoke(string serialNumber) => _engine.Revoke(serialNumber);

    public IReadOnlyList<IssuedCertRecord> GetIssued() => _engine.GetIssued();
}
