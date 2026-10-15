namespace RagPipeline.Api;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddRagPipelineOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new Microsoft.OpenApi.OpenApiInfo
                {
                    Title = "RagPipeline API",
                    Version = "v1"
                };
                document.Servers?.Clear();

                if (document.Components?.Schemas != null &&
                    document.Components.Schemas.TryGetValue("ProblemDetails", out var problemSchema) &&
                    problemSchema is Microsoft.OpenApi.OpenApiSchema concreteSchema)
                {
                    concreteSchema.Properties ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSchema>();
                    concreteSchema.Properties["traceId"] = new Microsoft.OpenApi.OpenApiSchema
                    {
                        Type = Microsoft.OpenApi.JsonSchemaType.String
                    };
                    concreteSchema.Required ??= new HashSet<string>();
                    concreteSchema.Required.Add("traceId");
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
