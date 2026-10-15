namespace RagPipeline.IntegrationTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagPipeline.BuildingBlocks.ErrorHandling;
using RagPipeline.BuildingBlocks.Modules;
using RagPipeline.BuildingBlocks.Observability;
using Xunit;

public class ObservabilityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CanaryPrefix = "CANARY-7f3a";
    private readonly WebApplicationFactory<Program> _factory;

    public ObservabilityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SecurityHeaders_OnHealthLive_ArePresent()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task SecurityHeaders_OnHealthReady503_ArePresent()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task SecurityHeaders_OnNotFoundUnderApi_ArePresent()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/unknown-route-that-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task SecurityHeaders_OnMethodNotAllowedUnderApi_ArePresent()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/v1/system/health/live", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task SecurityHeaders_OnInternalServerError500_ArePresent()
    {
        const string exceptionMessage = "FaultInjectionFailure";
        var faultyModule = new CanaryFaultyEndpointFilter(exceptionMessage);

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IModule>(faultyModule);
                services.AddSingleton<IStartupFilter>(faultyModule);
            });
        });

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync(CanaryFaultyEndpointFilter.EndpointRoute);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);
        AssertSecurityHeaders(response);
    }

    [Theory]
    [InlineData("/api/v1/system/health/live", "GET", HttpStatusCode.OK)]
    [InlineData("/api/v1/system/health/ready", "GET", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/api/v1/system/route-does-not-exist", "GET", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/system/health/live", "POST", HttpStatusCode.MethodNotAllowed)]
    public async Task SecurityHeaders_ArePresentOnAllApiResponses(string path, string method, HttpStatusCode expectedStatus)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task NonApiPath_DoesNotForceApiSecurityHeaders()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/non-api-path");

        var hasPragmaNoCache = (response.Headers.TryGetValues("Pragma", out var pragmaValues) ||
                                response.Content.Headers.TryGetValues("Pragma", out pragmaValues)) &&
                               pragmaValues.Any(v => v.Contains("no-cache", StringComparison.OrdinalIgnoreCase));

        var hasCacheControlNoStore = (response.Headers.TryGetValues("Cache-Control", out var ccValues) ||
                                      response.Content.Headers.TryGetValues("Cache-Control", out ccValues)) &&
                                     ccValues.Any(v => v.Contains("no-store", StringComparison.OrdinalIgnoreCase));

        Assert.False(hasPragmaNoCache && hasCacheControlNoStore,
            "API security headers (such as Pragma: no-cache and Cache-Control: no-store) must not be forced on non-/api paths.");

        if (response.Headers.TryGetValues("Referrer-Policy", out var rpValues))
        {
            Assert.DoesNotContain("no-referrer", string.Join(", ", rpValues));
        }
    }

    [Fact]
    public async Task CorrelationId_ValidInbound_IsEchoedInResponse()
    {
        using var client = _factory.CreateClient();
        const string validCorrelationId = "test-correlation-id-abc-12345";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/health/live");
        request.Headers.Add("X-Correlation-Id", validCorrelationId);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var echoedValues),
            "Expected X-Correlation-Id header to be present on response.");
        Assert.Equal(validCorrelationId, echoedValues.FirstOrDefault());
    }

    [Fact]
    public async Task CorrelationId_InvalidInbound_IsReplacedInResponse()
    {
        using var client = _factory.CreateClient();
        const string invalidCorrelationId = "invalid correlation id with spaces and <script>";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", invalidCorrelationId);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var replacedValues),
            "Expected X-Correlation-Id header to be present on response.");
        var returnedId = replacedValues.FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(returnedId));
        Assert.NotEqual(invalidCorrelationId, returnedId);
    }

    [Fact]
    public async Task CorrelationId_MissingInbound_IsGeneratedInResponse()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/health/live");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var generatedValues),
            "Expected X-Correlation-Id header to be generated and present on response.");
        var returnedId = generatedValues.FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(returnedId));
    }

    [Fact]
    public async Task CanaryTest_SecretsNeverAppearInResponseOrCapturedLogs_AndReturnsProblemDetailsWithTraceId()
    {
        var canaryPassword = $"{CanaryPrefix}-DbPasswordSecret-98765";
        var canaryExceptionSecret = $"{CanaryPrefix}-ExceptionSecret-54321";
        var canaryBearer = $"{CanaryPrefix}-BearerToken-67890";
        var canaryCookie = $"{CanaryPrefix}-CookieAuth-13579";
        var canaryJsonSecret = $"{CanaryPrefix}-JsonSecret-24680";

        var canaryConnectionString = $"Host=db.internal;Database=rag;User Id=raguser;Password={canaryPassword};";

        var capturingProvider = new CapturingLoggerProvider();

        var exceptionMessage = $"Crash failure with connection '{canaryConnectionString}', " +
                               $"Authorization: Bearer {canaryBearer}, " +
                               $"Cookie: session={canaryCookie}, " +
                               $"{{\"password\":\"{canaryJsonSecret}\"}}, " +
                               $"internal detail: {canaryExceptionSecret}";

        var faultyFilter = new CanaryFaultyEndpointFilter(exceptionMessage);

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hosting:ApplicationName"] = "RagPipeline.Api",
                    ["ConnectionStrings:Database"] = canaryConnectionString,
                    ["ConnectionStrings:DefaultConnection"] = canaryConnectionString,
                    ["Database:ConnectionString"] = canaryConnectionString
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(capturingProvider);
                services.AddSingleton<IStartupFilter>(faultyFilter);
                services.AddSingleton<IModule>(faultyFilter);
            });
        });

        using var client = customFactory.CreateClient();

        var response = await client.GetAsync(CanaryFaultyEndpointFilter.EndpointRoute);

        // 1. Assert status 500 and Content-Type application/problem+json
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemDetailsExtensions.ProblemJsonMediaType, response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();

        // 2. Assert 500 problem response has traceId
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("traceId", out var traceIdProp), "Expected problem details to contain 'traceId'.");
        var traceId = traceIdProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));

        // 3. Security headers must be present on 500 problem response
        AssertSecurityHeaders(response);

        // 4. Assert no canary appears in the HTTP responses (status, headers, body)
        var allCanaries = new[]
        {
            canaryPassword,
            canaryExceptionSecret,
            canaryBearer,
            canaryCookie,
            canaryJsonSecret,
            CanaryPrefix
        };

        foreach (var canary in allCanaries)
        {
            if (!string.IsNullOrEmpty(response.ReasonPhrase))
            {
                Assert.DoesNotContain(canary, response.ReasonPhrase);
            }

            Assert.DoesNotContain(canary, content);

            foreach (var header in response.Headers)
            {
                Assert.DoesNotContain(canary, header.Key);
                foreach (var val in header.Value)
                {
                    Assert.DoesNotContain(canary, val);
                }
            }

            foreach (var header in response.Content.Headers)
            {
                Assert.DoesNotContain(canary, header.Key);
                foreach (var val in header.Value)
                {
                    Assert.DoesNotContain(canary, val);
                }
            }
        }

        // 5. Assert no canary appears in captured log output
        Assert.NotEmpty(capturingProvider.CapturedLogs);
        foreach (var log in capturingProvider.CapturedLogs)
        {
            foreach (var canary in allCanaries)
            {
                Assert.DoesNotContain(canary, log);
            }
        }
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "Cache-Control", "no-store");
        AssertHeader(response, "Pragma", "no-cache");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
        AssertHeader(response, "X-Frame-Options", "DENY");
    }

    private static void AssertHeader(HttpResponseMessage response, string headerName, string expectedValue)
    {
        var found = response.Headers.TryGetValues(headerName, out var values)
            || response.Content.Headers.TryGetValues(headerName, out values);

        Assert.True(found, $"Expected security header '{headerName}' to be present on the response.");
        var joined = string.Join(", ", values!);
        Assert.Contains(expectedValue, joined, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CanaryFaultyEndpointFilter(string canaryExceptionMessage) : IStartupFilter, IModule
    {
        public const string EndpointRoute = "/api/v1/test/throw-canary";

        public string Name => "CanaryFaultyEndpoint";

        public void RegisterModule(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet(EndpointRoute, () =>
            {
                throw new InvalidOperationException(canaryExceptionMessage);
            });
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);
                try
                {
                    if (app is IEndpointRouteBuilder endpoints)
                    {
                        endpoints.MapGet("/api/v1/test/throw-canary-filter", () =>
                        {
                            throw new InvalidOperationException(canaryExceptionMessage);
                        });
                    }
                }
                catch
                {
                    // Ignore if route already mapped
                }
            };
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentBag<string> _capturedLogs = new();
        private readonly RedactingJsonConsoleFormatter _formatter = new();
        private IExternalScopeProvider? _scopeProvider;

        public IEnumerable<string> CapturedLogs => _capturedLogs;

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(this, categoryName);
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return provider._scopeProvider?.Push(state);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                try
                {
                    using var writer = new StringWriter();
                    var entry = new LogEntry<TState>(logLevel, categoryName, eventId, state, exception, formatter);
                    provider._formatter.Write(entry, provider._scopeProvider, writer);
                    var rendered = writer.ToString();
                    if (!string.IsNullOrWhiteSpace(rendered))
                    {
                        provider._capturedLogs.Add(rendered);
                    }
                }
                catch
                {
                    // Fallback to SensitiveDataRedactor if formatter throws
                }

                var rawMessage = formatter(state, exception);
                var exceptionText = exception?.ToString();
                var combined = string.IsNullOrEmpty(exceptionText)
                    ? rawMessage
                    : $"{rawMessage} {exceptionText}";

                var redacted = SensitiveDataRedactor.Redact(combined);
                provider._capturedLogs.Add(redacted);
            }
        }
    }
}
