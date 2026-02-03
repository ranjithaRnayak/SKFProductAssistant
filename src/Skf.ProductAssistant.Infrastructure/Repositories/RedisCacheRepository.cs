using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// Redis-based cache implementation for multi-instance deployments.
/// Used when FeatureFlags.UseRedis is true.
/// </summary>
/// <remarks>
/// This implementation provides:
/// - Distributed caching across multiple Azure Function instances
/// - Automatic TTL-based expiration managed by Redis
/// - Atomic operations for cache consistency
/// </remarks>
public sealed class RedisCacheRepository : ICacheRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheRepository> _logger;
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheRepository(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisCacheRepository> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    private string GetKey(string key) => _options.GetKey("cache", key);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var redisKey = GetKey(key);
            var value = await _db.StringGetAsync(redisKey);

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Redis cache miss for key: {Key}", key);
                return null;
            }

            _logger.LogDebug("Redis cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for cache get: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var redisKey = GetKey(key);
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var ttl = expiry ?? TimeSpan.FromMinutes(_options.CacheExpiryMinutes);

            await _db.StringSetAsync(redisKey, json, ttl);
            _logger.LogDebug("Redis cache set for key: {Key}, TTL: {Ttl}", key, ttl);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for cache set: {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default) where T : class
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

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            var removed = await _db.KeyDeleteAsync(redisKey);
            _logger.LogDebug("Redis cache remove for key: {Key}, removed: {Removed}", key, removed);
            return removed;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for cache remove: {Key}", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            return await _db.KeyExistsAsync(redisKey);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for cache exists: {Key}", key);
            return false;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_options.KeyPrefix}cache:*";

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                await _db.KeyDeleteAsync(key);
            }

            _logger.LogInformation("Redis cache cleared");
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for cache clear");
        }
    }

    public async Task<long> GetSizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_options.KeyPrefix}cache:*";

            var count = 0L;
            await foreach (var _ in server.KeysAsync(pattern: pattern))
            {
                count++;
            }

            return count;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for cache size");
            return 0;
        }
    }
}
