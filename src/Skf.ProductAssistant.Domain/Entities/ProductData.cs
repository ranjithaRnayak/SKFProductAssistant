using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Domain.Entities;

public sealed class ProductData
{
    public required ProductDesignation Designation { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required IReadOnlyDictionary<string, ProductAttribute> Attributes { get; init; }

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

    public string? GetAttributeValue(string attributeName) =>
        TryGetAttribute(attributeName, out var attr) ? attr?.Value : null;

    public bool HasAttribute(string attributeName) => TryGetAttribute(attributeName, out _);

    public IEnumerable<string> GetAttributeNames() => Attributes.Keys;

    private static string NormalizeAttributeName(string name) =>
        name.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

    public override string ToString() => $"{Designation} - {Name}";
}
