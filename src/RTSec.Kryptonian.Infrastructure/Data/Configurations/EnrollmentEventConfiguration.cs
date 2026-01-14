using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class EnrollmentEventConfiguration : IEntityTypeConfiguration<EnrollmentEvent>
{
    public void Configure(EntityTypeBuilder<EnrollmentEvent> builder)
    {
        builder.ToTable("enrollment_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(e => e.ProfileId)
            .HasColumnName("profile_id")
            .IsRequired();

        builder.Property(e => e.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(255);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.SubjectDn)
            .HasColumnName("subject_dn")
            .HasMaxLength(1024);

        builder.Property(e => e.RequestorIpAddress)
            .HasColumnName("requestor_ip_address")
            .HasMaxLength(45); // IPv6 max length

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2048);

        builder.Property(e => e.IssuedCertificateId)
            .HasColumnName("issued_certificate_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(e => e.Profile)
            .WithMany(p => p.EnrollmentEvents)
            .HasForeignKey(e => e.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.IssuedCertificate)
            .WithMany()
            .HasForeignKey(e => e.IssuedCertificateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.Timestamp);

        builder.HasIndex(e => e.ProfileId);

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.DeviceId);
    }
}
