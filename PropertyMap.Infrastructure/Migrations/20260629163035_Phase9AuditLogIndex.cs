using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyMap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase9AuditLogIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_FechaAccion",
                table: "AuditLogs",
                column: "FechaAccion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_FechaAccion",
                table: "AuditLogs");
        }
    }
}
