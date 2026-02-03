using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// Redis-based feedback storage for multi-instance deployments.
/// Used when FeatureFlags.UseRedis is true.
/// </summary>
public sealed class RedisFeedbackRepository : IFeedbackRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisFeedbackRepository> _logger;
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisFeedbackRepository(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisFeedbackRepository> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    private string GetFeedbackKey(string feedbackId) => _options.GetKey("feedback", feedbackId);
    private string GetConversationIndexKey(string conversationId) => _options.GetKey("feedback:conv", conversationId);
    private string GetProductIndexKey(string product) => _options.GetKey("feedback:product", product.ToUpperInvariant());
    private string GetPendingSetKey() => $"{_options.KeyPrefix}feedback:pending";

    public async Task<FeedbackEntry?> GetByIdAsync(string feedbackId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetFeedbackKey(feedbackId);
            var value = await _db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<FeedbackEntry>(value!, JsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for feedback get: {FeedbackId}", feedbackId);
            return null;
        }
    }

    public async Task SaveAsync(FeedbackEntry feedback, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetFeedbackKey(feedback.FeedbackId);
            var json = JsonSerializer.Serialize(feedback, JsonOptions);

            var batch = _db.CreateBatch();

            // Store the feedback entry
            _ = batch.StringSetAsync(key, json);

            // Add to conversation index
            _ = batch.SetAddAsync(GetConversationIndexKey(feedback.ConversationId), feedback.FeedbackId);

            // Add to product index if product specified
            if (!string.IsNullOrEmpty(feedback.ProductDesignation))
            {
                _ = batch.SetAddAsync(GetProductIndexKey(feedback.ProductDesignation), feedback.FeedbackId);
            }

            // Add to pending set if not reviewed
            if (!feedback.IsReviewed)
            {
                _ = batch.SortedSetAddAsync(GetPendingSetKey(), feedback.FeedbackId, feedback.CreatedAt.ToUnixTimeSeconds());
            }

            batch.Execute();
            await batch.WaitAll();

            _logger.LogInformation(
                "Feedback saved to Redis: {FeedbackId} for product {Product}",
                feedback.FeedbackId,
                feedback.ProductDesignation);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for feedback save: {FeedbackId}", feedback.FeedbackId);
        }
    }

    public async Task<IReadOnlyList<FeedbackEntry>> GetByConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var indexKey = GetConversationIndexKey(conversationId);
            var feedbackIds = await _db.SetMembersAsync(indexKey);

            var entries = new List<FeedbackEntry>();
            foreach (var id in feedbackIds)
            {
                var entry = await GetByIdAsync(id!, cancellationToken);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            return entries.OrderByDescending(f => f.CreatedAt).ToList();
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for feedback by conversation: {ConversationId}", conversationId);
            return [];
        }
    }

    public async Task<IReadOnlyList<FeedbackEntry>> GetByProductAsync(
        string productDesignation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var indexKey = GetProductIndexKey(productDesignation);
            var feedbackIds = await _db.SetMembersAsync(indexKey);

            var entries = new List<FeedbackEntry>();
            foreach (var id in feedbackIds)
            {
                var entry = await GetByIdAsync(id!, cancellationToken);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            return entries.OrderByDescending(f => f.CreatedAt).ToList();
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for feedback by product: {Product}", productDesignation);
            return [];
        }
    }

    public async Task<IReadOnlyList<FeedbackEntry>> GetPendingReviewAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingIds = await _db.SortedSetRangeByRankAsync(GetPendingSetKey(), 0, maxResults - 1);

            var entries = new List<FeedbackEntry>();
            foreach (var id in pendingIds)
            {
                var entry = await GetByIdAsync(id!, cancellationToken);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for pending feedback");
            return [];
        }
    }

    public async Task MarkAsReviewedAsync(
        string feedbackId,
        string? reviewerNotes = null,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(feedbackId, cancellationToken);
        if (entry is null) return;

        entry.MarkAsReviewed(reviewerNotes);
        await SaveAsync(entry, cancellationToken);

        // Remove from pending set
        await _db.SortedSetRemoveAsync(GetPendingSetKey(), feedbackId);

        _logger.LogInformation("Feedback marked as reviewed: {FeedbackId}", feedbackId);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return (int)await _db.SortedSetLengthAsync(GetPendingSetKey());
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for pending count");
            return 0;
        }
    }

    public async Task<bool> DeleteAsync(string feedbackId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await GetByIdAsync(feedbackId, cancellationToken);
            if (entry is null) return false;

            var batch = _db.CreateBatch();

            _ = batch.KeyDeleteAsync(GetFeedbackKey(feedbackId));
            _ = batch.SetRemoveAsync(GetConversationIndexKey(entry.ConversationId), feedbackId);
            _ = batch.SortedSetRemoveAsync(GetPendingSetKey(), feedbackId);

            if (!string.IsNullOrEmpty(entry.ProductDesignation))
            {
                _ = batch.SetRemoveAsync(GetProductIndexKey(entry.ProductDesignation), feedbackId);
            }

            batch.Execute();
            await batch.WaitAll();

            _logger.LogInformation("Feedback deleted from Redis: {FeedbackId}", feedbackId);
            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for feedback delete: {FeedbackId}", feedbackId);
            return false;
        }
    }
}
