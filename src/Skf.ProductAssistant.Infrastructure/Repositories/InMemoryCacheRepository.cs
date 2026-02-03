using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Skf.ProductAssistant.Domain.Interfaces;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// In-memory cache implementation for single-instance deployments.
/// Used when FeatureFlags.UseRedis is false.
/// </summary>
/// <remarks>
/// This implementation is suitable for:
/// - Development and testing
/// - Single-instance Azure Function deployments
/// - Scenarios where cache consistency across instances is not required
///
/// For multi-instance deployments, use RedisCacheRepository instead.
/// </remarks>
public sealed class InMemoryCacheRepository : ICacheRepository
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<InMemoryCacheRepository> _logger;
    private readonly Timer _cleanupTimer;

    public InMemoryCacheRepository(ILogger<InMemoryCacheRepository> logger)
    {
        _logger = logger;

        // Periodic cleanup of expired entries (every 5 minutes)
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return Task.FromResult(entry.Value as T);
            }

            // Entry expired, remove it
            _cache.TryRemove(key, out _);
            _logger.LogDebug("Cache expired for key: {Key}", key);
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(60));

        _cache[key] = new CacheEntry(value, expiresAt);
        _logger.LogDebug("Cache set for key: {Key}, expires: {ExpiresAt}", key, expiresAt);

        return Task.CompletedTask;
    }

    public Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default) where T : class
    {
        return GetOrSetInternalAsync(key, factory, expiry, cancellationToken);
    }

    private async Task<T> GetOrSetInternalAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry,
        CancellationToken cancellationToken) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var removed = _cache.TryRemove(key, out _);
        if (removed)
        {
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        return Task.FromResult(removed);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            return Task.FromResult(entry.ExpiresAt > DateTimeOffset.UtcNow);
        }
        return Task.FromResult(false);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        _logger.LogInformation("Cache cleared");
        return Task.CompletedTask;
    }

    public Task<long> GetSizeAsync(CancellationToken cancellationToken = default)
    {
        // Count only non-expired entries
        var count = _cache.Values.Count(e => e.ExpiresAt > DateTimeOffset.UtcNow);
        return Task.FromResult((long)count);
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);
}
