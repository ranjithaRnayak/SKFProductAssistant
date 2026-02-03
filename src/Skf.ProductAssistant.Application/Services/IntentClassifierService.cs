using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Skf.ProductAssistant.Domain.Enums;
using Skf.ProductAssistant.Infrastructure.Configuration;
using Skf.ProductAssistant.Infrastructure.SemanticKernel;

namespace Skf.ProductAssistant.Application.Services;

/// <summary>
/// Service for classifying user intent using Semantic Kernel.
/// Determines whether the user is asking a question, providing feedback, etc.
/// </summary>
/// <remarks>
/// Intent classification is the first step in request processing.
/// It routes the request to the appropriate agent (QnA or Feedback).
/// Uses low temperature for consistent classification results.
/// </remarks>
public sealed class IntentClassifierService
{
    private readonly Kernel _kernel;
    private readonly PromptOptions _prompts;
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<IntentClassifierService> _logger;

    public IntentClassifierService(
        KernelFactory kernelFactory,
        IOptions<PromptOptions> prompts,
        IOptions<FeatureFlags> featureFlags,
        ILogger<IntentClassifierService> logger)
    {
        _kernel = kernelFactory.CreateKernel();
        _prompts = prompts.Value;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Classifies the user's intent from their message.
    /// </summary>
    /// <param name="message">The user's message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The classified intent type.</returns>
    public async Task<IntentType> ClassifyAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return IntentType.Unknown;
        }

        // Quick pattern matching for common cases to save API calls
        var quickResult = QuickClassify(message);
        if (quickResult.HasValue)
        {
            _logger.LogDebug("Quick classified intent: {Intent}", quickResult.Value);
            return quickResult.Value;
        }

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage(_prompts.IntentClassifier.SystemPrompt);

            var userPrompt = _prompts.IntentClassifier.ClassificationTemplate
                .Replace("{message}", message);
            history.AddUserMessage(userPrompt);

            var response = await chatService.GetChatMessageContentAsync(
                history,
                new PromptExecutionSettings
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["max_tokens"] = 50,
                        ["temperature"] = 0.0
                    }
                },
                cancellationToken: cancellationToken);

            var result = ParseIntentResponse(response.Content ?? string.Empty);

            if (_featureFlags.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Classified message '{Message}' as {Intent}",
                    message.Length > 50 ? message[..50] + "..." : message,
                    result);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification failed, defaulting to Question");
            return IntentType.Question;
        }
    }

    /// <summary>
    /// Quick pattern-based classification for common cases.
    /// Avoids unnecessary API calls for obvious patterns.
    /// </summary>
    private static IntentType? QuickClassify(string message)
    {
        var lower = message.ToLowerInvariant().Trim();

        // Greetings
        if (IsGreeting(lower))
        {
            return IntentType.Conversational;
        }

        // Help requests
        if (IsHelpRequest(lower))
        {
            return IntentType.Help;
        }

        // Feedback indicators
        if (IsFeedback(lower))
        {
            return IntentType.Feedback;
        }

        return null;
    }

    private static bool IsGreeting(string message)
    {
        var greetings = new[] { "hi", "hello", "hey", "good morning", "good afternoon", "good evening", "thanks", "thank you" };
        return greetings.Any(g => message.StartsWith(g) && message.Length < 30);
    }

    private static bool IsHelpRequest(string message)
    {
        var helpPatterns = new[] { "help", "what can you do", "how do i", "how does this work" };
        return helpPatterns.Any(message.Contains);
    }

    private static bool IsFeedback(string message)
    {
        var feedbackPatterns = new[] { "that's wrong", "that is wrong", "incorrect", "actually it's", "the correct", "you made a mistake", "that's not right" };
        return feedbackPatterns.Any(message.Contains);
    }

    private static IntentType ParseIntentResponse(string response)
    {
        var normalized = response.Trim().ToUpperInvariant();

        return normalized switch
        {
            var s when s.Contains("QUESTION") => IntentType.Question,
            var s when s.Contains("FEEDBACK") => IntentType.Feedback,
            var s when s.Contains("CONVERSATIONAL") => IntentType.Conversational,
            var s when s.Contains("HELP") => IntentType.Help,
            _ => IntentType.Unknown
        };
    }
}
