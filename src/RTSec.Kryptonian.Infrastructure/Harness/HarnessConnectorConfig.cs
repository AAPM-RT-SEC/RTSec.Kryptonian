namespace RTSec.Kryptonian.Infrastructure.Harness;

public sealed record AdcsHarnessConnectorConfig
{
    public string HarnessBaseUrl { get; init; } = string.Empty;
    public string TemplateName { get; init; } = "DicomDeviceAuthentication";
    public int ValidityDays { get; init; } = 7;
}

public sealed record EjbcaHarnessConnectorConfig
{
    public string HarnessBaseUrl { get; init; } = string.Empty;
    public string CertificateProfile { get; init; } = "MedicalDeviceTLS";
    public string EndEntityProfile { get; init; } = "DicomDevice";
    public int ValidityDays { get; init; } = 7;
}
