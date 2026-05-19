using Microsoft.Extensions.Options;

namespace RTSec.Kryptonian.Bridge;

public class BridgeWorker : BackgroundService
{
    private readonly BridgeOptions _options;
    private readonly DimseBridgeService _bridgeService;
    private readonly ILogger<BridgeWorker> _logger;

    public BridgeWorker(
        IOptions<BridgeOptions> options,
        DimseBridgeService bridgeService,
        ILogger<BridgeWorker> logger)
    {
        _options = options.Value;
        _bridgeService = bridgeService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Kryptonian bridge configured: DIMSE {Host}:{Port}, TLS {TlsPort}, called AE {CalledAe}, bridge AE {BridgeAe}, listen port {ListenPort}",
            _options.DimseHost,
            _options.DimsePort,
            _options.DimseTlsPort,
            _options.CalledAeTitle,
            _options.BridgeAeTitle,
            _options.BridgeListenPort);

        _logger.LogInformation(
            "Bridge transport contracts are configured. DIMSE C-MOVE/C-STORE execution is isolated here so RTSec.Kryptonian.DimseTlsProxy remains unchanged.");

        var storeScpState = new StoreScpState();
        var storeServer = _bridgeService.StartStoreScp(storeScpState);
        _logger.LogInformation("Local non-TLS C-STORE SCP is listening on port {Port}", storeServer.Port);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            storeServer.Stop();
        }
    }
}
