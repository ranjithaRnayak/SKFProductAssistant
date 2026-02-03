using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Domain.ValueObjects;
using Skf.ProductAssistant.Infrastructure.Configuration;

namespace Skf.ProductAssistant.Infrastructure.Repositories;

/// <summary>
/// Repository implementation that reads product data from JSON files.
/// Implements lazy loading with optional file watching for development.
/// </summary>
/// <remarks>
/// JSON files are loaded once at startup (or on first access) and cached in memory.
/// This is the single source of truth for product data - no hallucination possible
/// because we only return what exists in these files.
/// </remarks>
public sealed class JsonDatasheetRepository : IDatasheetRepository, IDisposable
{
    private readonly DatasheetOptions _options;
    private readonly ILogger<JsonDatasheetRepository> _logger;
    private readonly ConcurrentDictionary<string, ProductData> _products = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JsonDatasheetRepository(
        IOptions<DatasheetOptions> options,
        ILogger<JsonDatasheetRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Ensures datasheets are loaded before any operation.
    /// Thread-safe lazy initialization.
    /// </summary>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded) return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_isLoaded) return;
            await LoadDatasheetsAsync(cancellationToken);
            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadDatasheetsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading datasheets from {Count} files", _options.FilePaths.Length);

        foreach (var relativePath in _options.FilePaths)
        {
            var filePath = _options.ResolvePath(relativePath);
            await LoadDatasheetFileAsync(filePath, cancellationToken);
        }

        _logger.LogInformation("Loaded {Count} products from datasheets", _products.Count);
    }

    private async Task LoadDatasheetFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Datasheet file not found: {FilePath}", filePath);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var products = JsonSerializer.Deserialize<List<ProductDataDto>>(json, JsonOptions);

            if (products is null)
            {
                _logger.LogWarning("Datasheet file contains no products: {FilePath}", filePath);
                return;
            }

            foreach (var dto in products)
            {
                var productData = MapToProductData(dto);
                _products[productData.Designation.Normalized] = productData;
            }

            _logger.LogDebug("Loaded {Count} products from {FilePath}", products.Count, filePath);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse datasheet file: {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid JSON in datasheet file: {filePath}", ex);
        }
    }

    private static ProductData MapToProductData(ProductDataDto dto)
    {
        var attributes = dto.Attributes
            .Select(kvp => new ProductAttribute(kvp.Key, kvp.Value.Value, kvp.Value.Unit))
            .ToList();

        return new ProductData
        {
            Designation = new ProductDesignation(dto.Designation),
            Name = dto.Name,
            Category = dto.Category,
            Attributes = attributes,
            Description = dto.Description,
            LastUpdated = dto.LastUpdated ?? DateTimeOffset.UtcNow
        };
    }

    public async Task<ProductData?> GetByDesignationAsync(
        ProductDesignation designation,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _products.TryGetValue(designation.Normalized, out var product) ? product : null;
    }

    public async Task<ProductAttribute?> GetAttributeAsync(
        ProductDesignation designation,
        string attributeName,
        CancellationToken cancellationToken = default)
    {
        var product = await GetByDesignationAsync(designation, cancellationToken);
        return product?.GetAttribute(attributeName);
    }

    public async Task<IReadOnlyList<ProductData>> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var normalizedQuery = query.ToLowerInvariant();

        // Search by designation, name, and category
        return _products.Values
            .Where(p =>
                p.Designation.Normalized.Contains(normalizedQuery) ||
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (p.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(maxResults)
            .ToList();
    }

    public async Task<IReadOnlyList<ProductData>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        return _products.Values
            .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetAllCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        return _products.Values
            .Select(p => p.Category)
            .Where(c => c is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList()!;
    }

    public async Task<bool> ExistsAsync(
        ProductDesignation designation,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _products.ContainsKey(designation.Normalized);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _products.Count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _loadLock.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Internal DTO for JSON deserialization.
    /// Keeps the JSON format separate from domain entities.
    /// </summary>
    private sealed class ProductDataDto
    {
        public required string Designation { get; init; }
        public required string Name { get; init; }
        public string? Category { get; init; }
        public string? Description { get; init; }
        public DateTimeOffset? LastUpdated { get; init; }
        public Dictionary<string, AttributeDto> Attributes { get; init; } = [];
    }

    private sealed class AttributeDto
    {
        public required string Value { get; init; }
        public string? Unit { get; init; }
    }
}
