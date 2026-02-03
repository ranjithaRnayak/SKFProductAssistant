namespace Skf.ProductAssistant.Domain.ValueObjects;

public sealed class ProductDesignation : IEquatable<ProductDesignation>
{
    public string RawValue { get; }
    public string NormalizedValue { get; }

    public ProductDesignation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product designation cannot be null or empty.", nameof(value));

        RawValue = value;
        NormalizedValue = Normalize(value);
    }

    private static string Normalize(string value) =>
        string.Join(" ", value.Trim().ToUpperInvariant().Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));

    public static ProductDesignation? TryCreate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new ProductDesignation(value);

    public bool Equals(ProductDesignation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return NormalizedValue == other.NormalizedValue;
    }

    public override bool Equals(object? obj) => Equals(obj as ProductDesignation);
    public override int GetHashCode() => NormalizedValue.GetHashCode();
    public override string ToString() => NormalizedValue;

    public static bool operator ==(ProductDesignation? left, ProductDesignation? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(ProductDesignation? left, ProductDesignation? right) => !(left == right);
    public static implicit operator string(ProductDesignation designation) => designation.NormalizedValue;
}
