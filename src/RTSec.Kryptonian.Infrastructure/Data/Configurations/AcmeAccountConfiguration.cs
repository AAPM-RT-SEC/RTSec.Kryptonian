using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Infrastructure.Data.Configurations;

public class AcmeAccountConfiguration : IEntityTypeConfiguration<AcmeAccount>
{
    public void Configure(EntityTypeBuilder<AcmeAccount> builder)
    {
        builder.ToTable("acme_accounts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.DirectoryUrl)
            .HasColumnName("directory_url")
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.AccountUrl)
            .HasColumnName("account_url")
            .HasMaxLength(1024);

        builder.Property(e => e.EncryptedPrivateKey)
            .HasColumnName("encrypted_private_key")
            .IsRequired();

        builder.Property(e => e.TermsOfServiceAccepted)
            .HasColumnName("terms_of_service_accepted")
            .HasDefaultValue(false);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.EabKeyId)
            .HasColumnName("eab_key_id")
            .HasMaxLength(255);

        builder.Property(e => e.EncryptedEabHmacKey)
            .HasColumnName("encrypted_eab_hmac_key");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        // Index for looking up accounts by directory URL
        builder.HasIndex(e => new { e.DirectoryUrl, e.Email })
            .IsUnique();

        builder.HasIndex(e => e.IsActive);
    }
}
