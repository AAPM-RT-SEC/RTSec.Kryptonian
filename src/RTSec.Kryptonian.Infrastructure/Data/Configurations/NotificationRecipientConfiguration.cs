using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("notification_recipients");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(255);
        builder.Property(e => e.NotifyOnEnrollmentRejected).HasColumnName("notify_on_enrollment_rejected");
        builder.Property(e => e.NotifyOnCertificateNearExpiry).HasColumnName("notify_on_certificate_near_expiry");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Email).IsUnique();
    }
}
