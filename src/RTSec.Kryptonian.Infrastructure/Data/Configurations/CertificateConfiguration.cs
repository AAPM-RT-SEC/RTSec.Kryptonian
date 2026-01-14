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

        builder.Property(e => e.EstProfileId)
            .HasColumnName("est_profile_id")
            .IsRequired();

        builder.Property(e => e.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(255);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(e => e.EstProfile)
            .WithMany(p => p.Certificates)
            .HasForeignKey(e => e.EstProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SerialNumber)
            .IsUnique();

        builder.HasIndex(e => e.Thumbprint)
            .IsUnique();

        builder.HasIndex(e => e.NotAfter);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.DeviceId);
    }
}
