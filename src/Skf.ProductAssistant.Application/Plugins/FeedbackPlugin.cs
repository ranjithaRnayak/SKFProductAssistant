using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Application.Plugins;

/// <summary>
/// Semantic Kernel plugin for capturing user feedback and corrections.
/// Enables the system to learn from user corrections.
/// </summary>
/// <remarks>
/// Feedback is stored for later review and potential datasheet updates.
/// This creates a feedback loop for continuous improvement.
/// </remarks>
public sealed class FeedbackPlugin
{
    private readonly IFeedbackRepository _repository;
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<FeedbackPlugin> _logger;
    private string _currentConversationId = string.Empty;

    public FeedbackPlugin(
        IFeedbackRepository repository,
        IOptions<FeatureFlags> featureFlags,
        ILogger<FeedbackPlugin> logger)
    {
        _repository = repository;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sets the current conversation ID for feedback tracking.
    /// </summary>
    public void SetConversationId(string conversationId)
    {
        _currentConversationId = conversationId;
    }

    /// <summary>
    /// Stores user feedback about incorrect information.
    /// </summary>
    [KernelFunction("store_feedback")]
    [Description("Stores user feedback or correction about product information. Returns the feedback ID for reference.")]
    public async Task<string> StoreFeedbackAsync(
        [Description("The user's feedback message")] string feedback,
        [Description("The product designation the feedback is about (optional)")] string? productDesignation = null,
        [Description("The specific attribute being corrected (optional)")] string? attribute = null,
        [Description("The original (incorrect) value if applicable")] string? originalValue = null,
        [Description("The corrected value if applicable")] string? correctedValue = null)
    {
        if (!_featureFlags.EnableFeedbackCapture)
        {
            _logger.LogDebug("Feedback capture is disabled");
            return "FEEDBACK_DISABLED";
        }

        var entry = FeedbackEntry.Create(
            conversationId: _currentConversationId,
            userMessage: feedback,
            productDesignation: productDesignation,
            attributeName: attribute,
            originalValue: originalValue,
            correctedValue: correctedValue);

        await _repository.SaveAsync(entry);

        _logger.LogInformation(
            "Feedback stored: {FeedbackId} for product {Product}",
            entry.FeedbackId,
            productDesignation ?? "N/A");

        return entry.FeedbackId;
    }

    /// <summary>
    /// Gets the count of pending feedback for review.
    /// </summary>
    [KernelFunction("get_pending_feedback_count")]
    [Description("Gets the number of feedback entries pending review.")]
    public async Task<int> GetPendingFeedbackCountAsync()
    {
        if (!_featureFlags.EnableFeedbackCapture)
        {
            return 0;
        }

        return await _repository.GetPendingCountAsync();
    }

    /// <summary>
    /// Checks if feedback exists for a product.
    /// </summary>
    [KernelFunction("has_feedback_for_product")]
    [Description("Checks if there's any feedback recorded for a specific product. Returns 'true' or 'false'.")]
    public async Task<string> HasFeedbackForProductAsync(
        [Description("The product designation to check")] string productDesignation)
    {
        if (!_featureFlags.EnableFeedbackCapture)
        {
            return "false";
        }

        var feedback = await _repository.GetByProductAsync(productDesignation);
        return feedback.Count > 0 ? "true" : "false";
    }
}
