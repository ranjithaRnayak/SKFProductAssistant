using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// Redis-based conversation state storage for multi-instance deployments.
/// Used when FeatureFlags.UseRedis is true.
/// </summary>
public sealed class RedisConversationStateRepository : IConversationStateRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisConversationStateRepository> _logger;
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisConversationStateRepository(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisConversationStateRepository> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    private string GetKey(string conversationId) => _options.GetKey("state", conversationId);
    private TimeSpan GetTtl() => TimeSpan.FromDays(_options.StateTtlDays);

    public async Task<ConversationContext?> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetKey(conversationId);
            var value = await _db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Conversation not found in Redis: {ConversationId}", conversationId);
                return null;
            }

            _logger.LogDebug("Conversation loaded from Redis: {ConversationId}", conversationId);
            return JsonSerializer.Deserialize<ConversationContext>(value!, JsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for state get: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task SaveAsync(ConversationContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetKey(context.ConversationId);
            var json = JsonSerializer.Serialize(context, JsonOptions);

            await _db.StringSetAsync(key, json, GetTtl());
            _logger.LogDebug("Conversation saved to Redis: {ConversationId}", context.ConversationId);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for state save: {ConversationId}", context.ConversationId);
        }
    }

    public async Task<ConversationContext> GetOrCreateAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(conversationId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var newContext = ConversationContext.Create(conversationId);
        await SaveAsync(newContext, cancellationToken);
        _logger.LogDebug("New conversation created in Redis: {ConversationId}", conversationId);
        return newContext;
    }

    public async Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetKey(conversationId);
            var deleted = await _db.KeyDeleteAsync(key);
            _logger.LogDebug("Conversation deleted from Redis: {ConversationId}, success: {Deleted}", conversationId, deleted);
            return deleted;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for state delete: {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetKey(conversationId);
            return await _db.KeyExistsAsync(key);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for state exists: {ConversationId}", conversationId);
            return false;
        }
    }

    public Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        // Redis handles TTL-based expiration automatically
        // This method is primarily for interface compatibility with InMemory implementation
        _logger.LogDebug("Redis handles expiration via TTL, no manual cleanup needed");
        return Task.FromResult(0);
    }

    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_options.KeyPrefix}state:*";

            var count = 0;
            await foreach (var _ in server.KeysAsync(pattern: pattern))
            {
                count++;
            }

            return count;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for active count");
            return 0;
        }
    }
}
