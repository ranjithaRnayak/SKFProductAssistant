using System.ComponentModel.DataAnnotations;

namespace Skf.ProductAssistant.Application.DTOs;

/// <summary>
/// Request model for the chat endpoint.
/// Represents a single user message in a conversation.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>
    /// Unique identifier for the conversation.
    /// Used to maintain context across multiple turns.
    /// If not provided, a new conversation is started.
    /// </summary>
    /// <example>conv_abc123</example>
    public string? ConversationId { get; init; }

    /// <summary>
    /// The user's message or question.
    /// </summary>
    /// <example>What is the bore diameter of bearing 6205-2RS?</example>
    [Required(ErrorMessage = "Message is required")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
    public required string Message { get; init; }

    /// <summary>
    /// Optional user identifier for analytics and personalization.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Optional locale for response formatting (e.g., "en-US", "de-DE").
    /// Affects number and unit formatting in responses.
    /// </summary>
    public string? Locale { get; init; }
}
