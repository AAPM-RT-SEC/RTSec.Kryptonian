using RTSec.Kryptonian.DimseTlsProxy;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Configuration.GetValue<bool>("Proxy:HarnessMode"))
    builder.Services.AddHttpClient<CaHarnessClient>();
builder.Services.AddSingleton<ServerCertificateProvider>();
builder.Services.AddHostedService<DimseTlsProxyWorker>();
if (builder.Configuration.GetValue<bool>("Proxy:EnableCertificateDownload"))
    builder.Services.AddHostedService<CertHttpWorker>();

var host = builder.Build();
host.Run();
