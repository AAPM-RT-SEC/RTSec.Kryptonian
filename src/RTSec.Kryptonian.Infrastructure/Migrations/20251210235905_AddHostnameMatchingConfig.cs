using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHostnameMatchingConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "allowed_wildcard_suffix",
                table: "est_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hostname_match_type",
                table: "est_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Exact");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allowed_wildcard_suffix",
                table: "est_profiles");

            migrationBuilder.DropColumn(
                name: "hostname_match_type",
                table: "est_profiles");
        }
    }
}
