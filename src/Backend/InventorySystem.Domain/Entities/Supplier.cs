using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.Entities;

/// <summary>Fournisseur d'un ou plusieurs produits (plan §3 : gestion des fournisseurs).</summary>
public sealed class Supplier : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? ContactEmail { get; private set; }
    public string? Phone { get; private set; }

    private Supplier() { }

    private Supplier(string name, string? contactEmail, string? phone)
    {
        Name = name;
        ContactEmail = contactEmail;
        Phone = phone;
    }

    public static Supplier Create(string name, string? contactEmail, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Le nom du fournisseur est obligatoire.");

        return new Supplier(name, contactEmail, phone);
    }
}
