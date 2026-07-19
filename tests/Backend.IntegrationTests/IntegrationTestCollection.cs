namespace InventorySystem.Backend.IntegrationTests;

/// <summary>
/// Regroupe toutes les classes de tests d'intégration pour qu'elles partagent le même
/// conteneur PostgreSQL (un seul démarrage, au lieu d'un par classe de test).
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Integration";
}
