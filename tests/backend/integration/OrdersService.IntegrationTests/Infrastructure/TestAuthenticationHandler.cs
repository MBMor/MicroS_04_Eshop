using System.Security.Claims;
using System.Text.Encodings.Web;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace OrdersService.IntegrationTests.Infrastructure;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        loggerFactory,
        encoder)
{
    public const string SchemeName =
        "OrdersServiceIntegrationTests";

    public const string SubjectHeaderName =
        "X-Test-Subject";

    public const string RolesHeaderName =
        "X-Test-Roles";

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                SubjectHeaderName,
                out StringValues subjectValues))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        string subject =
            subjectValues.ToString().Trim();

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult(
                AuthenticateResult.Fail(
                    "The test subject header is empty."));
        }

        List<Claim> claims =
        [
            new(
                EshopClaimNames.Subject,
                subject),

            new(
                EshopClaimNames.PreferredUsername,
                subject)
        ];

        if (Request.Headers.TryGetValue(
                RolesHeaderName,
                out StringValues roleValues))
        {
            claims.AddRange(
                roleValues
                    .ToString()
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.Ordinal)
                    .Select(
                        static role =>
                            new Claim(
                                EshopClaimNames.Roles,
                                role)));
        }

        ClaimsIdentity identity = new(
            claims,
            SchemeName,
            EshopClaimNames.PreferredUsername,
            EshopClaimNames.Roles);

        ClaimsPrincipal principal =
            new(identity);

        AuthenticationTicket ticket = new(
            principal,
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}
