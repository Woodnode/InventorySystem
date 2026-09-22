using InventorySystem.Domain.Common;
using InventorySystem.Domain.Exceptions;

namespace InventorySystem.Domain.Entities;

/// <summary>Fournisseur d'un ou plusieurs produits (plan §3 : gestion des fournisseurs).</summary>
public sealed class Supplier : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? ContactEmail { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; } = true;

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

    public void Update(string name, string? contactEmail, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Le nom du fournisseur est obligatoire.");

        Name = name;
        ContactEmail = contactEmail;
        Phone = phone;
    }

    /// <summary>Remplace la suppression : un fournisseur référencé par des produits existants
    /// ne doit jamais disparaître (même raisonnement que Warehouse — voir ré-audit F-4).</summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
