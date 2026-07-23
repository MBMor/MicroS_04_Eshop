using System.Security.Claims;
using System.Text.Encodings.Web;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiGateway.IntegrationTests.Infrastructure;

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
        "ApiGatewayIntegrationTest";

    public const string SubjectHeaderName =
        "X-Test-Subject";

    public const string RolesHeaderName =
        "X-Test-Roles";

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        string subject = Request
            .Headers[SubjectHeaderName]
            .ToString()
            .Trim();

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        Claim[] identityClaims =
        [
            new(
                EshopClaimNames.Subject,
                subject),

            new(
                EshopClaimNames.PreferredUsername,
                subject),

            new(
                EshopClaimNames.Email,
                $"{subject}@eshop.local")
        ];

        IEnumerable<Claim> roleClaims =
            ResolveRoles()
                .Select(
                    static role => new Claim(
                        EshopClaimNames.Roles,
                        role));

        ClaimsIdentity identity = new(
            identityClaims.Concat(roleClaims),
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

    private IEnumerable<string> ResolveRoles()
    {
        return Request
            .Headers[RolesHeaderName]
            .ToString()
            .Split(
                ',',
                StringSplitOptions.TrimEntries
                | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal);
    }
}
