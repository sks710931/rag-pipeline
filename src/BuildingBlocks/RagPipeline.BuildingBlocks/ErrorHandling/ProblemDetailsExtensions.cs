namespace RagPipeline.BuildingBlocks.ErrorHandling;

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

public static class ProblemDetailsExtensions
{
    public const string ProblemJsonMediaType = "application/problem+json";

    public static IServiceCollection AddProblemDetailsContract(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var httpContext = context.HttpContext;
                var problem = context.ProblemDetails;

                var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
                problem.Extensions["traceId"] = traceId;

                if (string.IsNullOrEmpty(problem.Type))
                {
                    problem.Type = problem.Status switch
                    {
                        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                        StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        _ => $"https://httpstatuses.io/{problem.Status ?? StatusCodes.Status500InternalServerError}"
                    };
                }

                if (problem.Status >= StatusCodes.Status500InternalServerError)
                {
                    problem.Title = "An error occurred while processing your request.";
                    problem.Detail = null;
                }

                // Strictly ensure no stack traces or exception payloads are ever exposed
                problem.Extensions.Remove("exception");
                problem.Extensions.Remove("stackTrace");
            };
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    public static IApplicationBuilder UseProblemDetailsContract(this WebApplication app)
    {
        app.UseExceptionHandler();

        app.UseStatusCodePages(async context =>
        {
            var httpContext = context.HttpContext;
            httpContext.Response.ContentType = ProblemJsonMediaType;

            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            var statusCode = httpContext.Response.StatusCode;

            var problem = new ProblemDetails
            {
                Type = statusCode switch
                {
                    StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                    StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    _ => $"https://httpstatuses.io/{statusCode}"
                },
                Title = statusCode switch
                {
                    StatusCodes.Status401Unauthorized => "Unauthorized",
                    StatusCodes.Status403Forbidden => "Forbidden",
                    StatusCodes.Status404NotFound => "Not Found",
                    StatusCodes.Status400BadRequest => "Bad Request",
                    StatusCodes.Status500InternalServerError => "Internal Server Error",
                    _ => "Error"
                },
                Status = statusCode,
                Instance = httpContext.Request.Path
            };

            problem.Extensions["traceId"] = traceId;

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: ProblemJsonMediaType);
        });

        return app;
    }
}
