using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class IssuerCrlStateConfiguration : IEntityTypeConfiguration<IssuerCrlState>
{
    public void Configure(EntityTypeBuilder<IssuerCrlState> builder)
    {
        builder.ToTable("issuer_crl_states");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CaBackendId).HasColumnName("ca_backend_id").IsRequired();
        builder.Property(e => e.IssuerFingerprint).HasColumnName("issuer_fingerprint").HasMaxLength(128).IsRequired();
        builder.Property(e => e.CrlNumber).HasColumnName("crl_number").IsRequired().IsConcurrencyToken();
        builder.Property(e => e.ThisUpdate).HasColumnName("this_update").IsRequired();
        builder.Property(e => e.NextUpdate).HasColumnName("next_update").IsRequired();
        builder.Property(e => e.CrlDerBase64).HasColumnName("crl_der_base64").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(e => e.IssuerFingerprint).IsUnique();
        builder.HasIndex(e => e.CaBackendId).IsUnique();
    }
}
