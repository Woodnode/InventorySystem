using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.ValueObjects;

/// <summary>
/// Value Object représentant un SKU (Stock Keeping Unit).
/// Immuable, auto-validant : évite la primitive obsession sur les chaînes.
/// </summary>
public sealed record Sku
{
    public string Value { get; }

    private Sku(string value) => Value = value;

    public static Sku Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Le SKU ne peut pas être vide.");

        value = value.Trim().ToUpperInvariant();

        if (value.Length is < 3 or > 32)
            throw new DomainException("Le SKU doit contenir entre 3 et 32 caractères.");

        return new Sku(value);
    }

    public override string ToString() => Value;
}
