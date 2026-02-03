using Skf.ProductAssistant.Domain.Enums;
using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Domain.Entities;

/// <summary>
/// Maintains conversation state across multiple turns with the same user.
/// Enables contextual follow-up questions and coherent multi-turn interactions.
/// </summary>
/// <remarks>
/// Example multi-turn conversation:
/// Turn 1: "What is the bore diameter of 6205-2RS?" → Stores CurrentProduct = 6205-2RS
/// Turn 2: "What about its outer diameter?" → Uses CurrentProduct for context
/// Turn 3: "Compare it to 6206-2RS" → Updates context with comparison mode
/// </remarks>
public sealed class ConversationContext
{
    /// <summary>
    /// Unique identifier for this conversation.
    /// Typically provided by the client or generated on first request.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// The product currently being discussed.
    /// Enables pronoun resolution ("What is its weight?" → refers to CurrentProduct).
    /// </summary>
    public ProductDesignation? CurrentProduct { get; private set; }

    /// <summary>
    /// Products mentioned in this conversation for comparison or history.
    /// </summary>
    public IReadOnlyList<ProductDesignation> MentionedProducts => _mentionedProducts.AsReadOnly();
    private readonly List<ProductDesignation> _mentionedProducts = [];

    /// <summary>
    /// The last attribute discussed, for follow-up questions.
    /// "What is the bore diameter?" followed by "In inches?" uses this.
    /// </summary>
    public string? LastAttribute { get; private set; }

    /// <summary>
    /// The intent of the most recent user message.
    /// </summary>
    public IntentType LastIntent { get; private set; } = IntentType.Unknown;

    /// <summary>
    /// Timestamp of the last interaction (UTC).
    /// Used for session expiry and TTL management.
    /// </summary>
    public DateTimeOffset LastUpdated { get; private set; }

    /// <summary>
    /// Timestamp when the conversation was created (UTC).
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Number of turns in this conversation.
    /// </summary>
    public int TurnCount { get; private set; }

    /// <summary>
    /// Creates a new conversation context.
    /// </summary>
    public static ConversationContext Create(string conversationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ConversationContext
        {
            ConversationId = conversationId,
            CreatedAt = now,
            LastUpdated = now,
            TurnCount = 0
        };
    }

    /// <summary>
    /// Updates context with a new product being discussed.
    /// </summary>
    public void SetCurrentProduct(ProductDesignation product)
    {
        CurrentProduct = product;

        if (!_mentionedProducts.Contains(product))
        {
            _mentionedProducts.Add(product);
        }

        Touch();
    }

    /// <summary>
    /// Updates the last attribute discussed.
    /// </summary>
    public void SetLastAttribute(string attributeName)
    {
        LastAttribute = attributeName;
        Touch();
    }

    /// <summary>
    /// Updates the last classified intent.
    /// </summary>
    public void SetLastIntent(IntentType intent)
    {
        LastIntent = intent;
        Touch();
    }

    /// <summary>
    /// Records a new turn in the conversation.
    /// </summary>
    public void IncrementTurn()
    {
        TurnCount++;
        Touch();
    }

    /// <summary>
    /// Clears the current product context (e.g., when user switches topics).
    /// </summary>
    public void ClearCurrentProduct()
    {
        CurrentProduct = null;
        LastAttribute = null;
        Touch();
    }

    /// <summary>
    /// Updates the last interaction timestamp.
    /// </summary>
    private void Touch()
    {
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks if the conversation has been idle longer than the specified duration.
    /// Used for session expiry decisions.
    /// </summary>
    public bool IsExpired(TimeSpan maxIdleTime)
    {
        return DateTimeOffset.UtcNow - LastUpdated > maxIdleTime;
    }

    public override string ToString() =>
        $"Conversation [{ConversationId}] - Product: {CurrentProduct?.ToString() ?? "None"}, Turns: {TurnCount}";
}
