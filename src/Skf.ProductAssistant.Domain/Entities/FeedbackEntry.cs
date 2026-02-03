using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Domain.Entities;

/// <summary>
/// Represents user feedback or correction about a product answer.
/// Captured by FeedbackAgent for analysis and potential datasheet improvements.
/// </summary>
/// <remarks>
/// Feedback entries help identify:
/// - Incorrect or outdated datasheet information
/// - Missing attributes users expect to find
/// - Unclear or ambiguous responses
/// - User experience issues with the assistant
/// </remarks>
public sealed class FeedbackEntry
{
    /// <summary>
    /// Unique identifier for this feedback entry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The conversation ID where feedback was provided.
    /// Links feedback to conversation context for analysis.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Product designation the feedback relates to (if applicable).
    /// Null when feedback is about general assistant behavior.
    /// </summary>
    public ProductDesignation? ProductDesignation { get; init; }

    /// <summary>
    /// The attribute being corrected (if applicable).
    /// Null when feedback is not about a specific attribute.
    /// </summary>
    public string? AttributeName { get; init; }

    /// <summary>
    /// The user's feedback message.
    /// </summary>
    public required string UserMessage { get; init; }

    /// <summary>
    /// The original assistant response that prompted feedback.
    /// Preserved for context when reviewing feedback.
    /// </summary>
    public string? OriginalResponse { get; init; }

    /// <summary>
    /// The corrected value suggested by the user (if provided).
    /// </summary>
    public string? SuggestedCorrection { get; init; }

    /// <summary>
    /// Timestamp when feedback was captured (UTC).
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Indicates if this feedback has been reviewed by a human.
    /// </summary>
    public bool IsReviewed { get; init; }

    /// <summary>
    /// Optional reviewer notes after human review.
    /// </summary>
    public string? ReviewNotes { get; init; }

    /// <summary>
    /// Creates a new feedback entry with auto-generated ID and timestamp.
    /// </summary>
    public static FeedbackEntry Create(
        string conversationId,
        string userMessage,
        ProductDesignation? productDesignation = null,
        string? attributeName = null,
        string? originalResponse = null,
        string? suggestedCorrection = null)
    {
        return new FeedbackEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversationId,
            ProductDesignation = productDesignation,
            AttributeName = attributeName,
            UserMessage = userMessage,
            OriginalResponse = originalResponse,
            SuggestedCorrection = suggestedCorrection,
            CreatedAt = DateTimeOffset.UtcNow,
            IsReviewed = false
        };
    }

    public override string ToString() =>
        $"Feedback [{Id}] for {ProductDesignation?.ToString() ?? "General"}: {UserMessage[..Math.Min(50, UserMessage.Length)]}...";
}
