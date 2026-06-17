using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyMap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "PropertyListings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Antiguedad",
                table: "PropertyListings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Cochera",
                table: "PropertyListings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SuperficieCubierta",
                table: "PropertyListings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "Antiguedad",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "Cochera",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "SuperficieCubierta",
                table: "PropertyListings");
        }
    }
}
