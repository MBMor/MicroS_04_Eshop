namespace BasketService.Identity;

public interface IBasketOwnerProvider
{
    string? GetCustomerId(HttpContext httpContext);
}
