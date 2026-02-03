using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Application.Plugins;

/// <summary>
/// Semantic Kernel plugin for caching datasheet lookups.
/// Improves performance by avoiding repeated datasheet reads.
/// </summary>
/// <remarks>
/// This plugin is optional and only used when EnableDatasheetCaching is true.
/// The cache stores string results from datasheet lookups.
/// </remarks>
public sealed class CachePlugin
{
    private readonly ICacheRepository _cache;
    private readonly FeatureFlags _featureFlags;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<CachePlugin> _logger;

    public CachePlugin(
        ICacheRepository cache,
        IOptions<FeatureFlags> featureFlags,
        IOptions<RedisOptions> redisOptions,
        ILogger<CachePlugin> logger)
    {
        _cache = cache;
        _featureFlags = featureFlags.Value;
        _redisOptions = redisOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets a cached value if available.
    /// </summary>
    [KernelFunction("get_cached_value")]
    [Description("Gets a previously cached lookup result. Returns the cached value or 'CACHE_MISS' if not found.")]
    public async Task<string> GetCachedValueAsync(
        [Description("The cache key (typically 'designation:attribute')")] string key)
    {
        if (!_featureFlags.EnableDatasheetCaching)
        {
            return "CACHE_DISABLED";
        }

        var value = await _cache.GetAsync<CachedValue>(key);

        if (value is null)
        {
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return "CACHE_MISS";
        }

        _logger.LogDebug("Cache hit for key: {Key}", key);
        return value.Value;
    }

    /// <summary>
    /// Caches a lookup result.
    /// </summary>
    [KernelFunction("cache_value")]
    [Description("Caches a lookup result for faster future access. Returns 'CACHED' on success.")]
    public async Task<string> CacheValueAsync(
        [Description("The cache key")] string key,
        [Description("The value to cache")] string value)
    {
        if (!_featureFlags.EnableDatasheetCaching)
        {
            return "CACHE_DISABLED";
        }

        var expiry = TimeSpan.FromMinutes(_redisOptions.CacheExpiryMinutes);
        await _cache.SetAsync(key, new CachedValue(value), expiry);

        _logger.LogDebug("Cached value for key: {Key}", key);
        return "CACHED";
    }

    /// <summary>
    /// Generates a cache key for product attribute lookups.
    /// </summary>
    [KernelFunction("make_cache_key")]
    [Description("Creates a cache key from product designation and attribute name.")]
    public string MakeCacheKey(
        [Description("The product designation")] string designation,
        [Description("The attribute name")] string attribute)
    {
        var normalizedDesignation = designation.ToUpperInvariant().Replace(" ", "").Replace("-", "");
        var normalizedAttribute = attribute.ToLowerInvariant().Replace(" ", "_");
        return $"attr:{normalizedDesignation}:{normalizedAttribute}";
    }

    /// <summary>
    /// Wrapper class for cached string values.
    /// </summary>
    private sealed record CachedValue(string Value);
}
