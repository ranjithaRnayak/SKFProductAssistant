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

public sealed class QnaAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly PromptOptions _prompts;
    private readonly FeatureFlags _featureFlags;
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
        _hallucinationGuard = hallucinationGuard;
        _normalization = normalization;
        _logger = logger;

        _kernel.ImportPluginFromObject(datasheetPlugin, "datasheet");
        _kernel.ImportPluginFromObject(statePlugin, "state");
        _kernel.ImportPluginFromObject(cachePlugin, "cache");
    }

    public async Task<ChatResponse> ProcessAsync(
        ChatRequest request,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("QnaAgent processing for conversation {ConversationId}", context.ConversationId);

        var product = _normalization.ExtractDesignationFromMessage(request.Message) ?? context.CurrentProduct;

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(_prompts.QnaAgent.SystemPrompt);
            history.AddUserMessage(BuildUserMessage(request.Message, context, product?.Original));

            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 500,
                    ["temperature"] = 0.0
                },
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var response = await chatService.GetChatMessageContentAsync(history, settings, _kernel, cancellationToken);
            var answer = response.Content ?? string.Empty;

            if (_featureFlags.StrictHallucinationPrevention)
            {
                var validation = await _hallucinationGuard.ValidateResponseAsync(
                    answer, request.Message, product?.Original, cancellationToken);

                if (!validation.IsValid)
                {
                    _logger.LogWarning("Hallucination check failed: {Reason}", validation.Reason);
                    return ChatResponse.NotFound(_prompts.QnaAgent.NotFoundResponse, context.ConversationId, product?.Original);
                }
            }

            stopwatch.Stop();
            var isFromDatasheet = !answer.Contains("NOT_FOUND") && !answer.Contains("don't have");

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
            _logger.LogError(ex, "QnaAgent failed");
            return new ChatResponse
            {
                Answer = "I encountered an error processing your request. Please try again.",
                ConversationId = context.ConversationId,
                Intent = IntentType.Question,
                IsFromDatasheet = false,
                Warning = "Processing error occurred."
            };
        }
    }

    private string BuildUserMessage(string message, ConversationContext context, string? product)
    {
        return _prompts.QnaAgent.UserTemplate
            .Replace("{question}", message)
            .Replace("{product}", product ?? "not specified")
            .Replace("{context}", BuildContextString(context));
    }

    private static string BuildContextString(ConversationContext context)
    {
        var parts = new List<string>();
        if (context.CurrentProduct is not null) parts.Add($"Current product: {context.CurrentProduct}");
        if (!string.IsNullOrEmpty(context.LastAttribute)) parts.Add($"Last attribute: {context.LastAttribute}");
        if (context.TurnCount > 0) parts.Add($"Turn: {context.TurnCount + 1}");
        return parts.Count > 0 ? string.Join("; ", parts) : "New conversation";
    }
}
