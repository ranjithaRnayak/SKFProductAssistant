using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Entities;

namespace Skf.ProductAssistant.Application.Plugins;

/// <summary>
/// Semantic Kernel plugin for managing conversation state.
/// Enables context-aware multi-turn conversations.
/// </summary>
/// <remarks>
/// This plugin allows the agent to:
/// - Retrieve the current product being discussed
/// - Get the last attribute asked about
/// - Check conversation history
/// </remarks>
public sealed class StatePlugin
{
    private readonly ConversationStateManager _stateManager;
    private readonly ILogger<StatePlugin> _logger;
    private ConversationContext? _currentContext;

    public StatePlugin(
        ConversationStateManager stateManager,
        ILogger<StatePlugin> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    /// <summary>
    /// Sets the current conversation context for this request.
    /// Called by the orchestrator before agent execution.
    /// </summary>
    public void SetContext(ConversationContext context)
    {
        _currentContext = context;
    }

    /// <summary>
    /// Gets the current product being discussed.
    /// </summary>
    [KernelFunction("get_current_product")]
    [Description("Gets the product designation currently being discussed in this conversation. Returns 'NO_PRODUCT' if no product context exists.")]
    public string GetCurrentProduct()
    {
        if (_currentContext?.CurrentProduct is null)
        {
            _logger.LogDebug("No current product in context");
            return "NO_PRODUCT";
        }

        _logger.LogDebug("Current product: {Product}", _currentContext.CurrentProduct);
        return _currentContext.CurrentProduct.Original;
    }

    /// <summary>
    /// Gets the last attribute discussed.
    /// </summary>
    [KernelFunction("get_last_attribute")]
    [Description("Gets the last attribute that was discussed. Returns 'NO_ATTRIBUTE' if none.")]
    public string GetLastAttribute()
    {
        if (string.IsNullOrEmpty(_currentContext?.LastAttribute))
        {
            return "NO_ATTRIBUTE";
        }

        return _currentContext.LastAttribute;
    }

    /// <summary>
    /// Gets the conversation turn count.
    /// </summary>
    [KernelFunction("get_turn_count")]
    [Description("Gets the number of turns in the current conversation.")]
    public int GetTurnCount()
    {
        return _currentContext?.TurnCount ?? 0;
    }

    /// <summary>
    /// Gets all products mentioned in this conversation.
    /// </summary>
    [KernelFunction("get_mentioned_products")]
    [Description("Gets all products mentioned during this conversation. Returns comma-separated list or 'NONE'.")]
    public string GetMentionedProducts()
    {
        if (_currentContext?.MentionedProducts.Count == 0)
        {
            return "NONE";
        }

        var products = _currentContext!.MentionedProducts.Select(p => p.Original);
        return string.Join(", ", products);
    }

    /// <summary>
    /// Checks if user might be referring to a previous product.
    /// </summary>
    [KernelFunction("has_product_context")]
    [Description("Checks if there's a product from previous messages that the user might be referring to. Returns 'true' or 'false'.")]
    public string HasProductContext()
    {
        return _currentContext?.CurrentProduct is not null ? "true" : "false";
    }
}
