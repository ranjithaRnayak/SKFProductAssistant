namespace Skf.ProductAssistant.Domain.ValueObjects;

/// <summary>
/// Represents a product attribute with its name, value, and optional unit.
/// Immutable value object for type-safe attribute handling.
/// </summary>
/// <remarks>
/// Examples:
/// - Name: "bore_diameter", Value: "25", Unit: "mm"
/// - Name: "dynamic_load_rating", Value: "14000", Unit: "N"
/// - Name: "seal_type", Value: "2RS", Unit: null
/// </remarks>
public sealed class ProductAttribute : IEquatable<ProductAttribute>
{
    /// <summary>
    /// The attribute name (normalized to lowercase with underscores).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The attribute value as a string.
    /// Numeric values are stored as strings to preserve precision and formatting.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The unit of measurement (e.g., "mm", "N", "kg").
    /// Null for dimensionless attributes like seal_type.
    /// </summary>
    public string? Unit { get; }

    /// <summary>
    /// Creates a new ProductAttribute with validation.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <exception cref="ArgumentException">Thrown when name or value is null/empty.</exception>
    public ProductAttribute(string name, string value, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Attribute name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Attribute value cannot be null or empty.", nameof(value));
        }

        Name = NormalizeName(name);
        Value = value.Trim();
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }

    /// <summary>
    /// Normalizes attribute name to lowercase with underscores.
    /// Enables consistent lookups regardless of input format.
    /// </summary>
    private static string NormalizeName(string name)
    {
        // Convert "Bore Diameter" or "bore-diameter" to "bore_diameter"
        return name
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
    }

    /// <summary>
    /// Gets the formatted display string including unit if present.
    /// </summary>
    public string ToDisplayString()
    {
        return Unit is not null ? $"{Value} {Unit}" : Value;
    }

    /// <summary>
    /// Attempts to parse the value as a decimal for numeric comparisons.
    /// </summary>
    /// <param name="result">The parsed decimal value if successful.</param>
    /// <returns>True if the value is numeric and was successfully parsed.</returns>
    public bool TryGetNumericValue(out decimal result)
    {
        return decimal.TryParse(Value, out result);
    }

    public bool Equals(ProductAttribute? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Value == other.Value && Unit == other.Unit;
    }

    public override bool Equals(object? obj) => Equals(obj as ProductAttribute);

    public override int GetHashCode() => HashCode.Combine(Name, Value, Unit);

    public override string ToString() => $"{Name}: {ToDisplayString()}";

    public static bool operator ==(ProductAttribute? left, ProductAttribute? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ProductAttribute? left, ProductAttribute? right)
        => !(left == right);
}
