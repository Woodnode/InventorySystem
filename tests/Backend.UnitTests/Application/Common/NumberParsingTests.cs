using FluentAssertions;
using InventorySystem.Application.Common;
using Xunit;

namespace InventorySystem.Backend.UnitTests.Application.Common;

/// <summary>
/// Couvre la lecture des decimaux importes.
///
/// L'implementation precedente lisait « 0,6 » comme 6 : la culture invariante traite
/// la virgule comme un separateur de milliers des lors que le style l'autorise. Tous
/// les poids saisis dans un classeur francophone se sont ainsi trouves multiplies par
/// dix ou par cent en base.
/// </summary>
public sealed class NumberParsingTests
{
    [Theory]
    // Decimales a la francaise : le cas qui a produit le defaut.
    [InlineData("0,6", 0.6)]
    [InlineData("0,41", 0.41)]
    [InlineData("1,5", 1.5)]
    [InlineData("12,75", 12.75)]
    // Decimales a l'anglaise.
    [InlineData("0.6", 0.6)]
    [InlineData("1.5", 1.5)]
    // Entiers.
    [InlineData("6", 6)]
    [InlineData("178", 178)]
    [InlineData("-3,5", -3.5)]
    // Separateurs de milliers, dans les deux conventions.
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("12,345,678", 12345678)]
    public void ParseDecimal_ReadsBothConventions(string entree, double attendu)
        => NumberParsing.ParseDecimal(entree).Should().Be((decimal)attendu);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("—")]
    public void ParseDecimal_ReturnsNullWhenThereIsNoNumber(string? entree)
        => NumberParsing.ParseDecimal(entree).Should().BeNull();

    [Fact]
    public void ParseDecimal_DoesNotMultiplyFrenchDecimalsByTen()
    {
        // Verification directe du defaut : 0,6 lb ne doit pas devenir 6 lb.
        NumberParsing.ParseDecimal("0,6").Should().NotBe(6m);
        NumberParsing.ParseDecimal("0,41").Should().NotBe(41m);
    }
}
