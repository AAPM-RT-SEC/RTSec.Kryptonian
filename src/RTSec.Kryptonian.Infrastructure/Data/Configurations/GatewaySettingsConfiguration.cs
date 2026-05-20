using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class GatewaySettingsConfiguration : IEntityTypeConfiguration<GatewaySettings>
{
    public void Configure(EntityTypeBuilder<GatewaySettings> builder)
    {
        builder.ToTable("gateway_settings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.DefaultCertificateLifetimeHours).HasColumnName("default_certificate_lifetime_hours");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
