using System.ComponentModel.DataAnnotations;

namespace Skf.ProductAssistant.Infrastructure.Configuration;

/// <summary>
/// Configuration options for JSON datasheet file locations.
/// Bound from appsettings.json "Datasheet" section.
/// </summary>
public sealed class DatasheetOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Datasheet";

    /// <summary>
    /// Array of file paths to JSON datasheet files.
    /// Paths can be absolute or relative to the application root.
    /// </summary>
    /// <example>
    /// ["datasheets/bearings_type1.json", "datasheets/bearings_type2.json"]
    /// </example>
    [Required(ErrorMessage = "At least one datasheet file path is required")]
    [MinLength(1, ErrorMessage = "At least one datasheet file path is required")]
    public required string[] FilePaths { get; init; }

    /// <summary>
    /// Base directory for relative file paths.
    /// Defaults to application base directory if not specified.
    /// </summary>
    public string? BaseDirectory { get; init; }

    /// <summary>
    /// Whether to watch files for changes and reload automatically.
    /// Useful for development; disable in production for performance.
    /// </summary>
    public bool WatchForChanges { get; init; } = false;

    /// <summary>
    /// Path to the attribute lexicon file for normalization mappings.
    /// </summary>
    public string? LexiconFilePath { get; init; }

    /// <summary>
    /// Resolves a file path to an absolute path using BaseDirectory.
    /// </summary>
    public string ResolvePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        var baseDir = BaseDirectory ?? AppContext.BaseDirectory;
        return Path.Combine(baseDir, filePath);
    }
}
