using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Interfaces;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// In-memory feedback storage for single-instance deployments.
/// Used when FeatureFlags.UseRedis is false.
/// </summary>
/// <remarks>
/// Feedback is stored in memory and will be lost on restart.
/// For production, use RedisFeedbackRepository or implement
/// persistence to a database.
/// </remarks>
public sealed class InMemoryFeedbackRepository : IFeedbackRepository
{
    private readonly ConcurrentDictionary<string, FeedbackEntry> _feedback = new();
    private readonly ILogger<InMemoryFeedbackRepository> _logger;

    public InMemoryFeedbackRepository(ILogger<InMemoryFeedbackRepository> logger)
    {
        _logger = logger;
    }

    public Task<FeedbackEntry?> GetByIdAsync(string feedbackId, CancellationToken cancellationToken = default)
    {
        _feedback.TryGetValue(feedbackId, out var entry);
        return Task.FromResult(entry);
    }

    public Task SaveAsync(FeedbackEntry feedback, CancellationToken cancellationToken = default)
    {
        _feedback[feedback.FeedbackId] = feedback;
        _logger.LogInformation(
            "Feedback saved: {FeedbackId} for product {Product}",
            feedback.FeedbackId,
            feedback.ProductDesignation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FeedbackEntry>> GetByConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var results = _feedback.Values
            .Where(f => f.ConversationId == conversationId)
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<FeedbackEntry>>(results);
    }

    public Task<IReadOnlyList<FeedbackEntry>> GetByProductAsync(
        string productDesignation,
        CancellationToken cancellationToken = default)
    {
        var normalized = productDesignation.ToUpperInvariant().Replace(" ", "").Replace("-", "");

        var results = _feedback.Values
            .Where(f => f.ProductDesignation?.ToUpperInvariant().Replace(" ", "").Replace("-", "") == normalized)
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<FeedbackEntry>>(results);
    }

    public Task<IReadOnlyList<FeedbackEntry>> GetPendingReviewAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = _feedback.Values
            .Where(f => !f.IsReviewed)
            .OrderBy(f => f.CreatedAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<FeedbackEntry>>(results);
    }

    public Task MarkAsReviewedAsync(
        string feedbackId,
        string? reviewerNotes = null,
        CancellationToken cancellationToken = default)
    {
        if (_feedback.TryGetValue(feedbackId, out var entry))
        {
            entry.MarkAsReviewed(reviewerNotes);
            _logger.LogInformation("Feedback marked as reviewed: {FeedbackId}", feedbackId);
        }

        return Task.CompletedTask;
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var count = _feedback.Values.Count(f => !f.IsReviewed);
        return Task.FromResult(count);
    }

    public Task<bool> DeleteAsync(string feedbackId, CancellationToken cancellationToken = default)
    {
        var removed = _feedback.TryRemove(feedbackId, out _);
        if (removed)
        {
            _logger.LogInformation("Feedback deleted: {FeedbackId}", feedbackId);
        }
        return Task.FromResult(removed);
    }
}
