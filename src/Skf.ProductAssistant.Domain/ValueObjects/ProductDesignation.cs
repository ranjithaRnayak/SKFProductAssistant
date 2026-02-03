namespace Skf.ProductAssistant.Domain.ValueObjects;

/// <summary>
/// Represents an SKF product designation (e.g., "6205-2RS", "7308 BECBP").
/// Immutable value object with validation to ensure designations follow SKF patterns.
/// </summary>
/// <remarks>
/// SKF designations typically consist of:
/// - A numeric base code (e.g., "6205", "7308")
/// - Optional suffix codes for variants (e.g., "-2RS", "BECBP")
/// This value object encapsulates parsing and normalization logic.
/// </remarks>
public sealed class ProductDesignation : IEquatable<ProductDesignation>
{
    /// <summary>
    /// The raw designation string as provided (before normalization).
    /// </summary>
    public string RawValue { get; }

    /// <summary>
    /// The normalized designation (uppercase, trimmed, standardized spacing).
    /// Used for lookups and comparisons.
    /// </summary>
    public string NormalizedValue { get; }

    /// <summary>
    /// Creates a new ProductDesignation with validation.
    /// </summary>
    /// <param name="value">The product designation string.</param>
    /// <exception cref="ArgumentException">Thrown when designation is null, empty, or whitespace.</exception>
    public ProductDesignation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product designation cannot be null or empty.", nameof(value));
        }

        RawValue = value;
        NormalizedValue = Normalize(value);
    }

    /// <summary>
    /// Normalizes a designation string for consistent lookups.
    /// </summary>
    private static string Normalize(string value)
    {
        // Remove extra whitespace, convert to uppercase for case-insensitive matching
        return string.Join(" ", value.Trim().ToUpperInvariant().Split(
            default(char[]),
            StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Attempts to create a ProductDesignation, returning null if invalid.
    /// Useful when validation failure shouldn't throw.
    /// </summary>
    public static ProductDesignation? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new ProductDesignation(value);
    }

    public bool Equals(ProductDesignation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return NormalizedValue == other.NormalizedValue;
    }

    public override bool Equals(object? obj) => Equals(obj as ProductDesignation);

    public override int GetHashCode() => NormalizedValue.GetHashCode();

    public override string ToString() => NormalizedValue;

    public static bool operator ==(ProductDesignation? left, ProductDesignation? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ProductDesignation? left, ProductDesignation? right)
        => !(left == right);

    /// <summary>
    /// Implicit conversion from string for convenience.
    /// </summary>
    public static implicit operator string(ProductDesignation designation) => designation.NormalizedValue;
}
