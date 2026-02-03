namespace Skf.ProductAssistant.Infrastructure.Configuration;

public sealed class FeatureFlags
{
    public const string SectionName = "FeatureFlags";

    public bool UseRedis { get; init; } = false;
    public bool EnableDetailedLogging { get; init; } = false;
    public bool StrictHallucinationPrevention { get; init; } = true;
    public bool EnableDatasheetCaching { get; init; } = true;
    public bool EnableConversationState { get; init; } = true;
    public bool EnableFeedbackCapture { get; init; } = true;
    public bool EnableSemanticSearch { get; init; } = false;
    public int MaxSearchResults { get; init; } = 10;
}
