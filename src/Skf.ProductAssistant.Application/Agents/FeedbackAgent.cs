using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Skf.ProductAssistant.Application.DTOs;
using Skf.ProductAssistant.Application.Plugins;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Enums;
using Skf.ProductAssistant.Infrastructure.Configuration;
using Skf.ProductAssistant.Infrastructure.SemanticKernel;

namespace Skf.ProductAssistant.Application.Agents;

/// <summary>
/// Agent for handling user feedback and corrections.
/// Captures feedback for later review and datasheet improvement.
/// </summary>
/// <remarks>
/// The Feedback Agent:
/// 1. Receives user corrections about product information
/// 2. Extracts structured feedback (product, attribute, original/correct values)
/// 3. Stores feedback for review
/// 4. Acknowledges receipt to the user
/// </remarks>
public sealed class FeedbackAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly PromptOptions _prompts;
    private readonly FeedbackPlugin _feedbackPlugin;
    private readonly StatePlugin _statePlugin;
    private readonly ProductNormalizationService _normalization;
    private readonly ILogger<FeedbackAgent> _logger;

    public string Name => "FeedbackAgent";

    public FeedbackAgent(
        KernelFactory kernelFactory,
        IOptions<PromptOptions> prompts,
        FeedbackPlugin feedbackPlugin,
        StatePlugin statePlugin,
        ProductNormalizationService normalization,
        ILogger<FeedbackAgent> logger)
    {
        _kernel = kernelFactory.CreateKernel();
        _prompts = prompts.Value;
        _feedbackPlugin = feedbackPlugin;
        _statePlugin = statePlugin;
        _normalization = normalization;
        _logger = logger;

        // Register plugins
        _kernel.ImportPluginFromObject(_feedbackPlugin, "feedback");
        _kernel.ImportPluginFromObject(_statePlugin, "state");
    }

    public async Task<ChatResponse> ProcessAsync(
        ChatRequest request,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "FeedbackAgent processing request for conversation {ConversationId}",
            context.ConversationId);

        // Set context for plugins
        _statePlugin.SetContext(context);
        _feedbackPlugin.SetConversationId(context.ConversationId);

        // Extract product from message or context
        var product = _normalization.ExtractDesignationFromMessage(request.Message)
                      ?? context.CurrentProduct;

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage(_prompts.FeedbackAgent.SystemPrompt);
            history.AddUserMessage(BuildFeedbackPrompt(request.Message, context, product?.Original));

            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 300,
                    ["temperature"] = 0.0
                },
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var response = await chatService.GetChatMessageContentAsync(
                history,
                settings,
                _kernel,
                cancellationToken);

            var answer = response.Content ?? _prompts.FeedbackAgent.AcknowledgmentTemplate;

            stopwatch.Stop();

            // Extract feedback ID from the response if function was called
            var feedbackId = ExtractFeedbackId(answer);

            return new ChatResponse
            {
                Answer = FormatAcknowledgment(answer),
                ConversationId = context.ConversationId,
                Intent = IntentType.Feedback,
                ProductDesignation = product?.Original,
                IsFromDatasheet = false,
                Metadata = new ResponseMetadata
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    AgentUsed = Name,
                    FeedbackId = feedbackId,
                    TurnNumber = context.TurnCount + 1
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FeedbackAgent failed to process request");

            return new ChatResponse
            {
                Answer = "Thank you for your feedback. I've noted your correction, though I encountered an issue recording the details. " +
                         "Our team will review this.",
                ConversationId = context.ConversationId,
                Intent = IntentType.Feedback,
                IsFromDatasheet = false,
                Warning = "Feedback may not have been fully recorded."
            };
        }
    }

    private string BuildFeedbackPrompt(string message, ConversationContext context, string? product)
    {
        var contextInfo = new List<string>();

        if (!string.IsNullOrEmpty(product))
        {
            contextInfo.Add($"Product being discussed: {product}");
        }

        if (!string.IsNullOrEmpty(context.LastAttribute))
        {
            contextInfo.Add($"Last attribute discussed: {context.LastAttribute}");
        }

        var contextString = contextInfo.Count > 0
            ? $"\n\nContext:\n{string.Join("\n", contextInfo)}"
            : "";

        return $"User feedback: {message}{contextString}\n\nPlease extract and store this feedback, then acknowledge receipt.";
    }

    private static string? ExtractFeedbackId(string response)
    {
        // Look for feedback ID pattern in response
        if (response.Contains("fb_"))
        {
            var start = response.IndexOf("fb_", StringComparison.Ordinal);
            var end = start + 20; // fb_ + 17 chars
            if (end <= response.Length)
            {
                return response.Substring(start, 20);
            }
        }

        return null;
    }

    private string FormatAcknowledgment(string response)
    {
        // Ensure the response is user-friendly
        if (response.Contains("FEEDBACK_DISABLED"))
        {
            return "Thank you for your feedback. While feedback recording is currently disabled, " +
                   "we appreciate you bringing this to our attention.";
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            return _prompts.FeedbackAgent.AcknowledgmentTemplate;
        }

        return response;
    }
}
