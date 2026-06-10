namespace RTSec.Kryptonian.Infrastructure.Harness;

public sealed record AdcsScepConnectorConfig
{
    public string BaseUrl { get; init; } = string.Empty;
    public string TemplateName { get; init; } = "DicomDeviceAuthentication";
    public int ValidityDays { get; init; } = 7;

    // Enrollment agent certificate for authenticating to NDES (mTLS).
    // Required when NDES EnforcePassword=1; omit when NDES is in no-challenge mode.
    // PFX takes priority; thumbprint looks up the certificate from the machine store.
    public string? EnrollmentAgentPfxPath { get; init; }
    public string? EnrollmentAgentPfxPassword { get; init; }
    public string? EnrollmentAgentThumbprint { get; init; }
}

public sealed record EjbcaRestConnectorConfig
{
    public string BaseUrl { get; init; } = string.Empty;
    public string CertificateProfile { get; init; } = "MedicalDeviceTLS";
    public string EndEntityProfile { get; init; } = "DicomDevice";
    public string? IssuerDn { get; init; }
    public int ValidityDays { get; init; } = 7;
}
