using System.ComponentModel.DataAnnotations;

namespace Skf.ProductAssistant.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Azure OpenAI service connection.
/// Bound from appsettings.json "AzureOpenAI" section.
/// </summary>
public sealed class AzureOpenAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL (e.g., https://your-resource.openai.azure.com/).
    /// </summary>
    [Required(ErrorMessage = "Azure OpenAI endpoint is required")]
    [Url(ErrorMessage = "Azure OpenAI endpoint must be a valid URL")]
    public required string Endpoint { get; init; }

    /// <summary>
    /// Azure OpenAI API key for authentication.
    /// Should be stored in environment variable or Azure Key Vault in production.
    /// </summary>
    [Required(ErrorMessage = "Azure OpenAI API key is required")]
    [MinLength(10, ErrorMessage = "API key appears to be invalid")]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Deployment name for the chat model (e.g., "gpt-4", "gpt-35-turbo").
    /// </summary>
    [Required(ErrorMessage = "Deployment name is required")]
    public required string DeploymentName { get; init; }

    /// <summary>
    /// Maximum tokens for completion responses.
    /// Lower values reduce cost and latency; 500 is reasonable for product Q&A.
    /// </summary>
    [Range(50, 4000, ErrorMessage = "MaxTokens must be between 50 and 4000")]
    public int MaxTokens { get; init; } = 500;

    /// <summary>
    /// Temperature for response generation (0.0 = deterministic, 1.0 = creative).
    /// Use 0.0 for factual product data to minimize hallucination risk.
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "Temperature must be between 0.0 and 1.0")]
    public double Temperature { get; init; } = 0.0;

    /// <summary>
    /// Optional: Embedding model deployment for semantic search (future use).
    /// </summary>
    public string? EmbeddingDeploymentName { get; init; }
}
