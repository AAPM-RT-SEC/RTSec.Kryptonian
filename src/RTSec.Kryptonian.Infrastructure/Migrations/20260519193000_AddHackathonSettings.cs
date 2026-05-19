using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTSec.Kryptonian.Infrastructure.Migrations
{
    public partial class AddHackathonSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hackathon_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    harness_base_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    team_token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    dimse_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    dimse_tls_port = table.Column<int>(type: "integer", nullable: false),
                    orthanc_dimse_port = table.Column<int>(type: "integer", nullable: false),
                    dicom_web_base_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    called_ae_title = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bridge_ae_title = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bridge_listen_port = table.Column<int>(type: "integer", nullable: false),
                    trusted_proxy_certificate_thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hackathon_settings", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "hackathon_settings");
        }
    }
}
