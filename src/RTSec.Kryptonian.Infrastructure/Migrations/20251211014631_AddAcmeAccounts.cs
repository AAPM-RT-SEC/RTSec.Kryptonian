using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcmeAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "TrustedClientCaThumbprints",
                table: "est_profiles",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<bool>(
                name: "ValidateClientCertificateChain",
                table: "est_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "acme_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    directory_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    account_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    encrypted_private_key = table.Column<string>(type: "text", nullable: false),
                    terms_of_service_accepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    eab_key_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encrypted_eab_hmac_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acme_accounts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_acme_accounts_directory_url_email",
                table: "acme_accounts",
                columns: new[] { "directory_url", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_acme_accounts_is_active",
                table: "acme_accounts",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acme_accounts");

            migrationBuilder.DropColumn(
                name: "TrustedClientCaThumbprints",
                table: "est_profiles");

            migrationBuilder.DropColumn(
                name: "ValidateClientCertificateChain",
                table: "est_profiles");
        }
    }
}
