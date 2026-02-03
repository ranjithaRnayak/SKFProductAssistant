using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Domain.Entities;

/// <summary>
/// Represents a product from the SKF datasheet with all its attributes.
/// This is the core entity for product information lookups.
/// </summary>
/// <remarks>
/// ProductData is the single source of truth for answering product questions.
/// The hallucination guard ensures all responses are grounded in actual ProductData instances.
/// </remarks>
public sealed class ProductData
{
    /// <summary>
    /// The unique product designation (e.g., "6205-2RS").
    /// </summary>
    public required ProductDesignation Designation { get; init; }

    /// <summary>
    /// Human-readable product name or description.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Product category (e.g., "Deep Groove Ball Bearings", "Angular Contact Ball Bearings").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Collection of product attributes (dimensions, ratings, etc.).
    /// </summary>
    public required IReadOnlyDictionary<string, ProductAttribute> Attributes { get; init; }

    /// <summary>
    /// Attempts to get an attribute by name (case-insensitive, normalized).
    /// </summary>
    /// <param name="attributeName">The attribute name to look up.</param>
    /// <param name="attribute">The found attribute, or null if not found.</param>
    /// <returns>True if the attribute exists.</returns>
    public bool TryGetAttribute(string attributeName, out ProductAttribute? attribute)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            attribute = null;
            return false;
        }

        var normalizedName = NormalizeAttributeName(attributeName);
        return Attributes.TryGetValue(normalizedName, out attribute);
    }

    /// <summary>
    /// Gets an attribute value by name, or null if not found.
    /// Convenience method for simple lookups.
    /// </summary>
    public string? GetAttributeValue(string attributeName)
    {
        return TryGetAttribute(attributeName, out var attr) ? attr?.Value : null;
    }

    /// <summary>
    /// Checks if the product has a specific attribute.
    /// </summary>
    public bool HasAttribute(string attributeName)
    {
        return TryGetAttribute(attributeName, out _);
    }

    /// <summary>
    /// Gets all attribute names for this product.
    /// Useful for listing available data points.
    /// </summary>
    public IEnumerable<string> GetAttributeNames() => Attributes.Keys;

    /// <summary>
    /// Normalizes attribute name for consistent dictionary lookup.
    /// </summary>
    private static string NormalizeAttributeName(string name)
    {
        return name.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
    }

    public override string ToString() => $"{Designation} - {Name}";
}
