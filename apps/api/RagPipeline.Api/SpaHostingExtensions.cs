namespace RagPipeline.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

public static class SpaHostingExtensions
{
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

        // Never allow default files or static file middleware to handle /api/* requests
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/api"),
            nonApiApp =>
            {
                nonApiApp.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = fileProvider
                });

                nonApiApp.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider
                });
            });

        // SPA Fallback: client-side routes fallback to index.html, strictly excluding /api/*
        app.MapFallback(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var indexFileInfo = fileProvider.GetFileInfo("index.html");
            if (indexFileInfo.Exists && indexFileInfo.PhysicalPath != null)
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.SendFileAsync(indexFileInfo.PhysicalPath);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        return app;
    }
}
