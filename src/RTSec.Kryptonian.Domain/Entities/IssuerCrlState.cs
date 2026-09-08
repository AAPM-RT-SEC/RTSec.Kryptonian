namespace RTSec.Kryptonian.Domain.Entities;

/// <summary>Durable signed CRL state for one CA certificate.</summary>
public class IssuerCrlState : BaseEntity
{
    public Guid CaBackendId { get; set; }
    public string IssuerFingerprint { get; set; } = string.Empty;
    public long CrlNumber { get; set; }
    public DateTime ThisUpdate { get; set; }
    public DateTime NextUpdate { get; set; }
    public string CrlDerBase64 { get; set; } = string.Empty;
}
