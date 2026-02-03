using Skf.ProductAssistant.Domain.Enums;

namespace Skf.ProductAssistant.Application.DTOs;

/// <summary>
/// Response model for the chat endpoint.
/// Contains the assistant's answer and metadata about the response.
/// </summary>
public sealed class ChatResponse
{
    /// <summary>
    /// The assistant's response to the user's message.
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// The conversation ID for maintaining context.
    /// Same as request if provided, or newly generated.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// The classified intent of the user's message.
    /// </summary>
    public IntentType Intent { get; init; }

    /// <summary>
    /// The product designation discussed in this turn, if any.
    /// </summary>
    public string? ProductDesignation { get; init; }

    /// <summary>
    /// Indicates whether the response came from datasheet data.
    /// False if the assistant abstained due to missing data.
    /// </summary>
    public bool IsFromDatasheet { get; init; }

    /// <summary>
    /// Any warnings or notes about the response.
    /// For example, "Data not found for requested attribute."
    /// </summary>
    public string? Warning { get; init; }

    /// <summary>
    /// Processing metadata for debugging and analytics.
    /// </summary>
    public ResponseMetadata? Metadata { get; init; }

    /// <summary>
    /// Creates a successful response with data from datasheet.
    /// </summary>
    public static ChatResponse FromDatasheet(
        string answer,
        string conversationId,
        IntentType intent,
        string? productDesignation = null)
    {
        return new ChatResponse
        {
            Answer = answer,
            ConversationId = conversationId,
            Intent = intent,
            ProductDesignation = productDesignation,
            IsFromDatasheet = true
        };
    }

    /// <summary>
    /// Creates a response when data is not found.
    /// </summary>
    public static ChatResponse NotFound(
        string message,
        string conversationId,
        string? productDesignation = null)
    {
        return new ChatResponse
        {
            Answer = message,
            ConversationId = conversationId,
            Intent = IntentType.Question,
            ProductDesignation = productDesignation,
            IsFromDatasheet = false,
            Warning = "Requested data not found in datasheets."
        };
    }

    /// <summary>
    /// Creates a response for feedback acknowledgment.
    /// </summary>
    public static ChatResponse FeedbackReceived(
        string message,
        string conversationId,
        string feedbackId)
    {
        return new ChatResponse
        {
            Answer = message,
            ConversationId = conversationId,
            Intent = IntentType.Feedback,
            IsFromDatasheet = false,
            Metadata = new ResponseMetadata { FeedbackId = feedbackId }
        };
    }
}

/// <summary>
/// Metadata about response processing.
/// </summary>
public sealed class ResponseMetadata
{
    /// <summary>
    /// Processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Which agent handled the request.
    /// </summary>
    public string? AgentUsed { get; init; }

    /// <summary>
    /// Number of function calls made by the agent.
    /// </summary>
    public int FunctionCallCount { get; init; }

    /// <summary>
    /// ID of feedback entry if feedback was captured.
    /// </summary>
    public string? FeedbackId { get; init; }

    /// <summary>
    /// Turn number in the conversation.
    /// </summary>
    public int TurnNumber { get; init; }
}
