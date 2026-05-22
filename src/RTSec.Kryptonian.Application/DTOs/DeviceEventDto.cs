namespace RTSec.Kryptonian.Application.DTOs;

public class DeviceEventDto
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string? Detail { get; init; }
    public string? IpAddress { get; init; }
    public Guid? CertificateId { get; init; }
}
