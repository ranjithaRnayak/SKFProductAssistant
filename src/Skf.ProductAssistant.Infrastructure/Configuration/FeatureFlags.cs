namespace Skf.ProductAssistant.Infrastructure.Configuration;

/// <summary>
/// Feature flags for controlling application behavior.
/// Bound from appsettings.json "FeatureFlags" section.
/// Enables gradual rollout and environment-specific configurations.
/// </summary>
public sealed class FeatureFlags
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// When true, uses Redis for caching and state storage.
    /// When false, uses in-memory implementations (suitable for single-instance deployments).
    /// </summary>
    public bool UseRedis { get; init; } = false;

    /// <summary>
    /// When true, enables detailed logging including prompts and responses.
    /// Disable in production to avoid logging sensitive data.
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;

    /// <summary>
    /// When true, strictly validates that all responses come from datasheet data.
    /// Agent must explicitly abstain if data is not found.
    /// </summary>
    public bool StrictHallucinationPrevention { get; init; } = true;

    /// <summary>
    /// When true, enables caching of datasheet lookups.
    /// Improves performance but may serve stale data if datasheets change.
    /// </summary>
    public bool EnableDatasheetCaching { get; init; } = true;

    /// <summary>
    /// When true, tracks conversation state across turns.
    /// When false, each request is treated independently.
    /// </summary>
    public bool EnableConversationState { get; init; } = true;

    /// <summary>
    /// When true, stores user feedback for review.
    /// Useful for improving the system based on corrections.
    /// </summary>
    public bool EnableFeedbackCapture { get; init; } = true;

    /// <summary>
    /// When true, uses semantic search for finding relevant products.
    /// When false, uses exact match on designation.
    /// </summary>
    public bool EnableSemanticSearch { get; init; } = false;

    /// <summary>
    /// Maximum number of products to return in search results.
    /// </summary>
    public int MaxSearchResults { get; init; } = 10;
}
