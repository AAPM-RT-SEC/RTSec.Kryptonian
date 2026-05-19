using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    public partial class FinalizeGateway : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "ca_backends",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subject_common_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    model = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_certificate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "device_record_id",
                table: "certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ca_backend_id",
                table: "certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ca_backend_type",
                table: "certificates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_der_base64",
                table: "certificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encrypted_private_key_pem",
                table: "certificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gateway_oid",
                table: "certificates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "device_record_id",
                table: "enrollment_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ca_backend_id",
                table: "enrollment_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ca_backends_is_active",
                table: "ca_backends",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_devices_subject_common_name",
                table: "devices",
                column: "subject_common_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_device_record_id",
                table: "certificates",
                column: "device_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_ca_backend_id",
                table: "certificates",
                column: "ca_backend_id");

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_device_record_id",
                table: "enrollment_events",
                column: "device_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_ca_backend_id",
                table: "enrollment_events",
                column: "ca_backend_id");

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_devices_device_record_id",
                table: "certificates",
                column: "device_record_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_certificates_ca_backends_ca_backend_id",
                table: "certificates",
                column: "ca_backend_id",
                principalTable: "ca_backends",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_enrollment_events_devices_device_record_id",
                table: "enrollment_events",
                column: "device_record_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_enrollment_events_ca_backends_ca_backend_id",
                table: "enrollment_events",
                column: "ca_backend_id",
                principalTable: "ca_backends",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_certificates_devices_device_record_id", "certificates");
            migrationBuilder.DropForeignKey("FK_certificates_ca_backends_ca_backend_id", "certificates");
            migrationBuilder.DropForeignKey("FK_enrollment_events_devices_device_record_id", "enrollment_events");
            migrationBuilder.DropForeignKey("FK_enrollment_events_ca_backends_ca_backend_id", "enrollment_events");

            migrationBuilder.DropTable(name: "devices");

            migrationBuilder.DropIndex(name: "IX_ca_backends_is_active", table: "ca_backends");
            migrationBuilder.DropIndex(name: "IX_certificates_device_record_id", table: "certificates");
            migrationBuilder.DropIndex(name: "IX_certificates_ca_backend_id", table: "certificates");
            migrationBuilder.DropIndex(name: "IX_enrollment_events_device_record_id", table: "enrollment_events");
            migrationBuilder.DropIndex(name: "IX_enrollment_events_ca_backend_id", table: "enrollment_events");

            migrationBuilder.DropColumn(name: "is_active", table: "ca_backends");
            migrationBuilder.DropColumn(name: "device_record_id", table: "certificates");
            migrationBuilder.DropColumn(name: "ca_backend_id", table: "certificates");
            migrationBuilder.DropColumn(name: "ca_backend_type", table: "certificates");
            migrationBuilder.DropColumn(name: "certificate_der_base64", table: "certificates");
            migrationBuilder.DropColumn(name: "encrypted_private_key_pem", table: "certificates");
            migrationBuilder.DropColumn(name: "gateway_oid", table: "certificates");
            migrationBuilder.DropColumn(name: "device_record_id", table: "enrollment_events");
            migrationBuilder.DropColumn(name: "ca_backend_id", table: "enrollment_events");
        }
    }
}
