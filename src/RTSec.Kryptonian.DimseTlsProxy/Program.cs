using RTSec.Kryptonian.DimseTlsProxy;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient<CaHarnessClient>();
builder.Services.AddSingleton<ServerCertificateProvider>();
builder.Services.AddHostedService<DimseTlsProxyWorker>();
builder.Services.AddHostedService<CertHttpWorker>();

var host = builder.Build();
host.Run();
