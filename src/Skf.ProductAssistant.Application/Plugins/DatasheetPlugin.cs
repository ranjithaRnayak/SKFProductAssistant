using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Application.Plugins;

/// <summary>
/// Semantic Kernel plugin for accessing product datasheet information.
/// This is the ONLY way the agent can get product data - ensuring no hallucination.
/// </summary>
/// <remarks>
/// All methods return "NOT_FOUND" when data is missing, which the agent
/// must interpret as "I don't have that information" rather than guessing.
/// </remarks>
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

    /// <summary>
    /// Gets a specific attribute value for a product.
    /// </summary>
    /// <param name="designation">The product designation (e.g., "6205-2RS").</param>
    /// <param name="attribute">The attribute name (e.g., "bore_diameter", "weight").</param>
    /// <returns>The attribute value with unit, or "NOT_FOUND" if not available.</returns>
    [KernelFunction("get_product_attribute")]
    [Description("Gets a specific attribute value for a product bearing. Returns the value with unit, or 'NOT_FOUND' if the product or attribute doesn't exist.")]
    public async Task<string> GetProductAttributeAsync(
        [Description("The product designation, e.g., '6205-2RS', '22220E'")] string designation,
        [Description("The attribute name, e.g., 'bore_diameter', 'outer_diameter', 'weight', 'dynamic_load_rating'")] string attribute)
    {
        _logger.LogDebug(
            "GetProductAttribute called: designation={Designation}, attribute={Attribute}",
            designation,
            attribute);

        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null)
        {
            _logger.LogDebug("Invalid designation: {Designation}", designation);
            return InvalidDesignationResponse;
        }

        var normalizedAttribute = _normalization.NormalizeAttribute(attribute);

        var productAttribute = await _repository.GetAttributeAsync(productDesignation, normalizedAttribute);

        if (productAttribute is null)
        {
            _logger.LogDebug(
                "Attribute not found: {Designation}.{Attribute}",
                productDesignation,
                normalizedAttribute);
            return NotFoundResponse;
        }

        return productAttribute.ToDisplayString();
    }

    /// <summary>
    /// Gets all available attributes for a product.
    /// </summary>
    [KernelFunction("get_product_info")]
    [Description("Gets all available information about a product bearing, including all its attributes. Returns 'NOT_FOUND' if the product doesn't exist.")]
    public async Task<string> GetProductInfoAsync(
        [Description("The product designation, e.g., '6205-2RS', '22220E'")] string designation)
    {
        _logger.LogDebug("GetProductInfo called: designation={Designation}", designation);

        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null)
        {
            return InvalidDesignationResponse;
        }

        var product = await _repository.GetByDesignationAsync(productDesignation);

        if (product is null)
        {
            _logger.LogDebug("Product not found: {Designation}", productDesignation);
            return NotFoundResponse;
        }

        // Format product info for the agent
        var lines = new List<string>
        {
            $"Product: {product.Designation}",
            $"Name: {product.Name}"
        };

        if (!string.IsNullOrEmpty(product.Category))
        {
            lines.Add($"Category: {product.Category}");
        }

        lines.Add("Attributes:");
        foreach (var attr in product.Attributes)
        {
            lines.Add($"  - {attr.Name}: {attr.ToDisplayString()}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Checks if a product exists in the database.
    /// </summary>
    [KernelFunction("check_product_exists")]
    [Description("Checks if a product bearing exists in the database. Returns 'true' or 'false'.")]
    public async Task<string> CheckProductExistsAsync(
        [Description("The product designation to check")] string designation)
    {
        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null)
        {
            return "false";
        }

        var exists = await _repository.ExistsAsync(productDesignation);
        return exists ? "true" : "false";
    }

    /// <summary>
    /// Searches for products matching a query.
    /// </summary>
    [KernelFunction("search_products")]
    [Description("Searches for products matching a query. Returns a list of matching product designations, or 'NO_RESULTS' if none found.")]
    public async Task<string> SearchProductsAsync(
        [Description("The search query (designation, name, or category)")] string query,
        [Description("Maximum number of results to return (default 5)")] int maxResults = 5)
    {
        _logger.LogDebug("SearchProducts called: query={Query}", query);

        var results = await _repository.SearchAsync(query, maxResults);

        if (results.Count == 0)
        {
            return "NO_RESULTS";
        }

        var lines = results.Select(p => $"- {p.Designation}: {p.Name}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Lists available attributes for a product.
    /// Useful when user asks "what information is available?"
    /// </summary>
    [KernelFunction("list_product_attributes")]
    [Description("Lists all available attributes for a product. Returns attribute names or 'NOT_FOUND' if product doesn't exist.")]
    public async Task<string> ListProductAttributesAsync(
        [Description("The product designation")] string designation)
    {
        var productDesignation = _normalization.NormalizeDesignation(designation);
        if (productDesignation is null)
        {
            return InvalidDesignationResponse;
        }

        var product = await _repository.GetByDesignationAsync(productDesignation);

        if (product is null)
        {
            return NotFoundResponse;
        }

        var attributes = product.Attributes.Select(a => a.Name);
        return string.Join(", ", attributes);
    }
}
