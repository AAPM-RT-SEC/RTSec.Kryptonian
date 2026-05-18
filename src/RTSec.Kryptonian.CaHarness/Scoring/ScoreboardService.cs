using System.Collections.Concurrent;
using RTSec.Kryptonian.CaHarness.Backends;
using RTSec.Kryptonian.CaHarness.Models;

namespace RTSec.Kryptonian.CaHarness.Scoring;

public sealed class ScoreboardService
{
    private static readonly string[] BackendOrder = ["selfsigned", "adcs", "ejbca", "acme"];

    private readonly TeamBackendRegistry _registry;
    private readonly ConcurrentDictionary<string, List<DicomTransferRecord>> _transfers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _resets = new(StringComparer.OrdinalIgnoreCase);
    // token → set of backend names that completed a DIMSE mTLS C-STORE
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _dimseStoreTls = new(StringComparer.OrdinalIgnoreCase);

    public ScoreboardService(TeamBackendRegistry registry) => _registry = registry;

    public void RecordTransfer(DicomTransferRecord record)
    {
        var list = _transfers.GetOrAdd(record.Token, _ => []);
        lock (list)
            list.Add(record);
    }

    public void RecordDimseStoreTls(string token, string backend)
    {
        var backends = _dimseStoreTls.GetOrAdd(token, _ => new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
        backends[backend] = true;
    }

    public void RecordReset(string token, string backend)
    {
        var counts = _resets.GetOrAdd(token, _ => new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        counts.AddOrUpdate(backend, 1, (_, v) => v + 1);
    }

    public IReadOnlyList<TeamScore> GetScoreboard()
    {
        var teams = _registry.GetAllTeams();
        var scores = new List<TeamScore>(teams.Count);

        foreach (var team in teams)
        {
            var issuedCounts = _registry.GetIssuedCounts(team.Token);
            var transfers = GetTransfersForToken(team.Token);
            var resetCounts = _resets.TryGetValue(team.Token, out var rc) ? rc : null;
            var acmeIssued = team.Backends.Acme.GetIssued();

            var dimseCompletedBackends = _dimseStoreTls.TryGetValue(team.Token, out var db) ? db : null;

            var backends = BackendOrder.Select(backend =>
            {
                var certsIssued = issuedCounts.TryGetValue(backend, out var c) ? c : 0;
                var resets = resetCounts?.TryGetValue(backend, out var r) == true ? r : 0;
                var backendTransfers = transfers.Where(t => t.Backend.Equals(backend, StringComparison.OrdinalIgnoreCase)).ToList();
                var firstDicom = backendTransfers.Count > 0
                    ? backendTransfers.Min(t => t.ReceivedUtc)
                    : (DateTime?)null;
                var usedGateway = backendTransfers.Any(t => t.UsedGateway);
                var dimseStoreTlsComplete = dimseCompletedBackends?.ContainsKey(backend) == true;

                string status;
                if (backend == "acme")
                    status = acmeIssued.Count > 0 ? "dicom_complete" : certsIssued > 0 ? "enrolled" : "not_started";
                else
                    status = firstDicom.HasValue ? "dicom_complete" : certsIssued > 0 ? "enrolled" : "not_started";

                return new BackendScore(backend, status, certsIssued, resets, usedGateway, firstDicom, dimseStoreTlsComplete);
            }).ToList();

            var backendsComplete = backends.Count(b => b.Status == "dicom_complete");
            var firstDicomOverall = transfers.Count > 0 ? transfers.Min(t => t.ReceivedUtc) : (DateTime?)null;

            scores.Add(new TeamScore(team.TeamName, backends, backendsComplete, firstDicomOverall, 0));
        }

        scores.Sort((a, b) =>
        {
            var cmp = b.BackendsComplete.CompareTo(a.BackendsComplete);
            if (cmp != 0) return cmp;
            if (a.FirstDicomUtc.HasValue && b.FirstDicomUtc.HasValue)
                return a.FirstDicomUtc.Value.CompareTo(b.FirstDicomUtc.Value);
            if (a.FirstDicomUtc.HasValue) return -1;
            if (b.FirstDicomUtc.HasValue) return 1;
            return string.Compare(a.TeamName, b.TeamName, StringComparison.OrdinalIgnoreCase);
        });

        return scores.Select((s, i) => s with { OverallRank = i + 1 }).ToList();
    }

    private List<DicomTransferRecord> GetTransfersForToken(string token)
    {
        if (!_transfers.TryGetValue(token, out var list)) return [];
        lock (list)
            return [.. list];
    }
}
