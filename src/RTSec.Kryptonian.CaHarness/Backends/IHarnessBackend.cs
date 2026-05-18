using RTSec.Kryptonian.CaHarness.Models;

namespace RTSec.Kryptonian.CaHarness.Backends;

public interface IHarnessBackend
{
    string BackendId { get; }
    void Reset();
    string GetCaCertificatePem();
    IssueResponse Issue(IssueRequest request);
    // EST-enrolled path — embeds the EST OID extension proving enrollment went through a gateway.
    IssueResponse IssueViaEst(string csrPkcs10Base64, string deviceId = "");
    bool Revoke(string serialNumber);
    IReadOnlyList<IssuedCertRecord> GetIssued();
}
