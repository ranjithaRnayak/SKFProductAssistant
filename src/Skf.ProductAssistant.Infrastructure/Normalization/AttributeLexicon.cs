using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Infrastructure.Normalization;

/// <summary>
/// Provides attribute name normalization using a configurable lexicon.
/// Maps user-friendly terms to canonical attribute names used in datasheets.
/// </summary>
/// <remarks>
/// The lexicon is loaded from a JSON file specified in DatasheetOptions.LexiconFilePath.
/// This ensures no hardcoding of attribute mappings.
///
/// Example mappings:
/// - "width" → "outer_diameter_width"
/// - "bore", "inner diameter" → "bore_diameter"
/// - "weight", "mass" → "weight"
/// </remarks>
public sealed class AttributeLexicon
{
    private readonly Dictionary<string, string> _mappings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AttributeLexicon> _logger;
    private bool _isLoaded;

    public AttributeLexicon(
        IOptions<DatasheetOptions> options,
        ILogger<AttributeLexicon> logger)
    {
        _logger = logger;

        var lexiconPath = options.Value.LexiconFilePath;
        if (!string.IsNullOrEmpty(lexiconPath))
        {
            var resolvedPath = options.Value.ResolvePath(lexiconPath);
            LoadLexicon(resolvedPath);
        }
    }

    private void LoadLexicon(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Attribute lexicon file not found: {FilePath}", filePath);
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var lexiconData = JsonSerializer.Deserialize<LexiconData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (lexiconData?.Mappings is null)
            {
                _logger.LogWarning("Lexicon file contains no mappings: {FilePath}", filePath);
                return;
            }

            foreach (var mapping in lexiconData.Mappings)
            {
                var canonicalName = mapping.CanonicalName;
                foreach (var alias in mapping.Aliases)
                {
                    _mappings[alias] = canonicalName;
                }
                // Also map the canonical name to itself for consistency
                _mappings[canonicalName] = canonicalName;
            }

            _isLoaded = true;
            _logger.LogInformation(
                "Loaded attribute lexicon with {Count} mappings from {FilePath}",
                _mappings.Count,
                filePath);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse attribute lexicon: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Normalizes an attribute name to its canonical form.
    /// </summary>
    /// <param name="attributeName">The user-provided attribute name.</param>
    /// <returns>
    /// The canonical attribute name if a mapping exists,
    /// otherwise the input normalized to lowercase with underscores.
    /// </returns>
    public string Normalize(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return string.Empty;
        }

        var trimmed = attributeName.Trim();

        // Try exact match first
        if (_mappings.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        // Try normalized form
        var normalized = trimmed
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");

        if (_mappings.TryGetValue(normalized, out canonical))
        {
            return canonical;
        }

        // Return normalized form if no mapping found
        return normalized;
    }

    /// <summary>
    /// Checks if a mapping exists for the given attribute name.
    /// </summary>
    public bool HasMapping(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        var normalized = attributeName.Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");

        return _mappings.ContainsKey(attributeName) || _mappings.ContainsKey(normalized);
    }

    /// <summary>
    /// Gets all known aliases for a canonical attribute name.
    /// </summary>
    public IReadOnlyList<string> GetAliases(string canonicalName)
    {
        return _mappings
            .Where(kvp => kvp.Value.Equals(canonicalName, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets all canonical attribute names in the lexicon.
    /// </summary>
    public IReadOnlyList<string> GetCanonicalNames()
    {
        return _mappings.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Whether the lexicon was successfully loaded.
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Number of mappings in the lexicon.
    /// </summary>
    public int Count => _mappings.Count;

    private sealed class LexiconData
    {
        public List<AttributeMapping>? Mappings { get; init; }
    }

    private sealed class AttributeMapping
    {
        public required string CanonicalName { get; init; }
        public List<string> Aliases { get; init; } = [];
    }
}
