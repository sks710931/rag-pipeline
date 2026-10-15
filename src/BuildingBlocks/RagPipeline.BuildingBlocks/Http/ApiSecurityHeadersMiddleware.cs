namespace RagPipeline.BuildingBlocks.Http;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public sealed class ApiSecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public ApiSecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsApiPath(context.Request.Path))
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Cache-Control"] = "no-store";
                headers["Pragma"] = "no-cache";
                headers["Referrer-Policy"] = "no-referrer";
                headers["X-Frame-Options"] = "DENY";
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    public static bool IsApiPath(PathString pathString)
    {
        var path = pathString.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string unescaped;
        try
        {
            unescaped = Uri.UnescapeDataString(path);
        }
        catch
        {
            unescaped = path;
        }

        var trimmed = unescaped.TrimStart('/');
        return string.Equals(trimmed, "api", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("api/", StringComparison.OrdinalIgnoreCase);
    }
}
