using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.ToTable("notification_settings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Enabled).HasColumnName("enabled");
        builder.Property(e => e.SmtpHost).HasColumnName("smtp_host").HasMaxLength(255).IsRequired();
        builder.Property(e => e.SmtpPort).HasColumnName("smtp_port");
        builder.Property(e => e.TlsMode)
            .HasColumnName("tls_mode")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(e => e.AuthMode)
            .HasColumnName("auth_mode")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(e => e.Username).HasColumnName("username").HasMaxLength(255);
        builder.Property(e => e.EncryptedPassword).HasColumnName("encrypted_password");
        builder.Property(e => e.FromAddress).HasColumnName("from_address").HasMaxLength(255).IsRequired();
        builder.Property(e => e.FromDisplayName).HasColumnName("from_display_name").HasMaxLength(255);
        builder.Property(e => e.TrustServerCertificate).HasColumnName("trust_server_certificate");
        builder.Property(e => e.NotifyOnEnrollmentRejected).HasColumnName("notify_on_enrollment_rejected");
        builder.Property(e => e.NotifyOnCertificateNearExpiry).HasColumnName("notify_on_certificate_near_expiry");
        builder.Property(e => e.ExpiryWarningDays).HasColumnName("expiry_warning_days");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
