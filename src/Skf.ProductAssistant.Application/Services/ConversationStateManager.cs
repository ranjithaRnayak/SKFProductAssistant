using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Enums;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Domain.ValueObjects;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Application.Services;

/// <summary>
/// Manages conversation state across multiple turns.
/// Handles context tracking for multi-turn conversations.
/// </summary>
/// <remarks>
/// This service enables:
/// - Pronoun resolution ("What is its weight?" → refers to current product)
/// - Follow-up questions ("What about the outer diameter?")
/// - Conversation history for context
/// </remarks>
public sealed class ConversationStateManager
{
    private readonly IConversationStateRepository _repository;
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<ConversationStateManager> _logger;

    public ConversationStateManager(
        IConversationStateRepository repository,
        IOptions<FeatureFlags> featureFlags,
        ILogger<ConversationStateManager> logger)
    {
        _repository = repository;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a conversation context.
    /// </summary>
    /// <param name="conversationId">The conversation ID. If null, generates a new one.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation context.</returns>
    public async Task<ConversationContext> GetOrCreateAsync(
        string? conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!_featureFlags.EnableConversationState)
        {
            // Return ephemeral context when state tracking is disabled
            return ConversationContext.Create(conversationId ?? GenerateConversationId());
        }

        var id = conversationId ?? GenerateConversationId();
        return await _repository.GetOrCreateAsync(id, cancellationToken);
    }

    /// <summary>
    /// Updates the conversation context after processing a message.
    /// </summary>
    public async Task UpdateContextAsync(
        ConversationContext context,
        IntentType intent,
        ProductDesignation? product,
        string? attribute,
        CancellationToken cancellationToken = default)
    {
        context.IncrementTurn();
        context.SetLastIntent(intent);

        if (product is not null)
        {
            context.SetCurrentProduct(product);
            _logger.LogDebug(
                "Updated current product to {Product} for conversation {ConversationId}",
                product,
                context.ConversationId);
        }

        if (!string.IsNullOrEmpty(attribute))
        {
            context.SetLastAttribute(attribute);
        }

        if (_featureFlags.EnableConversationState)
        {
            await _repository.SaveAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Resolves the product for a message using context if needed.
    /// </summary>
    /// <param name="context">The conversation context.</param>
    /// <param name="explicitProduct">Explicitly mentioned product, if any.</param>
    /// <returns>The resolved product designation.</returns>
    public ProductDesignation? ResolveProduct(
        ConversationContext context,
        ProductDesignation? explicitProduct)
    {
        // Explicit mention takes precedence
        if (explicitProduct is not null)
        {
            return explicitProduct;
        }

        // Fall back to current product from context
        if (context.CurrentProduct is not null)
        {
            _logger.LogDebug(
                "Using product from context: {Product}",
                context.CurrentProduct);
            return context.CurrentProduct;
        }

        return null;
    }

    /// <summary>
    /// Resolves the attribute for a message using context if needed.
    /// </summary>
    public string? ResolveAttribute(
        ConversationContext context,
        string? explicitAttribute)
    {
        // Explicit mention takes precedence
        if (!string.IsNullOrEmpty(explicitAttribute))
        {
            return explicitAttribute;
        }

        // Fall back to last attribute from context
        return context.LastAttribute;
    }

    /// <summary>
    /// Generates a new conversation ID.
    /// </summary>
    private static string GenerateConversationId()
    {
        return $"conv_{Guid.NewGuid():N}"[..20];
    }

    /// <summary>
    /// Clears the current product context.
    /// Called when user explicitly changes topics.
    /// </summary>
    public async Task ClearProductContextAsync(
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        context.ClearCurrentProduct();

        if (_featureFlags.EnableConversationState)
        {
            await _repository.SaveAsync(context, cancellationToken);
        }

        _logger.LogDebug("Cleared product context for conversation {ConversationId}", context.ConversationId);
    }

    /// <summary>
    /// Gets conversation statistics for monitoring.
    /// </summary>
    public async Task<int> GetActiveConversationCountAsync(CancellationToken cancellationToken = default)
    {
        if (!_featureFlags.EnableConversationState)
        {
            return 0;
        }

        return await _repository.GetActiveCountAsync(cancellationToken);
    }
}
