using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class EstProfileConfiguration : IEntityTypeConfiguration<EstProfile>
{
    public void Configure(EntityTypeBuilder<EstProfile> builder)
    {
        builder.ToTable("est_profiles");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.PathPrefix)
            .HasColumnName("path_prefix")
            .IsRequired()
            .HasMaxLength(255);

        // EF Core 8 maps primitive collections to JSON by default — works on Postgres,
        // SQLite, and in-memory without provider-specific column hints.
        builder.PrimitiveCollection(e => e.Hostnames)
            .HasColumnName("hostnames")
            .Metadata.SetValueComparer(ValueComparers.StringCollectionComparer);

        builder.Property(e => e.HostnameMatchType)
            .HasColumnName("hostname_match_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Domain.Enums.HostnameMatchType.Exact);

        builder.Property(e => e.AllowedWildcardSuffix)
            .HasColumnName("allowed_wildcard_suffix")
            .HasMaxLength(255);

        builder.Property(e => e.CaBackendId)
            .HasColumnName("ca_backend_id")
            .IsRequired();

        builder.Property(e => e.CertificateTemplate)
            .HasColumnName("certificate_template")
            .HasMaxLength(255);

        builder.PrimitiveCollection(e => e.AllowedKeyUsages)
            .HasColumnName("allowed_key_usages")
            .Metadata.SetValueComparer(ValueComparers.StringCollectionComparer);

        builder.Property(e => e.ValidityDays)
            .HasColumnName("validity_days")
            .HasDefaultValue(365);

        builder.Property(e => e.RequireClientCertificate)
            .HasColumnName("require_client_certificate")
            .HasDefaultValue(true);

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(e => e.CaBackend)
            .WithMany(c => c.EstProfiles)
            .HasForeignKey(e => e.CaBackendId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Name)
            .IsUnique();

        // Hostnames is a JSON-mapped primitive collection; SQL indexes cannot cover it
        // portably. Hostname matching happens in-process after loading the profile, so
        // no index is needed here.
        builder.HasIndex(e => e.PathPrefix)
            .HasDatabaseName("ix_est_profiles_path_prefix");
    }
}
