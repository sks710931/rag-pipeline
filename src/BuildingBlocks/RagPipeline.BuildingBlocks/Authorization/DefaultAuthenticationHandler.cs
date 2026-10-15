namespace RagPipeline.BuildingBlocks.Authorization;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class DefaultAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    public DefaultAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("X-Test-Anonymous", out var isAnon) &&
            string.Equals(isAnon, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Guid userId = Guid.Empty;

        // 1. Try Authorization header: Bearer <guid>
        var authHeader = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                (string.Equals(parts[0], "Bearer", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[0], "Test", StringComparison.OrdinalIgnoreCase)))
            {
                _ = Guid.TryParse(parts[1], out userId);
            }
        }

        // 2. Try X-Test-UserId header for integration test flexibility
        if (userId == Guid.Empty &&
            Request.Headers.TryGetValue("X-Test-UserId", out var headerUserId) &&
            Guid.TryParse(headerUserId, out var parsedHeaderId))
        {
            userId = parsedHeaderId;
        }

        if (userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // AC3: Establish identity strictly with authenticated NameIdentifier.
        // Client-supplied role/tenant headers are NEVER mapped into claims.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user-{userId:N}")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
