using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Interfaces;

namespace Skf.ProductAssistant.Application.Plugins;

public sealed class DatasheetPlugin
{
    private readonly IDatasheetRepository _repository;
    private readonly ProductNormalizationService _normalization;
    private readonly ILogger<DatasheetPlugin> _logger;

    public const string NotFoundResponse = "NOT_FOUND";
    public const string InvalidDesignationResponse = "INVALID_DESIGNATION";

    public DatasheetPlugin(
        IDatasheetRepository repository,
        ProductNormalizationService normalization,
        ILogger<DatasheetPlugin> logger)
    {
        _repository = repository;
        _normalization = normalization;
        _logger = logger;
    }

    [KernelFunction("get_product_attribute")]
    [Description("Gets a specific attribute for a product. Returns value with unit, or 'NOT_FOUND'.")]
    public async Task<string> GetProductAttributeAsync(
        [Description("Product designation (e.g., '6205-2RS')")] string designation,
        [Description("Attribute name (e.g., 'width', 'bore_diameter')")] string attribute)
    {
        _logger.LogDebug("GetProductAttribute: {Designation}, {Attribute}", designation, attribute);

        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null) return InvalidDesignationResponse;

        var normalizedAttribute = _normalization.NormalizeAttribute(attribute);
        var productAttribute = await _repository.GetAttributeAsync(productDesignation, normalizedAttribute);

        if (productAttribute is null)
        {
            _logger.LogDebug("Attribute not found: {Designation}.{Attribute}", productDesignation, normalizedAttribute);
            return NotFoundResponse;
        }

        return productAttribute.ToDisplayString();
    }

    [KernelFunction("get_product_info")]
    [Description("Gets all information about a product. Returns 'NOT_FOUND' if product doesn't exist.")]
    public async Task<string> GetProductInfoAsync(
        [Description("Product designation (e.g., '6205-2RS')")] string designation)
    {
        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null) return InvalidDesignationResponse;

        var product = await _repository.GetByDesignationAsync(productDesignation);
        if (product is null) return NotFoundResponse;

        var lines = new List<string>
        {
            $"Product: {product.Designation}",
            $"Name: {product.Name}",
            $"Category: {product.Category}",
            "Attributes:"
        };
        lines.AddRange(product.Attributes.Select(attr => $"  - {attr.Name}: {attr.ToDisplayString()}"));

        return string.Join("\n", lines);
    }

    [KernelFunction("check_product_exists")]
    [Description("Checks if a product exists. Returns 'true' or 'false'.")]
    public async Task<string> CheckProductExistsAsync(
        [Description("Product designation to check")] string designation)
    {
        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null) return "false";

        var exists = await _repository.ExistsAsync(productDesignation);
        return exists ? "true" : "false";
    }

    [KernelFunction("search_products")]
    [Description("Searches for products. Returns matching designations or 'NO_RESULTS'.")]
    public async Task<string> SearchProductsAsync(
        [Description("Search query")] string query,
        [Description("Max results (default 5)")] int maxResults = 5)
    {
        var results = await _repository.SearchAsync(query, maxResults);
        if (results.Count == 0) return "NO_RESULTS";

        return string.Join("\n", results.Select(p => $"- {p.Designation}: {p.Name}"));
    }

    [KernelFunction("list_product_attributes")]
    [Description("Lists available attributes for a product. Returns 'NOT_FOUND' if product doesn't exist.")]
    public async Task<string> ListProductAttributesAsync(
        [Description("Product designation")] string designation)
    {
        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null) return InvalidDesignationResponse;

        var product = await _repository.GetByDesignationAsync(productDesignation);
        if (product is null) return NotFoundResponse;

        return string.Join(", ", product.Attributes.Select(a => a.Name));
    }
}
