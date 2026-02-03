using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Infrastructure.SemanticKernel;

/// <summary>
/// Factory for creating configured Semantic Kernel instances.
/// Centralizes kernel configuration and plugin registration.
/// </summary>
/// <remarks>
/// The kernel is configured with:
/// - Azure OpenAI chat completion service
/// - Low temperature (0.0) for deterministic, factual responses
/// - Function calling enabled for datasheet access
/// </remarks>
public sealed class KernelFactory
{
    private readonly AzureOpenAIOptions _azureOptions;
    private readonly FeatureFlags _featureFlags;
    private readonly ILoggerFactory _loggerFactory;

    public KernelFactory(
        IOptions<AzureOpenAIOptions> azureOptions,
        IOptions<FeatureFlags> featureFlags,
        ILoggerFactory loggerFactory)
    {
        _azureOptions = azureOptions.Value;
        _featureFlags = featureFlags.Value;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new Semantic Kernel instance configured for the Product Assistant.
    /// </summary>
    /// <remarks>
    /// Each agent should get its own kernel instance to avoid state pollution.
    /// Plugins are registered separately via RegisterPlugins method.
    /// </remarks>
    public Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();

        // Configure Azure OpenAI
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: _azureOptions.DeploymentName,
            endpoint: _azureOptions.Endpoint,
            apiKey: _azureOptions.ApiKey);

        // Add logging if detailed logging is enabled
        if (_featureFlags.EnableDetailedLogging)
        {
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddFilter("Microsoft.SemanticKernel", LogLevel.Debug);
            });
        }

        return builder.Build();
    }

    /// <summary>
    /// Gets the default prompt execution settings for product Q&A.
    /// </summary>
    /// <remarks>
    /// Temperature 0.0 ensures deterministic responses based on function call results.
    /// This is critical for hallucination prevention - we want the model to use
    /// only the data returned from our datasheet plugin.
    /// </remarks>
    public PromptExecutionSettings GetDefaultSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["max_tokens"] = _azureOptions.MaxTokens,
                ["temperature"] = _azureOptions.Temperature
            }
        };
    }

    /// <summary>
    /// Gets prompt execution settings with function calling enabled.
    /// </summary>
    public PromptExecutionSettings GetFunctionCallingSettings()
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["max_tokens"] = _azureOptions.MaxTokens,
                ["temperature"] = _azureOptions.Temperature
            },
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
    }
}
