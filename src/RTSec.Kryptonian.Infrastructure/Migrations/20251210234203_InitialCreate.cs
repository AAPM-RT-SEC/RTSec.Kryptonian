using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ca_backends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    config = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ca_backends", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "est_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    path_prefix = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    hostnames = table.Column<List<string>>(type: "jsonb", nullable: false),
                    ca_backend_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_template = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    allowed_key_usages = table.Column<List<string>>(type: "jsonb", nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 365),
                    require_client_certificate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_est_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_est_profiles_ca_backends_ca_backend_id",
                        column: x => x.ca_backend_id,
                        principalTable: "ca_backends",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    subject_dn = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    issuer_dn = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    certificate_pem = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    est_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.id);
                    table.ForeignKey(
                        name: "FK_certificates_est_profiles_est_profile_id",
                        column: x => x.est_profile_id,
                        principalTable: "est_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enrollment_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_dn = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    requestor_ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    issued_certificate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollment_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_enrollment_events_certificates_issued_certificate_id",
                        column: x => x.issued_certificate_id,
                        principalTable: "certificates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_enrollment_events_est_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "est_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ca_backends_name",
                table: "ca_backends",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_device_id",
                table: "certificates",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_est_profile_id",
                table: "certificates",
                column: "est_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_not_after",
                table: "certificates",
                column: "not_after");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_serial_number",
                table: "certificates",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_status",
                table: "certificates",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_thumbprint",
                table: "certificates",
                column: "thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_device_id",
                table: "enrollment_events",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_issued_certificate_id",
                table: "enrollment_events",
                column: "issued_certificate_id");

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_profile_id",
                table: "enrollment_events",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_status",
                table: "enrollment_events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_enrollment_events_timestamp",
                table: "enrollment_events",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_est_profiles_ca_backend_id",
                table: "est_profiles",
                column: "ca_backend_id");

            migrationBuilder.CreateIndex(
                name: "IX_est_profiles_name",
                table: "est_profiles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_est_profiles_path_hostnames",
                table: "est_profiles",
                columns: new[] { "path_prefix", "hostnames" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enrollment_events");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "est_profiles");

            migrationBuilder.DropTable(
                name: "ca_backends");
        }
    }
}
