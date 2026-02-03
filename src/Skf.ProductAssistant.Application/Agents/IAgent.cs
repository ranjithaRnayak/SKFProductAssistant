using Skf.ProductAssistant.Application.DTOs;
using Skf.ProductAssistant.Domain.Entities;

namespace Skf.ProductAssistant.Application.Agents;

/// <summary>
/// Interface for AI agents that handle specific types of user requests.
/// Each agent specializes in a particular intent type.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// The name of this agent for logging and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes a user request and generates a response.
    /// </summary>
    /// <param name="request">The user's chat request.</param>
    /// <param name="context">The conversation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chat response.</returns>
    Task<ChatResponse> ProcessAsync(
        ChatRequest request,
        ConversationContext context,
        CancellationToken cancellationToken = default);
}
