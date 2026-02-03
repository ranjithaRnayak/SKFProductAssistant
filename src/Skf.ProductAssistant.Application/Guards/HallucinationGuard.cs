using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Application.Plugins;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Domain.ValueObjects;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Application.Guards;

/// <summary>
/// Guards against AI hallucination by validating responses against actual data.
/// Critical component for ensuring factual accuracy in product information.
/// </summary>
/// <remarks>
/// Hallucination prevention strategy:
/// 1. Function-first: Agent must use DatasheetPlugin to get data
/// 2. Validation: Responses are checked against known data
/// 3. Abstention: When data is not found, agent must explicitly say so
/// 4. No guessing: Numbers and specifications must come from datasheets
/// </remarks>
public sealed class HallucinationGuard
{
    private readonly IDatasheetRepository _repository;
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<HallucinationGuard> _logger;

    // Patterns that indicate potential hallucination
    private static readonly string[] HallucinationIndicators =
    [
        "approximately",
        "around",
        "usually",
        "typically",
        "generally",
        "probably",
        "might be",
        "could be",
        "I believe",
        "I think",
        "estimated"
    ];

    // Patterns that indicate proper abstention
    private static readonly string[] AbstentionIndicators =
    [
        "NOT_FOUND",
        "not found",
        "don't have",
        "do not have",
        "not available",
        "no information",
        "cannot find",
        "unable to find"
    ];

    public HallucinationGuard(
        IDatasheetRepository repository,
        IOptions<FeatureFlags> featureFlags,
        ILogger<HallucinationGuard> logger)
    {
        _repository = repository;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Validates that a response doesn't contain fabricated information.
    /// </summary>
    /// <param name="response">The AI-generated response.</param>
    /// <param name="originalQuestion">The user's original question.</param>
    /// <param name="productDesignation">The product being discussed, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details.</returns>
    public async Task<ValidationResult> ValidateResponseAsync(
        string response,
        string originalQuestion,
        string? productDesignation,
        CancellationToken cancellationToken = default)
    {
        if (!_featureFlags.StrictHallucinationPrevention)
        {
            return ValidationResult.Valid();
        }

        // Check for hallucination indicators
        var hallucinationCheck = CheckForHallucinationIndicators(response);
        if (!hallucinationCheck.IsValid)
        {
            _logger.LogWarning(
                "Response contains hallucination indicator: {Reason}",
                hallucinationCheck.Reason);
            return hallucinationCheck;
        }

        // If response is an abstention, that's valid
        if (IsAbstention(response))
        {
            return ValidationResult.Valid();
        }

        // If a product is mentioned, verify the response data exists
        if (!string.IsNullOrEmpty(productDesignation))
        {
            var dataCheck = await ValidateDataExistsAsync(
                response,
                productDesignation,
                cancellationToken);

            if (!dataCheck.IsValid)
            {
                return dataCheck;
            }
        }

        // Check for suspicious numeric patterns
        var numericCheck = CheckNumericPatterns(response);
        if (!numericCheck.IsValid)
        {
            _logger.LogWarning(
                "Response contains suspicious numeric pattern: {Reason}",
                numericCheck.Reason);
            return numericCheck;
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Checks response for language that suggests guessing or uncertainty.
    /// </summary>
    private static ValidationResult CheckForHallucinationIndicators(string response)
    {
        var lower = response.ToLowerInvariant();

        foreach (var indicator in HallucinationIndicators)
        {
            if (lower.Contains(indicator))
            {
                return ValidationResult.Invalid(
                    $"Response contains uncertainty indicator: '{indicator}'. " +
                    "Product specifications should be stated as facts from the datasheet.");
            }
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Checks if the response is a proper abstention when data is not found.
    /// </summary>
    private static bool IsAbstention(string response)
    {
        var lower = response.ToLowerInvariant();
        return AbstentionIndicators.Any(lower.Contains);
    }

    /// <summary>
    /// Validates that data mentioned in the response exists in the datasheet.
    /// </summary>
    private async Task<ValidationResult> ValidateDataExistsAsync(
        string response,
        string productDesignation,
        CancellationToken cancellationToken)
    {
        try
        {
            var designation = new ProductDesignation(productDesignation);
            var exists = await _repository.ExistsAsync(designation, cancellationToken);

            if (!exists)
            {
                return ValidationResult.Invalid(
                    $"Response mentions product '{productDesignation}' which doesn't exist in datasheets.");
            }

            return ValidationResult.Valid();
        }
        catch (ArgumentException)
        {
            // Invalid designation format
            return ValidationResult.Invalid(
                $"Invalid product designation format: '{productDesignation}'.");
        }
    }

    /// <summary>
    /// Checks for suspicious numeric patterns that might be fabricated.
    /// </summary>
    private ValidationResult CheckNumericPatterns(string response)
    {
        // Look for very precise numbers that might be made up
        // e.g., "12.3456789 mm" - too many decimal places for bearing specs
        var suspiciousDecimals = System.Text.RegularExpressions.Regex.Matches(
            response,
            @"\d+\.\d{5,}");

        if (suspiciousDecimals.Count > 0)
        {
            return ValidationResult.Invalid(
                "Response contains suspiciously precise numbers. " +
                "Bearing specifications typically have 1-3 decimal places.");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Forces an abstention response when data cannot be found.
    /// </summary>
    public string GetAbstentionResponse(string productDesignation, string? attributeName)
    {
        if (!string.IsNullOrEmpty(attributeName))
        {
            return $"I don't have information about the {attributeName} for product {productDesignation} in my database. " +
                   "Please verify the product designation or contact SKF support for this specification.";
        }

        return $"I couldn't find product {productDesignation} in my database. " +
               "Please verify the product designation or contact SKF support.";
    }
}

/// <summary>
/// Result of a hallucination validation check.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; private init; }
    public string? Reason { get; private init; }

    private ValidationResult() { }

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(string reason) => new()
    {
        IsValid = false,
        Reason = reason
    };
}
