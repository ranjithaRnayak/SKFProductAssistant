using Skf.ProductAssistant.Domain.Entities;

namespace Skf.ProductAssistant.Domain.Interfaces;

/// <summary>
/// Repository for persisting conversation state across requests.
/// Implementations: InMemoryConversationStateRepository (default), RedisConversationStateRepository (when USE_REDIS=true).
/// </summary>
/// <remarks>
/// Conversation state enables:
/// - Multi-turn conversations with context
/// - Pronoun resolution ("What is its weight?" → refers to current product)
/// - Follow-up questions without repeating product designation
///
/// State has TTL based on RedisOptions.StateTtlDays configuration.
/// </remarks>
public interface IConversationStateRepository
{
    /// <summary>
    /// Gets conversation state by ID.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation context, or null if not found or expired.</returns>
    Task<ConversationContext?> GetAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates conversation state.
    /// </summary>
    /// <param name="context">The conversation context to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(ConversationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a conversation context.
    /// If the conversation doesn't exist, creates a new one.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Existing or new conversation context.</returns>
    Task<ConversationContext> GetOrCreateAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a conversation state.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the conversation was found and deleted.</returns>
    Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a conversation exists.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the conversation exists and hasn't expired.</returns>
    Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes expired conversation states.
    /// Called periodically or on-demand to clean up old sessions.
    /// </summary>
    /// <param name="maxAge">Maximum age of conversations to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of conversations removed.</returns>
    Task<int> CleanupExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active conversations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active conversation count.</returns>
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);
}
