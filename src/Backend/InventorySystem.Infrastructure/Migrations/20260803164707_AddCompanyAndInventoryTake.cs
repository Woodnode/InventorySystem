using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAndInventoryTake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InventoryDate",
                table: "stocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleName",
                table: "stocks",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "products",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryDate",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "ResponsibleName",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "products");
        }
    }
}
