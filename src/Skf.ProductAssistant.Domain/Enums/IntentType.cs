namespace Skf.ProductAssistant.Domain.Enums;

/// <summary>
/// Classifies the user's intent to route to the appropriate agent.
/// Used by IntentClassifierService to determine processing flow.
/// </summary>
public enum IntentType
{
    /// <summary>
    /// User is asking a question about product specifications or attributes.
    /// Routes to QnaAgent for datasheet lookup.
    /// </summary>
    Question = 0,

    /// <summary>
    /// User is providing feedback or correction about previous answers.
    /// Routes to FeedbackAgent for capture and acknowledgment.
    /// </summary>
    Feedback = 1,

    /// <summary>
    /// User intent could not be determined with confidence.
    /// Orchestrator should request clarification.
    /// </summary>
    Unknown = 2,

    /// <summary>
    /// User is engaging in casual conversation (greetings, thanks, etc.).
    /// Can be handled with simple acknowledgment without agent routing.
    /// </summary>
    Conversational = 3,

    /// <summary>
    /// User is requesting help or asking what the assistant can do.
    /// Responds with capability summary without datasheet lookup.
    /// </summary>
    Help = 4
}
