namespace OrdersService.Identity;

public interface IOrderOwnerProvider
{
    string? GetCustomerId(HttpContext httpContext);
}
