namespace InventorySystem.Application.Common.Dtos;

/// <summary>
/// Enveloppe de pagination générique pour les listes qui grandissent sans borne dans le
/// temps (catalogue produits, historique des mouvements). Volontairement pas utilisée pour
/// Warehouses/Suppliers : ce sont des listes de référence de petite taille, réutilisées
/// telles quelles comme source de menus déroulants (formulaire produit, formulaire de
/// mouvement) côté web ET mobile — les paginer casserait ces sélecteurs sans résoudre de
/// problème réel à l'échelle attendue.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
