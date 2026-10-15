namespace RagPipeline.Api;

using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

public static class SpaHostingExtensions
{
    public const string ContentSecurityPolicyHeaderValue =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    public const string ReferrerPolicyHeaderValue = "strict-origin-when-cross-origin";
    public const string XFrameOptionsHeaderValue = "DENY";
    public const string XContentTypeOptionsHeaderValue = "nosniff";
    public const string IndexCacheControlHeaderValue = "no-cache";
    public const string AssetsCacheControlHeaderValue = "public, max-age=31536000, immutable";

    public static string? ResolveSpaRoot(IWebHostEnvironment environment, IConfiguration configuration)
    {
        // 1. Explicit configuration (e.g., from test fixture or environment)
        var configuredPath = configuration["Spa:Root"] ?? configuration["WebRoot"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullConfiguredPath = Path.GetFullPath(configuredPath, environment.ContentRootPath);
            if (Directory.Exists(fullConfiguredPath))
            {
                return fullConfiguredPath;
            }
        }

        // 2. apps/web/dist relative to ContentRootPath (local run, IDE, tests)
        var relativeWebDist = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "web", "dist"));
        if (Directory.Exists(relativeWebDist))
        {
            return relativeWebDist;
        }

        // 3. Search upwards for repo root containing RagPipeline.slnx
        var currentDir = new DirectoryInfo(environment.ContentRootPath);
        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "apps", "web", "dist");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(currentDir.FullName, "RagPipeline.slnx")))
            {
                break;
            }

            currentDir = currentDir.Parent;
        }

        // 4. Fallback to wwwroot in ContentRootPath (e.g. published container)
        var wwwroot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            return wwwroot;
        }

        return null;
    }

    public static WebApplication UseSpaHosting(this WebApplication app)
    {
        var spaRoot = ResolveSpaRoot(app.Environment, app.Configuration);
        if (spaRoot == null)
        {
            // When apps/web has not been built and no static assets are present,
            // gracefully skip static files and SPA fallback (no hard failure).
            return app;
        }

        var fileProvider = new PhysicalFileProvider(spaRoot);

        // Never allow static file middleware or headers to handle /api/* requests
        app.UseWhen(
            context => !IsApiRequest(context.Request.Path),
            nonApiApp =>
            {
                nonApiApp.Use(async (context, next) =>
                {
                    ApplySecurityHeaders(context.Response.Headers);
                    await next();
                });

                nonApiApp.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    OnPrepareResponse = ctx =>
                    {
                        ApplySecurityHeaders(ctx.Context.Response.Headers);

                        if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Context.Response.Headers.CacheControl = IndexCacheControlHeaderValue;
                        }
                        else if (ctx.Context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Context.Response.Headers.CacheControl = AssetsCacheControlHeaderValue;
                        }
                    }
                });
            });

        // SPA Fallback: client-side routes fallback to index.html, strictly excluding /api/*
        app.MapFallback(async context =>
        {
            if (IsApiRequest(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.Headers.Allow = "GET, HEAD";
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            if (!AcceptsHtml(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var indexFileInfo = fileProvider.GetFileInfo("index.html");
            if (indexFileInfo.Exists)
            {
                ApplySecurityHeaders(context.Response.Headers);
                context.Response.Headers.CacheControl = IndexCacheControlHeaderValue;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.StatusCode = StatusCodes.Status200OK;

                if (HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.ContentLength = indexFileInfo.Length;
                    return;
                }

                if (indexFileInfo.PhysicalPath != null)
                {
                    await context.Response.SendFileAsync(indexFileInfo.PhysicalPath);
                }
                else
                {
                    await using var stream = indexFileInfo.CreateReadStream();
                    await stream.CopyToAsync(context.Response.Body);
                }

                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        return app;
    }

    public static void ApplySecurityHeaders(IHeaderDictionary headers)
    {
        headers["X-Content-Type-Options"] = XContentTypeOptionsHeaderValue;
        headers["Referrer-Policy"] = ReferrerPolicyHeaderValue;
        headers["X-Frame-Options"] = XFrameOptionsHeaderValue;
        headers["Content-Security-Policy"] = ContentSecurityPolicyHeaderValue;
    }

    private static bool IsApiRequest(PathString path)
    {
        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var pathValue = path.Value;
        if (!string.IsNullOrEmpty(pathValue))
        {
            var unescaped = Uri.UnescapeDataString(pathValue);
            if (unescaped.Equals("/api", StringComparison.OrdinalIgnoreCase) ||
                unescaped.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AcceptsHtml(HttpRequest request)
    {
        var accept = request.Headers.Accept;
        if (StringValues.IsNullOrEmpty(accept))
        {
            return true;
        }

        var hasAnyNonEmpty = false;
        foreach (var value in accept)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                hasAnyNonEmpty = true;
                break;
            }
        }

        if (!hasAnyNonEmpty)
        {
            return true;
        }

        if (MediaTypeHeaderValue.TryParseList(accept, out var mediaTypes) && mediaTypes.Count > 0)
        {
            foreach (var mediaType in mediaTypes)
            {
                if (mediaType.Quality.HasValue && mediaType.Quality.Value <= 0.0)
                {
                    continue;
                }

                if (mediaType.MatchesAllTypes ||
                    (mediaType.Type.Equals("text", StringComparison.OrdinalIgnoreCase) && mediaType.MatchesAllSubTypes) ||
                    mediaType.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
                    mediaType.MediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var rawValue in accept)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            if (rawValue.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
                rawValue.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
                rawValue.Contains("*/*", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
