namespace RTSec.Kryptonian.Domain.Entities;

public class HackathonSettings : BaseEntity
{
    public string HarnessBaseUrl { get; set; } = string.Empty;
    public string TeamToken { get; set; } = string.Empty;
    public string DimseHost { get; set; } = "kryptonian-dimse.eastus.cloudapp.azure.com";
    public int DimseTlsPort { get; set; } = 4243;
    public int OrthancDimsePort { get; set; } = 4242;
    public string DicomWebBaseUrl { get; set; } = "http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web";
    public string CalledAeTitle { get; set; } = "KRYPTONIAN";
    public string BridgeAeTitle { get; set; } = "KRYPTONIANBRIDGE";
    public int BridgeListenPort { get; set; } = 11112;
    public string? TrustedProxyCertificateThumbprint { get; set; }
}
