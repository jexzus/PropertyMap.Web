using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyMap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Destacado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Destacado",
                table: "PropertyListings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Destacado",
                table: "PropertyListings");
        }
    }
}
