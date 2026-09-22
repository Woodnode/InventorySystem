using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace InventorySystem.Infrastructure.Auth;

/// <summary>
/// Cache mémoire des rotations réussies (TTL = fenêtre de grâce concurrente).
/// </summary>
public sealed class MemoryRefreshRotationCache : IRefreshRotationCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private readonly IMemoryCache _cache;

    public MemoryRefreshRotationCache(IMemoryCache cache) => _cache = cache;

    public void Store(string consumedTokenHash, AuthResultDto result)
        => _cache.Set(CacheKey(consumedTokenHash), result, Ttl);

    public bool TryGet(string consumedTokenHash, out AuthResultDto? result)
        => _cache.TryGetValue(CacheKey(consumedTokenHash), out result);

    public void Remove(string consumedTokenHash) => _cache.Remove(CacheKey(consumedTokenHash));

    private static string CacheKey(string hash) => $"refresh-rotation:{hash}";
}
