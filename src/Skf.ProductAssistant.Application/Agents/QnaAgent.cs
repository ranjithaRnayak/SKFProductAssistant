using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Skf.ProductAssistant.Application.DTOs;
using Skf.ProductAssistant.Application.Guards;
using Skf.ProductAssistant.Application.Plugins;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Enums;
using Skf.ProductAssistant.Infrastructure.Configuration;
using Skf.ProductAssistant.Infrastructure.SemanticKernel;

namespace Skf.ProductAssistant.Application.Agents;

/// <summary>
/// Agent for handling product Q&A using Semantic Kernel with function calling.
/// This agent ONLY answers from datasheet data - never hallucinates.
/// </summary>
/// <remarks>
/// The QnA Agent:
/// 1. Receives user questions about product specifications
/// 2. Uses DatasheetPlugin to look up actual data
/// 3. Uses HallucinationGuard to validate responses
/// 4. Returns factual answers or explicitly abstains if data not found
/// </remarks>
public sealed class QnaAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly PromptOptions _prompts;
    private readonly FeatureFlags _featureFlags;
    private readonly DatasheetPlugin _datasheetPlugin;
    private readonly StatePlugin _statePlugin;
    private readonly CachePlugin _cachePlugin;
    private readonly HallucinationGuard _hallucinationGuard;
    private readonly ProductNormalizationService _normalization;
    private readonly ILogger<QnaAgent> _logger;

    public string Name => "QnaAgent";

    public QnaAgent(
        KernelFactory kernelFactory,
        IOptions<PromptOptions> prompts,
        IOptions<FeatureFlags> featureFlags,
        DatasheetPlugin datasheetPlugin,
        StatePlugin statePlugin,
        CachePlugin cachePlugin,
        HallucinationGuard hallucinationGuard,
        ProductNormalizationService normalization,
        ILogger<QnaAgent> logger)
    {
        _kernel = kernelFactory.CreateKernel();
        _prompts = prompts.Value;
        _featureFlags = featureFlags.Value;
        _datasheetPlugin = datasheetPlugin;
        _statePlugin = statePlugin;
        _cachePlugin = cachePlugin;
        _hallucinationGuard = hallucinationGuard;
        _normalization = normalization;
        _logger = logger;

        // Register plugins with the kernel
        _kernel.ImportPluginFromObject(_datasheetPlugin, "datasheet");
        _kernel.ImportPluginFromObject(_statePlugin, "state");
        _kernel.ImportPluginFromObject(_cachePlugin, "cache");
    }

    public async Task<ChatResponse> ProcessAsync(
        ChatRequest request,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "QnaAgent processing request for conversation {ConversationId}",
            context.ConversationId);

        // Set context for plugins
        _statePlugin.SetContext(context);

        // Extract product and attribute from message
        var product = _normalization.ExtractDesignationFromMessage(request.Message)
                      ?? context.CurrentProduct;
        var attribute = _normalization.ExtractAttributeFromMessage(request.Message);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            // Build the chat history with system prompt and user message
            var history = new ChatHistory();
            history.AddSystemMessage(_prompts.QnaAgent.SystemPrompt);

            // Build context-aware user message
            var userMessage = BuildUserMessage(request.Message, context, product?.Original);
            history.AddUserMessage(userMessage);

            // Execute with function calling enabled
            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 500,
                    ["temperature"] = 0.0
                },
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var response = await chatService.GetChatMessageContentAsync(
                history,
                settings,
                _kernel,
                cancellationToken);

            var answer = response.Content ?? string.Empty;

            // Validate response doesn't contain hallucinations
            if (_featureFlags.StrictHallucinationPrevention)
            {
                var validationResult = await _hallucinationGuard.ValidateResponseAsync(
                    answer,
                    request.Message,
                    product?.Original,
                    cancellationToken);

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning(
                        "Response failed hallucination check: {Reason}",
                        validationResult.Reason);

                    return ChatResponse.NotFound(
                        _prompts.QnaAgent.NotFoundResponse,
                        context.ConversationId,
                        product?.Original);
                }
            }

            stopwatch.Stop();

            var isFromDatasheet = !answer.Contains("NOT_FOUND") &&
                                  !answer.Contains("don't have") &&
                                  !answer.Contains("not available");

            return new ChatResponse
            {
                Answer = answer,
                ConversationId = context.ConversationId,
                Intent = IntentType.Question,
                ProductDesignation = product?.Original,
                IsFromDatasheet = isFromDatasheet,
                Metadata = new ResponseMetadata
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    AgentUsed = Name,
                    TurnNumber = context.TurnCount + 1
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QnaAgent failed to process request");

            return new ChatResponse
            {
                Answer = "I apologize, but I encountered an error processing your request. Please try again.",
                ConversationId = context.ConversationId,
                Intent = IntentType.Question,
                IsFromDatasheet = false,
                Warning = "An error occurred during processing."
            };
        }
    }

    private string BuildUserMessage(string message, ConversationContext context, string? product)
    {
        var template = _prompts.QnaAgent.UserTemplate;

        // Replace placeholders
        var result = template
            .Replace("{question}", message)
            .Replace("{product}", product ?? "not specified")
            .Replace("{context}", BuildContextString(context));

        return result;
    }

    private static string BuildContextString(ConversationContext context)
    {
        var parts = new List<string>();

        if (context.CurrentProduct is not null)
        {
            parts.Add($"Current product: {context.CurrentProduct}");
        }

        if (!string.IsNullOrEmpty(context.LastAttribute))
        {
            parts.Add($"Last attribute discussed: {context.LastAttribute}");
        }

        if (context.TurnCount > 0)
        {
            parts.Add($"Conversation turn: {context.TurnCount + 1}");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "New conversation";
    }
}
