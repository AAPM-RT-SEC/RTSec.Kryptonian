namespace RTSec.Kryptonian.Infrastructure.Harness;

public sealed record AdcsScepConnectorConfig
{
    public string BaseUrl { get; init; } = string.Empty;
    public string TemplateName { get; init; } = "DicomDeviceAuthentication";
    public int ValidityDays { get; init; } = 7;
}

public sealed record EjbcaRestConnectorConfig
{
    public string BaseUrl { get; init; } = string.Empty;
    public string CertificateProfile { get; init; } = "MedicalDeviceTLS";
    public string EndEntityProfile { get; init; } = "DicomDevice";
    public string? IssuerDn { get; init; }
    public int ValidityDays { get; init; } = 7;
}
