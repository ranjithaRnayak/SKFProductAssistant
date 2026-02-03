using System.ComponentModel.DataAnnotations;

namespace Skf.ProductAssistant.Infrastructure.Configuration;

/// <summary>
/// Configuration options for AI prompts used by agents and services.
/// Bound from appsettings.json "Prompts" section.
/// All prompts are externalized for easy tuning without code changes.
/// </summary>
public sealed class PromptOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Prompts";

    /// <summary>
    /// Prompts for the Orchestrator that routes requests.
    /// </summary>
    [Required]
    public required OrchestratorPrompts Orchestrator { get; init; }

    /// <summary>
    /// Prompts for the QnA Agent that answers product questions.
    /// </summary>
    [Required]
    public required QnaAgentPrompts QnaAgent { get; init; }

    /// <summary>
    /// Prompts for the Feedback Agent that captures corrections.
    /// </summary>
    [Required]
    public required FeedbackAgentPrompts FeedbackAgent { get; init; }

    /// <summary>
    /// Prompts for intent classification.
    /// </summary>
    [Required]
    public required IntentClassifierPrompts IntentClassifier { get; init; }
}

/// <summary>
/// Prompts for the Orchestrator service.
/// </summary>
public sealed class OrchestratorPrompts
{
    /// <summary>
    /// System prompt establishing the orchestrator's role.
    /// </summary>
    [Required]
    public required string SystemPrompt { get; init; }
}

/// <summary>
/// Prompts for the QnA Agent.
/// </summary>
public sealed class QnaAgentPrompts
{
    /// <summary>
    /// System prompt establishing the agent's role and constraints.
    /// Must include instructions to use only datasheet data.
    /// </summary>
    [Required]
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Template for user messages. Placeholders: {question}, {context}, {product}.
    /// </summary>
    [Required]
    public required string UserTemplate { get; init; }

    /// <summary>
    /// Response when product data is not found.
    /// </summary>
    [Required]
    public required string NotFoundResponse { get; init; }

    /// <summary>
    /// Response when attribute is not available for a product.
    /// </summary>
    [Required]
    public required string AttributeNotAvailableResponse { get; init; }
}

/// <summary>
/// Prompts for the Feedback Agent.
/// </summary>
public sealed class FeedbackAgentPrompts
{
    /// <summary>
    /// System prompt establishing the feedback agent's role.
    /// </summary>
    [Required]
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Template for acknowledging feedback receipt.
    /// </summary>
    [Required]
    public required string AcknowledgmentTemplate { get; init; }
}

/// <summary>
/// Prompts for intent classification.
/// </summary>
public sealed class IntentClassifierPrompts
{
    /// <summary>
    /// System prompt for intent classification.
    /// </summary>
    [Required]
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Template for classification request. Placeholder: {message}.
    /// </summary>
    [Required]
    public required string ClassificationTemplate { get; init; }
}
