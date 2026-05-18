using System.Collections.Concurrent;

namespace RTSec.Kryptonian.CaHarness.Backends;

public sealed class TeamBackendRegistry
{
    private readonly string _acmeUpstreamUrl;
    private readonly string? _acmeHarnessBaseUrl;
    // keyed by GUID token (case-insensitive)
    private readonly ConcurrentDictionary<string, TeamEntry> _teams =
        new(StringComparer.OrdinalIgnoreCase);

    public TeamBackendRegistry(string acmeUpstreamUrl, string? acmeHarnessBaseUrl)
    {
        _acmeUpstreamUrl = acmeUpstreamUrl;
        _acmeHarnessBaseUrl = acmeHarnessBaseUrl;
    }

    public (string Token, string TeamName) Register(string teamName)
    {
        var token = Guid.NewGuid().ToString();
        var entry = new TeamEntry(
            token,
            teamName,
            new TeamBackends(
                new SelfSignedHarnessBackend(),
                new AdcsHarnessBackend(),
                new EjbcaHarnessBackend(),
                new AcmeProxyBackend(_acmeUpstreamUrl, _acmeHarnessBaseUrl, $"/teams/{token}")));
        _teams[token] = entry;
        return (token, teamName);
    }

    public TeamEntry? GetByToken(string token) =>
        _teams.TryGetValue(token, out var entry) ? entry : null;

    public IReadOnlyList<string> GetTeamNames() =>
        [.. _teams.Values.Select(e => e.TeamName).Order()];

    public IReadOnlyList<TeamEntry> GetAllTeams() =>
        [.. _teams.Values.OrderBy(e => e.TeamName)];

    public Dictionary<string, int> GetIssuedCounts(string token)
    {
        if (!_teams.TryGetValue(token, out var entry))
            return [];
        return new Dictionary<string, int>
        {
            ["selfsigned"] = entry.Backends.SelfSigned.GetIssued().Count,
            ["adcs"] = entry.Backends.Adcs.GetIssued().Count,
            ["ejbca"] = entry.Backends.Ejbca.GetIssued().Count,
            ["acme"] = entry.Backends.Acme.GetIssued().Count,
        };
    }
}

public sealed class TeamEntry
{
    public string Token { get; }
    public string TeamName { get; }
    public TeamBackends Backends { get; }

    public TeamEntry(string token, string teamName, TeamBackends backends)
    {
        Token = token;
        TeamName = teamName;
        Backends = backends;
    }
}

public sealed class TeamBackends
{
    public SelfSignedHarnessBackend SelfSigned { get; }
    public AdcsHarnessBackend Adcs { get; }
    public EjbcaHarnessBackend Ejbca { get; }
    public AcmeProxyBackend Acme { get; }

    public TeamBackends(
        SelfSignedHarnessBackend selfSigned,
        AdcsHarnessBackend adcs,
        EjbcaHarnessBackend ejbca,
        AcmeProxyBackend acme)
    {
        SelfSigned = selfSigned;
        Adcs = adcs;
        Ejbca = ejbca;
        Acme = acme;
    }

    public IHarnessBackend? ResolveBackend(string backend) =>
        backend.ToLowerInvariant() switch
        {
            "selfsigned" => SelfSigned,
            "adcs" => Adcs,
            "ejbca" => Ejbca,
            _ => null
        };
}
