namespace RagPipeline.BuildingBlocks.Observability;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string CorrelationIdItemKey = "CorrelationId";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex CorrelationIdPattern = new(
        @"^[A-Za-z0-9._-]{1,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId;
        var inbound = context.Request.Headers[CorrelationIdHeader].ToString();

        if (!string.IsNullOrEmpty(inbound) && CorrelationIdPattern.IsMatch(inbound))
        {
            correlationId = inbound;
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[CorrelationIdItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        var traceId = Activity.Current is { TraceId: var tid } && tid != default
            ? tid.ToString()
            : context.TraceIdentifier;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId,
            ["traceId"] = traceId
        }))
        {
            await _next(context);
        }
    }
}
