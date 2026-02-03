using Microsoft.Extensions.Logging;
using Skf.ProductAssistant.Domain.ValueObjects;
using Skf.ProductAssistant.Infrastructure.Normalization;

namespace Skf.ProductAssistant.Application.Services;

/// <summary>
/// Service for normalizing product designations and attribute names.
/// Ensures consistent lookups regardless of user input format.
/// </summary>
/// <remarks>
/// This service handles:
/// - Product designation normalization (6205-2RS → 62052RS)
/// - Attribute name normalization using the lexicon (bore → bore_diameter)
/// - Fuzzy matching for common typos (future enhancement)
/// </remarks>
public sealed class ProductNormalizationService
{
    private readonly AttributeLexicon _lexicon;
    private readonly ILogger<ProductNormalizationService> _logger;

    public ProductNormalizationService(
        AttributeLexicon lexicon,
        ILogger<ProductNormalizationService> logger)
    {
        _lexicon = lexicon;
        _logger = logger;
    }

    /// <summary>
    /// Normalizes a product designation string.
    /// </summary>
    /// <param name="designation">Raw designation from user input.</param>
    /// <returns>Normalized ProductDesignation, or null if invalid.</returns>
    public ProductDesignation? NormalizeDesignation(string? designation)
    {
        if (string.IsNullOrWhiteSpace(designation))
        {
            return null;
        }

        try
        {
            return new ProductDesignation(designation);
        }
        catch (ArgumentException ex)
        {
            _logger.LogDebug(ex, "Invalid product designation: {Designation}", designation);
            return null;
        }
    }

    /// <summary>
    /// Normalizes an attribute name using the lexicon.
    /// </summary>
    /// <param name="attributeName">Raw attribute name from user input.</param>
    /// <returns>Normalized canonical attribute name.</returns>
    public string NormalizeAttribute(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return string.Empty;
        }

        var normalized = _lexicon.Normalize(attributeName);

        if (_lexicon.HasMapping(attributeName) && normalized != attributeName.ToLowerInvariant())
        {
            _logger.LogDebug(
                "Normalized attribute '{Original}' to '{Normalized}'",
                attributeName,
                normalized);
        }

        return normalized;
    }

    /// <summary>
    /// Extracts product designation from a user message.
    /// Looks for patterns that match bearing designations.
    /// </summary>
    /// <param name="message">User message.</param>
    /// <returns>Extracted designation, or null if none found.</returns>
    public ProductDesignation? ExtractDesignationFromMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        // Common bearing designation patterns:
        // - 6205-2RS, 6205 2RS, 62052RS
        // - 22220 E, 22220E
        // - NU 205 ECP, NU205ECP
        var words = message.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            // Skip common words
            if (IsCommonWord(word))
            {
                continue;
            }

            // Try to parse as designation
            var designation = NormalizeDesignation(word);
            if (designation is not null && LooksLikeDesignation(designation.Original))
            {
                return designation;
            }
        }

        // Try combining adjacent words (e.g., "6205" "2RS")
        for (var i = 0; i < words.Length - 1; i++)
        {
            var combined = words[i] + words[i + 1];
            var designation = NormalizeDesignation(combined);
            if (designation is not null && LooksLikeDesignation(combined))
            {
                return designation;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts attribute name from a user question.
    /// </summary>
    /// <param name="message">User message.</param>
    /// <returns>Extracted and normalized attribute name, or null if none found.</returns>
    public string? ExtractAttributeFromMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var lower = message.ToLowerInvariant();

        // Look for known attributes from the lexicon
        foreach (var canonical in _lexicon.GetCanonicalNames())
        {
            var aliases = _lexicon.GetAliases(canonical);
            foreach (var alias in aliases)
            {
                if (lower.Contains(alias.ToLowerInvariant()))
                {
                    return canonical;
                }
            }
        }

        // Common attribute patterns
        var attributePatterns = new Dictionary<string, string>
        {
            ["bore diameter"] = "bore_diameter",
            ["outer diameter"] = "outer_diameter",
            ["width"] = "width",
            ["weight"] = "weight",
            ["dynamic load"] = "dynamic_load_rating",
            ["static load"] = "static_load_rating"
        };

        foreach (var (pattern, attribute) in attributePatterns)
        {
            if (lower.Contains(pattern))
            {
                return _lexicon.Normalize(attribute);
            }
        }

        return null;
    }

    private static bool IsCommonWord(string word)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "what", "which", "where", "when", "how",
            "of", "for", "to", "in", "on", "at", "by", "with", "about", "bearing", "product"
        };
        return common.Contains(word);
    }

    private static bool LooksLikeDesignation(string value)
    {
        // Must have at least one digit
        if (!value.Any(char.IsDigit))
        {
            return false;
        }

        // Should be reasonable length
        if (value.Length < 3 || value.Length > 30)
        {
            return false;
        }

        // Should have mix of letters and numbers (or just numbers with suffix)
        return true;
    }
}
