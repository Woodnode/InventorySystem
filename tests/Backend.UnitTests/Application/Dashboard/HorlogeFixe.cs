namespace InventorySystem.Backend.UnitTests.Application.Dashboard;

/// <summary>Horloge arrêtée sur un instant donné, pour des tests indépendants de l'heure.</summary>
internal sealed class HorlogeFixe(DateTimeOffset maintenant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => maintenant;
}
