namespace Eshop.Security.Authorization;

public static class EshopPolicies
{
    public const string CustomerOnly = "CustomerOnly";

    public const string SupportOnly = "SupportOnly";

    public const string AdminOnly = "AdminOnly";

    public const string SupportOrAdmin = "SupportOrAdmin";

    public const string CustomerOrAdmin = "CustomerOrAdmin";

    public const string AuthenticatedUser = "AuthenticatedUser";
}
