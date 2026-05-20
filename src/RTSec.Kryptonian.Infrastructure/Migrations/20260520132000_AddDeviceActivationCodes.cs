using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260520132000_AddDeviceActivationCodes")]
    public partial class AddDeviceActivationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "activation_code_hash",
                table: "devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "activation_code_expires_at",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "activation_code_used_at",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "activation_code_hash",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "activation_code_expires_at",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "activation_code_used_at",
                table: "devices");
        }
    }
}
