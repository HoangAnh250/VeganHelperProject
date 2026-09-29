using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace VeganHelper.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddNetTopologySuiteShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_shops_1",
                table: "shops");

            migrationBuilder.DropCheckConstraint(
                name: "CK_shops_2",
                table: "shops");

            migrationBuilder.DropCheckConstraint(
                name: "CK_shops_3",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "shops");

            migrationBuilder.AddColumn<Point>(
                name: "location",
                table: "shops",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "opening_hours",
                table: "shops",
                type: "NVARCHAR(500)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rating",
                table: "shops",
                type: "DECIMAL(3,1)",
                nullable: false,
                defaultValueSql: "0.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "opening_hours",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "shops");

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "shops",
                type: "DECIMAL(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "shops",
                type: "DECIMAL(10,7)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_shops_1",
                table: "shops",
                sql: "latitude BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "CK_shops_2",
                table: "shops",
                sql: "longitude BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_shops_3",
                table: "shops",
                sql: "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL)");
        }
    }
}
