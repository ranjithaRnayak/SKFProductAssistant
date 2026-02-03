using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Application.Agents;
using Skf.ProductAssistant.Application.DTOs;
using Skf.ProductAssistant.Application.Guards;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Enums;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Application.Orchestration;

public sealed class OrchestratorService
{
    private readonly IntentClassifierService _intentClassifier;
    private readonly ConversationStateManager _stateManager;
    private readonly ProductNormalizationService _normalizationService;
    private readonly QnaAgent _qnaAgent;
    private readonly FeedbackAgent _feedbackAgent;
    private readonly HallucinationGuard _hallucinationGuard;
    private readonly PromptOptions _prompts;
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(
        IntentClassifierService intentClassifier,
        ConversationStateManager stateManager,
        ProductNormalizationService normalizationService,
        QnaAgent qnaAgent,
        FeedbackAgent feedbackAgent,
        HallucinationGuard hallucinationGuard,
        IOptions<PromptOptions> prompts,
        IOptions<FeatureFlags> featureFlags,
        ILogger<OrchestratorService> logger)
    {
        _intentClassifier = intentClassifier;
        _stateManager = stateManager;
        _normalizationService = normalizationService;
        _qnaAgent = qnaAgent;
        _feedbackAgent = feedbackAgent;
        _hallucinationGuard = hallucinationGuard;
        _prompts = prompts.Value;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes a chat request and returns the response.
    /// </summary>
    public async Task<ChatResponse> ProcessAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Get or create conversation context
            var context = await _stateManager.GetOrCreateAsync(
                request.ConversationId,
                cancellationToken);

            _logger.LogDebug(
                "Processing message for conversation {ConversationId}, turn {Turn}",
                context.ConversationId,
                context.TurnCount + 1);

            // Step 2: Classify intent
            var intent = await _intentClassifier.ClassifyAsync(
                request.Message,
                context,
                cancellationToken);

            _logger.LogDebug("Classified intent: {Intent}", intent);

            // Step 3: Extract and normalize product designation if present
            var extractedProduct = _normalizationService.ExtractProductDesignation(request.Message);
            var resolvedProduct = _stateManager.ResolveProduct(context, extractedProduct);

            // Step 4: Route to appropriate agent based on intent
            var response = intent switch
            {
                IntentType.Question => await HandleQuestionAsync(
                    request.Message,
                    context,
                    resolvedProduct?.ToString(),
                    cancellationToken),

                IntentType.Feedback => await HandleFeedbackAsync(
                    request.Message,
                    context,
                    resolvedProduct?.ToString(),
                    cancellationToken),

                IntentType.Conversational => HandleConversational(request.Message, context.ConversationId),

                IntentType.Help => HandleHelp(context.ConversationId),

                _ => HandleUnknown(request.Message, context.ConversationId)
            };

            // Step 5: Update conversation state
            await _stateManager.UpdateContextAsync(
                context,
                intent,
                resolvedProduct,
                ExtractAttributeFromMessage(request.Message),
                cancellationToken);

            // Step 6: Add metadata
            stopwatch.Stop();
            return response with
            {
                Intent = intent,
                Metadata = new ResponseMetadata
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    AgentUsed = GetAgentName(intent),
                    TurnNumber = context.TurnCount
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            stopwatch.Stop();

            return new ChatResponse
            {
                Answer = "I encountered an error processing your request. Please try again.",
                ConversationId = request.ConversationId ?? "error",
                Intent = IntentType.Unknown,
                Warning = _featureFlags.EnableDetailedLogging ? ex.Message : null,
                Metadata = new ResponseMetadata
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                }
            };
        }
    }

    private async Task<ChatResponse> HandleQuestionAsync(
        string message,
        Domain.Entities.ConversationContext context,
        string? productDesignation,
        CancellationToken cancellationToken)
    {
        // Use QnA agent to answer
        var agentResponse = await _qnaAgent.ProcessAsync(
            message,
            context,
            productDesignation,
            cancellationToken);

        // Validate response for hallucination
        if (_featureFlags.StrictHallucinationPrevention)
        {
            var validation = await _hallucinationGuard.ValidateResponseAsync(
                agentResponse.Answer,
                message,
                productDesignation,
                cancellationToken);

            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Hallucination guard triggered: {Reason}",
                    validation.Reason);

                // Return abstention response instead
                return ChatResponse.NotFound(
                    _hallucinationGuard.GetAbstentionResponse(
                        productDesignation ?? "unknown",
                        ExtractAttributeFromMessage(message)),
                    context.ConversationId,
                    productDesignation);
            }
        }

        return agentResponse;
    }

    private async Task<ChatResponse> HandleFeedbackAsync(
        string message,
        Domain.Entities.ConversationContext context,
        string? productDesignation,
        CancellationToken cancellationToken)
    {
        return await _feedbackAgent.ProcessAsync(
            message,
            context,
            productDesignation,
            cancellationToken);
    }

    private ChatResponse HandleConversational(string message, string conversationId)
    {
        // Simple acknowledgment for greetings, thanks, etc.
        var lowerMessage = message.ToLowerInvariant();

        string response;
        if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi"))
        {
            response = "Hello! I'm the SKF Product Assistant. How can I help you with bearing information today?";
        }
        else if (lowerMessage.Contains("thank"))
        {
            response = "You're welcome! Let me know if you have any other questions about SKF products.";
        }
        else if (lowerMessage.Contains("bye") || lowerMessage.Contains("goodbye"))
        {
            response = "Goodbye! Feel free to return if you need more product information.";
        }
        else
        {
            response = "I'm here to help with SKF product information. What would you like to know?";
        }

        return new ChatResponse
        {
            Answer = response,
            ConversationId = conversationId,
            Intent = IntentType.Conversational,
            IsFromDatasheet = false
        };
    }

    private ChatResponse HandleHelp(string conversationId)
    {
        var response = """
            I'm the SKF Product Assistant. I can help you with:

            • **Product specifications** - Ask about dimensions, load ratings, speeds, etc.
            • **Product comparisons** - Compare specifications between products
            • **Technical attributes** - Bore diameter, outer diameter, width, weight, etc.

            Example questions:
            - "What is the bore diameter of 6205-2RS?"
            - "Tell me about bearing 6206-2Z"
            - "Compare 6205 and 6206"

            You can also provide feedback if you notice any incorrect information.
            """;

        return new ChatResponse
        {
            Answer = response,
            ConversationId = conversationId,
            Intent = IntentType.Help,
            IsFromDatasheet = false
        };
    }

    private ChatResponse HandleUnknown(string message, string conversationId)
    {
        return new ChatResponse
        {
            Answer = "I'm not sure I understand your request. Could you rephrase it? " +
                    "I can help with product specifications, comparisons, or capturing feedback.",
            ConversationId = conversationId,
            Intent = IntentType.Unknown,
            IsFromDatasheet = false
        };
    }

    private static string? ExtractAttributeFromMessage(string message)
    {
        // Simple extraction - in production, this would use NLP
        var attributes = new[]
        {
            "bore diameter", "outer diameter", "width", "weight",
            "dynamic load", "static load", "speed", "temperature"
        };

        var lower = message.ToLowerInvariant();
        return attributes.FirstOrDefault(a => lower.Contains(a));
    }

    private static string GetAgentName(IntentType intent) => intent switch
    {
        IntentType.Question => "QnaAgent",
        IntentType.Feedback => "FeedbackAgent",
        _ => "Orchestrator"
    };
}
