using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// In-memory conversation state storage for single-instance deployments.
/// Used when FeatureFlags.UseRedis is false.
/// </summary>
public sealed class InMemoryConversationStateRepository : IConversationStateRepository
{
    private readonly ConcurrentDictionary<string, ConversationContext> _conversations = new();
    private readonly RedisOptions _options;
    private readonly ILogger<InMemoryConversationStateRepository> _logger;
    private readonly Timer _cleanupTimer;

    public InMemoryConversationStateRepository(
        IOptions<RedisOptions> options,
        ILogger<InMemoryConversationStateRepository> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Periodic cleanup of expired conversations (every hour)
        _cleanupTimer = new Timer(
            _ => _ = CleanupExpiredAsync(TimeSpan.FromDays(_options.StateTtlDays)),
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1));
    }

    public Task<ConversationContext?> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId, out var context))
        {
            // Check if expired
            if (context.IsExpired(TimeSpan.FromDays(_options.StateTtlDays)))
            {
                _conversations.TryRemove(conversationId, out _);
                _logger.LogDebug("Conversation expired and removed: {ConversationId}", conversationId);
                return Task.FromResult<ConversationContext?>(null);
            }

            return Task.FromResult<ConversationContext?>(context);
        }

        return Task.FromResult<ConversationContext?>(null);
    }

    public Task SaveAsync(ConversationContext context, CancellationToken cancellationToken = default)
    {
        _conversations[context.ConversationId] = context;
        _logger.LogDebug("Conversation saved: {ConversationId}, turns: {Turns}",
            context.ConversationId,
            context.TurnCount);
        return Task.CompletedTask;
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
        _logger.LogDebug("New conversation created: {ConversationId}", conversationId);
        return newContext;
    }

    public Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var removed = _conversations.TryRemove(conversationId, out _);
        if (removed)
        {
            _logger.LogDebug("Conversation deleted: {ConversationId}", conversationId);
        }
        return Task.FromResult(removed);
    }

    public Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId, out var context))
        {
            return Task.FromResult(!context.IsExpired(TimeSpan.FromDays(_options.StateTtlDays)));
        }
        return Task.FromResult(false);
    }

    public Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var expiredKeys = _conversations
            .Where(kvp => kvp.Value.IsExpired(maxAge))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _conversations.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired conversations", expiredKeys.Count);
        }

        return Task.FromResult(expiredKeys.Count);
    }

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        var maxAge = TimeSpan.FromDays(_options.StateTtlDays);
        var count = _conversations.Values.Count(c => !c.IsExpired(maxAge));
        return Task.FromResult(count);
    }
}
