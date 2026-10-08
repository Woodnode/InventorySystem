namespace InventorySystem.Application.Products.Dtos;

/// <summary>Une collection éditoriale et le nombre de références qu'elle porte.</summary>
public sealed record CollectionDto(string Name, int Count);
