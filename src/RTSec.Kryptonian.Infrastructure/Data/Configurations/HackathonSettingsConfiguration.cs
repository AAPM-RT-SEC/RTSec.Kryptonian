using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class HackathonSettingsConfiguration : IEntityTypeConfiguration<HackathonSettings>
{
    public void Configure(EntityTypeBuilder<HackathonSettings> builder)
    {
        builder.ToTable("hackathon_settings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.HarnessBaseUrl).HasColumnName("harness_base_url").IsRequired().HasMaxLength(2048);
        builder.Property(e => e.TeamToken).HasColumnName("team_token").HasMaxLength(512);
        builder.Property(e => e.DimseHost).HasColumnName("dimse_host").IsRequired().HasMaxLength(255);
        builder.Property(e => e.DimseTlsPort).HasColumnName("dimse_tls_port");
        builder.Property(e => e.OrthancDimsePort).HasColumnName("orthanc_dimse_port");
        builder.Property(e => e.DicomWebBaseUrl).HasColumnName("dicom_web_base_url").IsRequired().HasMaxLength(2048);
        builder.Property(e => e.CalledAeTitle).HasColumnName("called_ae_title").IsRequired().HasMaxLength(16);
        builder.Property(e => e.BridgeAeTitle).HasColumnName("bridge_ae_title").IsRequired().HasMaxLength(16);
        builder.Property(e => e.BridgeListenPort).HasColumnName("bridge_listen_port");
        builder.Property(e => e.TrustedProxyCertificateThumbprint).HasColumnName("trusted_proxy_certificate_thumbprint").HasMaxLength(128);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
