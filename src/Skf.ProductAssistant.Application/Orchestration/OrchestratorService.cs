using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Application.Agents;
using Skf.ProductAssistant.Application.DTOs;
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
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(
        IntentClassifierService intentClassifier,
        ConversationStateManager stateManager,
        ProductNormalizationService normalizationService,
        QnaAgent qnaAgent,
        FeedbackAgent feedbackAgent,
        IOptions<FeatureFlags> featureFlags,
        ILogger<OrchestratorService> logger)
    {
        _intentClassifier = intentClassifier;
        _stateManager = stateManager;
        _normalizationService = normalizationService;
        _qnaAgent = qnaAgent;
        _feedbackAgent = feedbackAgent;
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
                cancellationToken);

            _logger.LogDebug("Classified intent: {Intent}", intent);

            // Step 3: Extract and normalize product designation if present
            var extractedProduct = _normalizationService.ExtractDesignationFromMessage(request.Message);
            var resolvedProduct = _stateManager.ResolveProduct(context, extractedProduct);
            var extractedAttribute = _normalizationService.ExtractAttributeFromMessage(request.Message);

            // Step 4: Route to appropriate agent based on intent
            var response = intent switch
            {
                IntentType.Question => await HandleQuestionAsync(
                    request,
                    context,
                    cancellationToken),

                IntentType.Feedback => await HandleFeedbackAsync(
                    request,
                    context,
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
                extractedAttribute,
                cancellationToken);

            // Step 6: Add metadata
            stopwatch.Stop();
            return MergeResponseMetadata(
                response,
                intent,
                context.TurnCount,
                stopwatch.ElapsedMilliseconds);
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
        ChatRequest request,
        Domain.Entities.ConversationContext context,
        CancellationToken cancellationToken)
    {
        return await _qnaAgent.ProcessAsync(request, context, cancellationToken);
    }

    private async Task<ChatResponse> HandleFeedbackAsync(
        ChatRequest request,
        Domain.Entities.ConversationContext context,
        CancellationToken cancellationToken)
    {
        return await _feedbackAgent.ProcessAsync(request, context, cancellationToken);
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

    private static string GetAgentName(IntentType intent) => intent switch
    {
        IntentType.Question => "QnaAgent",
        IntentType.Feedback => "FeedbackAgent",
        _ => "Orchestrator"
    };

    private static ChatResponse MergeResponseMetadata(
        ChatResponse response,
        IntentType intent,
        int turnNumber,
        long processingTimeMs)
    {
        var mergedMetadata = response.Metadata is null
            ? new ResponseMetadata
            {
                ProcessingTimeMs = processingTimeMs,
                AgentUsed = GetAgentName(intent),
                TurnNumber = turnNumber
            }
            : new ResponseMetadata
            {
                ProcessingTimeMs = response.Metadata.ProcessingTimeMs == 0
                    ? processingTimeMs
                    : response.Metadata.ProcessingTimeMs,
                AgentUsed = string.IsNullOrWhiteSpace(response.Metadata.AgentUsed)
                    ? GetAgentName(intent)
                    : response.Metadata.AgentUsed,
                FunctionCallCount = response.Metadata.FunctionCallCount,
                FeedbackId = response.Metadata.FeedbackId,
                TurnNumber = response.Metadata.TurnNumber == 0
                    ? turnNumber
                    : response.Metadata.TurnNumber
            };

        return new ChatResponse
        {
            Answer = response.Answer,
            ConversationId = response.ConversationId,
            Intent = intent,
            ProductDesignation = response.ProductDesignation,
            IsFromDatasheet = response.IsFromDatasheet,
            Warning = response.Warning,
            Metadata = mergedMetadata
        };
    }
}
