using System.Security.Cryptography.X509Certificates;

namespace Kryptonian.DICOMTls;

internal sealed record MedicalDeviceNode(
    string Label,
    string CommonName,
    string SerialNumber,
    string AeTitle,
    X509Certificate2 Certificate,
    X509Certificate2Collection IssuedCertificates);
