using RTSec.Kryptonian.Bridge;
using FellowOakDicom;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFellowOakDicom();
builder.Services.Configure<BridgeOptions>(builder.Configuration.GetSection("Bridge"));
builder.Services.AddHttpClient<HarnessBridgeClient>();
builder.Services.AddSingleton<DicomWebMultipartParser>();
builder.Services.AddSingleton<DimseBridgeService>();
builder.Services.AddHttpClient<BridgeFlowService>();
builder.Services.AddHostedService<BridgeWorker>();

var host = builder.Build();
host.Run();
