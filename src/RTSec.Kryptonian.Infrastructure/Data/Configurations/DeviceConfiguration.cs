using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.DisplayName)
            .HasColumnName("display_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.SubjectCommonName)
            .HasColumnName("subject_common_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Manufacturer)
            .HasColumnName("manufacturer")
            .HasMaxLength(255);

        builder.Property(e => e.Model)
            .HasColumnName("model")
            .HasMaxLength(255);

        builder.Property(e => e.SerialNumber)
            .HasColumnName("serial_number")
            .HasMaxLength(255);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ActivationCodeHash)
            .HasColumnName("activation_code_hash")
            .IsConcurrencyToken()
            .HasMaxLength(64);

        builder.Property(e => e.ActivationCodeExpiresAt)
            .HasColumnName("activation_code_expires_at");

        builder.Property(e => e.ActivationCodeUsedAt)
            .HasColumnName("activation_code_used_at");

        builder.Property(e => e.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(e => e.RemovedAt)
            .HasColumnName("removed_at");

        builder.Property(e => e.LastCertificateId)
            .HasColumnName("last_certificate_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.SubjectCommonName)
            .IsUnique();
    }
}
