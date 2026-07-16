using Asp.Versioning;
using CatalogService.Contracts;
using CatalogService.Data;
using CatalogService.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatalogService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsController(
    CatalogDbContext dbContext)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>>
        GetProducts(
            [FromQuery] bool includeInactive = false,
            CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query =
            dbContext.Products.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        ProductResponse[] response = await query
            .OrderBy(product => product.Name)
            .Select(product => new ProductResponse(
                product.Id,
                product.Name,
                product.Sku,
                product.Description,
                product.Category,
                product.PriceAmount,
                product.Currency,
                product.IsActive,
                product.CreatedAtUtc,
                product.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductResponse>>
        GetProductById(
            Guid id,
            CancellationToken cancellationToken)
    {
        Product? product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);

        if (product is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Product was not found.",
                $"Product '{id}' does not exist."));
        }

        return Ok(ProductResponse.FromProduct(product));
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductResponse>>
        CreateProduct(
            CreateProductRequest request,
            CancellationToken cancellationToken)
    {
        string normalizedSku =
            request.Sku.Trim().ToUpperInvariant();

        bool skuAlreadyExists = await dbContext.Products
            .AnyAsync(
                product => product.Sku == normalizedSku,
                cancellationToken);

        if (skuAlreadyExists)
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Product SKU already exists.",
                $"Product with SKU '{normalizedSku}' already exists."));
        }

        Product product;

        try
        {
            product = Product.Create(
                Guid.NewGuid(),
                request.Name,
                normalizedSku,
                request.Description,
                request.Category,
                request.PriceAmount,
                request.Currency,
                request.IsActive,
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Product validation failed.",
                exception.Message));
        }

        dbContext.Products.Add(product);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Product SKU already exists.",
                $"Product with SKU '{normalizedSku}' already exists."));
        }

        ProductResponse response =
            ProductResponse.FromProduct(product);

        return CreatedAtAction(
            nameof(GetProductById),
            new
            {
                version = RouteData.Values["version"],
                id = product.Id
            },
            response);
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductResponse>>
        UpdateProduct(
            Guid id,
            UpdateProductRequest request,
            CancellationToken cancellationToken)
    {
        Product? product = await dbContext.Products
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);

        if (product is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Product was not found.",
                $"Product '{id}' does not exist."));
        }

        string normalizedSku =
            request.Sku.Trim().ToUpperInvariant();

        bool skuAlreadyExists = await dbContext.Products
            .AnyAsync(
                candidate =>
                    candidate.Id != id
                    && candidate.Sku == normalizedSku,
                cancellationToken);

        if (skuAlreadyExists)
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Product SKU already exists.",
                $"Product with SKU '{normalizedSku}' already exists."));
        }

        try
        {
            product.Update(
                request.Name,
                normalizedSku,
                request.Description,
                request.Category,
                request.PriceAmount,
                request.Currency,
                request.IsActive,
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Product validation failed.",
                exception.Message));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Product SKU already exists.",
                $"Product with SKU '{normalizedSku}' already exists."));
        }

        return Ok(ProductResponse.FromProduct(product));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        Product? product = await dbContext.Products
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);

        if (product is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Product was not found.",
                $"Product '{id}' does not exist."));
        }

        product.Deactivate(DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }

    private static ProblemDetails CreateProblem(
        int status,
        string title,
        string? detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
