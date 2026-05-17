using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workshop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetailCasePanels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retail_case_panels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retail_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body_panel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retail_case_panels", x => x.id);
                    table.ForeignKey(
                        name: "fk_retail_case_panels_body_panels_body_panel_id",
                        column: x => x.body_panel_id,
                        principalTable: "body_panels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_retail_case_panels_retail_cases_retail_case_id",
                        column: x => x.retail_case_id,
                        principalTable: "retail_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_retail_case_panels_body_panel_id",
                table: "retail_case_panels",
                column: "body_panel_id");

            migrationBuilder.CreateIndex(
                name: "ix_retail_case_panels_retail_case_id_body_panel_id",
                table: "retail_case_panels",
                columns: new[] { "retail_case_id", "body_panel_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retail_case_panels");
        }
    }
}
