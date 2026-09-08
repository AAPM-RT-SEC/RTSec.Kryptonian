using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.SerialNumber)
            .HasColumnName("serial_number")
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.SubjectDn)
            .HasColumnName("subject_dn")
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(e => e.IssuerDn)
            .HasColumnName("issuer_dn")
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(e => e.Thumbprint)
            .HasColumnName("thumbprint")
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.NotBefore)
            .HasColumnName("not_before");

        builder.Property(e => e.NotAfter)
            .HasColumnName("not_after");

        builder.Property(e => e.CertificatePem)
            .HasColumnName("certificate_pem")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // SQLite drops DateTime.Kind; these instants are always stored in UTC.
        builder.Property(e => e.RevokedAt).HasColumnName("revoked_at")
            .HasConversion(value => value, value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : (DateTime?)null);

        builder.Property(e => e.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.EstProfileId)
            .HasColumnName("est_profile_id")
            .IsRequired();

        builder.Property(e => e.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(255);

        builder.Property(e => e.DeviceRecordId)
            .HasColumnName("device_record_id");

        builder.Property(e => e.CaBackendId)
            .HasColumnName("ca_backend_id");

        builder.Property(e => e.CaBackendType)
            .HasColumnName("ca_backend_type")
            .HasMaxLength(50);

        builder.Property(e => e.CertificateDerBase64)
            .HasColumnName("certificate_der_base64");

        builder.Property(e => e.EncryptedPrivateKeyPem)
            .HasColumnName("encrypted_private_key_pem");

        builder.Property(e => e.GatewayOid)
            .HasColumnName("gateway_oid")
            .HasMaxLength(128);

        builder.Property(e => e.LastExpiryNotifiedAt)
            .HasColumnName("last_expiry_notified_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(e => e.EstProfile)
            .WithMany(p => p.Certificates)
            .HasForeignKey(e => e.EstProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Device)
            .WithMany(d => d.Certificates)
            .HasForeignKey(e => e.DeviceRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.CaBackend)
            .WithMany()
            .HasForeignKey(e => e.CaBackendId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.CaBackendId, e.SerialNumber })
            .HasDatabaseName("ix_certificates_issuer_serial").IsUnique();

        builder.HasIndex(e => e.Thumbprint)
            .IsUnique();

        builder.HasIndex(e => e.NotAfter);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.DeviceId);

        builder.HasIndex(e => e.DeviceRecordId);

        builder.HasIndex(e => e.CaBackendId);
    }
}
