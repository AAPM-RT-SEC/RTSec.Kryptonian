using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HostnameColumnTypeChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop and recreate column with new type (safe for fresh DB)
            migrationBuilder.Sql("ALTER TABLE est_profiles DROP COLUMN hostnames;");
            migrationBuilder.Sql("ALTER TABLE est_profiles ADD COLUMN hostnames text[] NOT NULL DEFAULT '{}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<string>>(
                name: "hostnames",
                table: "est_profiles",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]");
        }
    }
}
