using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NB : les colonnes refresh_tokens.FamilyId/ReplacedByTokenId (+ index) que EF
            // avait initialement regénérées ici ont été retirées — elles sont déjà créées par
            // la migration 20260727025446_AddRefreshTokenFamily, dont le snapshot du modèle
            // n'avait simplement jamais été commité (fichiers de migration non trackés trouvés
            // sur disque). Les laisser aurait fait échouer cette migration sur toute base où
            // AddRefreshTokenFamily est déjà (ou sera) appliquée en premier — colonne dupliquée.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "suppliers");
        }
    }
}
