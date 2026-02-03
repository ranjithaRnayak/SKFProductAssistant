using System.ComponentModel.DataAnnotations;

namespace Skf.ProductAssistant.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Redis cache and state storage.
/// Bound from appsettings.json "Redis" section.
/// Redis is optional - controlled by FeatureFlags.UseRedis.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string (e.g., "localhost:6379" or Azure Redis connection string).
    /// Required only when FeatureFlags.UseRedis is true.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Whether Redis is enabled. Mirrors FeatureFlags.UseRedis for convenience.
    /// When false, in-memory implementations are used.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Cache expiry time in minutes for product data lookups.
    /// Longer values improve performance but may serve stale data.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "CacheExpiryMinutes must be between 1 and 1440 (24 hours)")]
    public int CacheExpiryMinutes { get; init; } = 60;

    /// <summary>
    /// Time-to-live for conversation state in days.
    /// Conversations older than this are automatically cleaned up.
    /// </summary>
    [Range(1, 30, ErrorMessage = "StateTtlDays must be between 1 and 30")]
    public int StateTtlDays { get; init; } = 7;

    /// <summary>
    /// Key prefix for all Redis keys to avoid collisions in shared Redis instances.
    /// </summary>
    public string KeyPrefix { get; init; } = "skf:productassistant:";

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    [Range(1000, 30000, ErrorMessage = "ConnectTimeoutMs must be between 1000 and 30000")]
    public int ConnectTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Gets the full Redis key for a given key type and identifier.
    /// </summary>
    public string GetKey(string keyType, string identifier)
    {
        return $"{KeyPrefix}{keyType}:{identifier}";
    }
}
