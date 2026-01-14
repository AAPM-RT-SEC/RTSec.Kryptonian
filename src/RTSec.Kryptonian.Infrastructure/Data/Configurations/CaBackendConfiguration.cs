using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Infrastructure.Data;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class CaBackendConfiguration : IEntityTypeConfiguration<CaBackend>
{
    public void Configure(EntityTypeBuilder<CaBackend> builder)
    {
        builder.ToTable("ca_backends");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Url)
            .HasColumnName("url")
            .HasMaxLength(1024);

        builder.Property(e => e.Config)
            .HasColumnName("config")
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(ValueComparers.DictionaryComparer);

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.Name)
            .IsUnique();
    }
}
