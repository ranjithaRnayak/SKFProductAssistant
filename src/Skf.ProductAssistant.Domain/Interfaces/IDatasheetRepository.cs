using Skf.ProductAssistant.Domain.Entities;
using Skf.ProductAssistant.Domain.ValueObjects;

namespace Skf.ProductAssistant.Domain.Interfaces;

/// <summary>
/// Repository for accessing product datasheet information.
/// Implementation reads from JSON files configured in DatasheetOptions.
/// </summary>
/// <remarks>
/// This is the primary data source for product information.
/// All answers must be grounded in data from this repository to prevent hallucinations.
/// </remarks>
public interface IDatasheetRepository
{
    /// <summary>
    /// Gets a product by its designation.
    /// </summary>
    /// <param name="designation">The product designation to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The product data, or null if not found.</returns>
    Task<ProductData?> GetProductAsync(ProductDesignation designation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific attribute value for a product.
    /// </summary>
    /// <param name="designation">The product designation.</param>
    /// <param name="attributeName">The attribute name to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attribute, or null if product or attribute not found.</returns>
    Task<ProductAttribute?> GetAttributeAsync(
        ProductDesignation designation,
        string attributeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for products matching a partial designation or name.
    /// </summary>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching products.</returns>
    Task<IReadOnlyList<ProductData>> SearchProductsAsync(
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all products in a specific category.
    /// </summary>
    /// <param name="category">The product category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Products in the category.</returns>
    Task<IReadOnlyList<ProductData>> GetProductsByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available product categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of category names.</returns>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a product exists in the datasheet.
    /// </summary>
    /// <param name="designation">The product designation to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the product exists.</returns>
    Task<bool> ProductExistsAsync(ProductDesignation designation, CancellationToken cancellationToken = default);
}
