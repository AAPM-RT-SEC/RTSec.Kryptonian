namespace RTSec.Kryptonian.Bridge;

public class BridgeOptions
{
    public string HarnessBaseUrl { get; set; } = "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io";

    public string? TeamToken { get; set; }

    public string DimseHost { get; set; } = "kryptonian-dimse.eastus.cloudapp.azure.com";

    public int DimsePort { get; set; } = 4242;

    public int DimseTlsPort { get; set; } = 4243;

    public string DicomWebBaseUrl { get; set; } = "http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web";

    public string CalledAeTitle { get; set; } = "KRYPTONIAN";

    public string BridgeAeTitle { get; set; } = "KRYPTONIANBRIDGE";

    public int BridgeListenPort { get; set; } = 11112;

    public string? DeviceCertificateDerBase64 { get; set; }
}
