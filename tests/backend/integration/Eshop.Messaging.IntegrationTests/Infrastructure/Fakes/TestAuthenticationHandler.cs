using System.Security.Claims;
using System.Text.Encodings.Web;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    public const string SchemeName = "IntegrationTest";

    private const string CustomerHeaderName = "X-Customer-Id";
    private const string DefaultSubject = "integration-test-customer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string subject = ResolveSubject();

        Claim[] claims =
        [
            new(
                EshopClaimNames.Subject,
                subject),

            new(
                EshopClaimNames.PreferredUsername,
                subject),

            new(
                EshopClaimNames.Email,
                "integration-test@eshop.local"),

            new(
                EshopClaimNames.Roles,
                EshopRoles.Customer),

            new(
                EshopClaimNames.Roles,
                EshopRoles.Support),

            new(
                EshopClaimNames.Roles,
                EshopRoles.Admin)
        ];

        ClaimsIdentity identity = new(
            claims,
            SchemeName,
            EshopClaimNames.PreferredUsername,
            EshopClaimNames.Roles);

        ClaimsPrincipal principal = new(identity);

        AuthenticationTicket ticket = new(
            principal,
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }

    private string ResolveSubject()
    {
        string customerId = Request
            .Headers[CustomerHeaderName]
            .ToString()
            .Trim();

        return string.IsNullOrWhiteSpace(customerId)
            ? DefaultSubject
            : customerId;
    }
}
