using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Infrastructure.Migrations
{
    /// <summary>
    /// Passage du poids unitaire au systeme metrique.
    ///
    /// Le renommage seul ne suffit pas : les valeurs deja en base sont exprimees en
    /// livres. Sans la conversion qui suit, elles seraient relues comme des grammes
    /// et chaque poids se trouverait divise par 453 dans les totaux.
    /// </summary>
    public partial class RenameWeightPerCopyToGrams : Migration
    {
        /// <summary>Facteur exact de la livre avoirdupois.</summary>
        private const string GrammesParLivre = "453.59237";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WeightPerCopyLb",
                table: "products",
                newName: "WeightPerCopyGrams");

            migrationBuilder.Sql(
                $"""
                UPDATE products
                SET "WeightPerCopyGrams" = ROUND("WeightPerCopyGrams" * {GrammesParLivre}, 2)
                WHERE "WeightPerCopyGrams" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reconversion avant renommage, dans l'ordre inverse de Up.
            migrationBuilder.Sql(
                $"""
                UPDATE products
                SET "WeightPerCopyGrams" = ROUND("WeightPerCopyGrams" / {GrammesParLivre}, 4)
                WHERE "WeightPerCopyGrams" IS NOT NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "WeightPerCopyGrams",
                table: "products",
                newName: "WeightPerCopyLb");
        }
    }
}
