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
    // token → completed Flow 2 (C-MOVE → DICOMWeb STOW)
    private readonly ConcurrentDictionary<string, bool> _cmoveComplete = new(StringComparer.OrdinalIgnoreCase);
    // token → completed Flow 3 (DICOMWeb → DIMSE)
    private readonly ConcurrentDictionary<string, bool> _dicomWebToDimseComplete = new(StringComparer.OrdinalIgnoreCase);
    // token → manual UI demo scores (set by admin)
    private readonly ConcurrentDictionary<string, ManualScore> _manualScores = new(StringComparer.OrdinalIgnoreCase);

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

    public void SetManualScore(string token, bool deviceRegistration, bool pendingStatus, bool deviceRemoval) =>
        _manualScores[token] = new ManualScore(deviceRegistration, pendingStatus, deviceRemoval);

    public void RecordCmoveStow(string token) => _cmoveComplete[token] = true;

    public void RecordDicomWebToDimse(string token) => _dicomWebToDimseComplete[token] = true;

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

            var backendsComplete = backends.Count(b => b.DimseStoreTlsComplete);
            var firstDicomOverall = transfers.Count > 0 ? transfers.Min(t => t.ReceivedUtc) : (DateTime?)null;
            var cmoveComplete = _cmoveComplete.ContainsKey(team.Token);
            var dicomWebToDimseComplete = _dicomWebToDimseComplete.ContainsKey(team.Token);
            var manual = _manualScores.TryGetValue(team.Token, out var ms) ? ms : new ManualScore(false, false, false);

            scores.Add(new TeamScore(team.TeamName, backends, backendsComplete, firstDicomOverall, 0, cmoveComplete, dicomWebToDimseComplete, manual));
        }

        scores.Sort((a, b) =>
        {
            var scoreA = a.BackendsComplete + (a.CmoveComplete ? 1 : 0) + (a.DicomWebToDimseComplete ? 1 : 0) + a.Manual.Total;
            var scoreB = b.BackendsComplete + (b.CmoveComplete ? 1 : 0) + (b.DicomWebToDimseComplete ? 1 : 0) + b.Manual.Total;
            var cmp = scoreB.CompareTo(scoreA);
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
