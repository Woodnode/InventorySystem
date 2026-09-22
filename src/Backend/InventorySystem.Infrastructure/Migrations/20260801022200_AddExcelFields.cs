using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExcelFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoxesCount",
                table: "stocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "stocks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CopiesPerBox",
                table: "stocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DistributorName",
                table: "stocks",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EntryDate",
                table: "stocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitDate",
                table: "stocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pallet",
                table: "stocks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnDate",
                table: "stocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "stocks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Space",
                table: "stocks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Collection",
                table: "products",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectCode",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VolumeNumber",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerCopyLb",
                table: "products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "products",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoxesCount",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "CopiesPerBox",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "DistributorName",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "EntryDate",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "ExitDate",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "Pallet",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "ReturnDate",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "Space",
                table: "stocks");

            migrationBuilder.DropColumn(
                name: "Collection",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProjectCode",
                table: "products");

            migrationBuilder.DropColumn(
                name: "VolumeNumber",
                table: "products");

            migrationBuilder.DropColumn(
                name: "WeightPerCopyLb",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "products");
        }
    }
}
