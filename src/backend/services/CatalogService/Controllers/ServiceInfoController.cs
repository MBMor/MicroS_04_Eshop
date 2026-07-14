using CatalogService.Contracts;
using CatalogService.Data;
using CatalogService.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(CatalogDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query = dbContext.Products.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        List<ProductResponse> products = await query
            .OrderBy(product => product.Name)
            .Select(product => ProductResponse.FromProduct(product))
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetProductById(
        Guid id,
        CancellationToken cancellationToken)
    {
        Product? product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        return Ok(ProductResponse.FromProduct(product));
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedSku = request.Sku.Trim().ToUpperInvariant();

        bool skuAlreadyExists = await dbContext.Products
            .AnyAsync(product => product.Sku == normalizedSku, cancellationToken);

        if (skuAlreadyExists)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Product SKU already exists.",
                Detail = $"Product with SKU '{normalizedSku}' already exists."
            });
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        Product product = Product.Create(
            Guid.NewGuid(),
            request.Name,
            normalizedSku,
            request.Description,
            request.Category,
            request.PriceAmount,
            request.Currency,
            request.IsActive,
            now);

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        ProductResponse response = ProductResponse.FromProduct(product);

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.Id },
            response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        Product? product = await dbContext.Products
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        string normalizedSku = request.Sku.Trim().ToUpperInvariant();

        bool skuAlreadyExists = await dbContext.Products
            .AnyAsync(candidate => candidate.Id != id && candidate.Sku == normalizedSku, cancellationToken);

        if (skuAlreadyExists)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Product SKU already exists.",
                Detail = $"Product with SKU '{normalizedSku}' already exists."
            });
        }

        product.Update(
            request.Name,
            normalizedSku,
            request.Description,
            request.Category,
            request.PriceAmount,
            request.Currency,
            request.IsActive,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ProductResponse.FromProduct(product));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        Product? product = await dbContext.Products
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        product.Deactivate(DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
