using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Domain.Interfaces;

/// <summary>
/// Repository for storing and retrieving user feedback.
/// Implementations: InMemoryFeedbackRepository (default), RedisFeedbackRepository (when USE_REDIS=true).
/// </summary>
/// <remarks>
/// Feedback is captured when users correct or dispute assistant responses.
/// This data is valuable for:
/// - Identifying datasheet errors or gaps
/// - Improving response quality
/// - Training and fine-tuning
/// </remarks>
public interface IFeedbackRepository
{
    /// <summary>
    /// Saves a feedback entry.
    /// </summary>
    /// <param name="feedback">The feedback to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(FeedbackEntry feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feedback entry by ID.
    /// </summary>
    /// <param name="id">The feedback ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The feedback entry, or null if not found.</returns>
    Task<FeedbackEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feedback for a specific conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feedback entries for the conversation.</returns>
    Task<IReadOnlyList<FeedbackEntry>> GetByConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feedback related to a specific product.
    /// Useful for identifying products with data quality issues.
    /// </summary>
    /// <param name="designation">The product designation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feedback entries for the product.</returns>
    Task<IReadOnlyList<FeedbackEntry>> GetByProductAsync(
        ProductDesignation designation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unreviewed feedback entries for human review.
    /// </summary>
    /// <param name="maxCount">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unreviewed feedback entries, oldest first.</returns>
    Task<IReadOnlyList<FeedbackEntry>> GetUnreviewedAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets feedback entries within a date range.
    /// </summary>
    /// <param name="from">Start of date range (inclusive).</param>
    /// <param name="to">End of date range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feedback entries within the range.</returns>
    Task<IReadOnlyList<FeedbackEntry>> GetByDateRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a feedback entry as reviewed.
    /// </summary>
    /// <param name="id">The feedback ID.</param>
    /// <param name="reviewNotes">Optional reviewer notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feedback was found and updated.</returns>
    Task<bool> MarkAsReviewedAsync(
        string id,
        string? reviewNotes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of feedback entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total feedback count.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
