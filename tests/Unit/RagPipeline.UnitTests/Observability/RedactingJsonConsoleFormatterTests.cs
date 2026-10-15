namespace RagPipeline.UnitTests.Observability;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagPipeline.BuildingBlocks.Observability;
using Xunit;

public class RedactingJsonConsoleFormatterTests
{
    private const string CanaryPrefix = "CANARY-7f3a";

    [Fact]
    public void FormatName_IsConstantAndNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(RedactingJsonConsoleFormatter.FormatName));
    }

    [Fact]
    public void ParameterlessConstructor_CreatesFormatterWithMatchingName()
    {
        var formatter = new RedactingJsonConsoleFormatter();

        Assert.NotNull(formatter);
        Assert.Equal(RedactingJsonConsoleFormatter.FormatName, formatter.Name);
    }

    [Fact]
    public void Write_WithCanaryInConnectionString_NeverLeaksCanary()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        using var writer = new StringWriter();
        var canary = $"{CanaryPrefix}-conn-pass-987123";
        var message = $"Connecting to storage: Server=db.local;Database=rag;Password={canary};User Id=rag;";

        var logEntry = new LogEntry<string>(
            LogLevel.Information,
            "DatabaseCategory",
            new EventId(101, "DbConnect"),
            message,
            null,
            (s, _) => s);

        formatter.Write(logEntry, null, writer);
        var output = writer.ToString();

        Assert.DoesNotContain(canary, output);
        Assert.DoesNotContain(CanaryPrefix, output);
        Assert.Contains("Password=***", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_WithCanaryInBearerToken_NeverLeaksCanary()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        using var writer = new StringWriter();
        var canary = $"{CanaryPrefix}-bearer-tok-876234";
        var message = $"Forwarding request with header: Bearer {canary}";

        var logEntry = new LogEntry<string>(
            LogLevel.Information,
            "HttpCategory",
            new EventId(102, "OutboundHttp"),
            message,
            null,
            (s, _) => s);

        formatter.Write(logEntry, null, writer);
        var output = writer.ToString();

        Assert.DoesNotContain(canary, output);
        Assert.DoesNotContain(CanaryPrefix, output);
        Assert.Contains("Bearer ***", output);
    }

    [Fact]
    public void Write_WithCanaryInCookie_NeverLeaksCanary()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        using var writer = new StringWriter();
        var canary = $"{CanaryPrefix}-cookie-val-765345";
        var message = $"Received request with Cookie: session={canary}; id=42";

        var logEntry = new LogEntry<string>(
            LogLevel.Information,
            "HttpCategory",
            new EventId(103, "InboundHttp"),
            message,
            null,
            (s, _) => s);

        formatter.Write(logEntry, null, writer);
        var output = writer.ToString();

        Assert.DoesNotContain(canary, output);
        Assert.DoesNotContain(CanaryPrefix, output);
        Assert.Contains("Cookie: ***", output);
    }

    [Fact]
    public void Write_WithCanaryInJsonPassword_NeverLeaksCanary()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        using var writer = new StringWriter();
        var canary = $"{CanaryPrefix}-json-pwd-654456";
        var message = $"{{\"username\":\"alice\",\"password\":\"{canary}\",\"role\":\"admin\"}}";

        var logEntry = new LogEntry<string>(
            LogLevel.Information,
            "AuthCategory",
            new EventId(104, "Authenticate"),
            message,
            null,
            (s, _) => s);

        formatter.Write(logEntry, null, writer);
        var output = writer.ToString();

        Assert.DoesNotContain(canary, output);
        Assert.DoesNotContain(CanaryPrefix, output);
        Assert.Contains("***", output);
    }

    [Fact]
    public void Write_WithCanaryInExceptionMessage_NeverLeaksCanary()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        using var writer = new StringWriter();
        var canaryPass = $"{CanaryPrefix}-exc-pass-543567";
        var canaryToken = $"{CanaryPrefix}-exc-tok-432678";
        var canaryCookie = $"{CanaryPrefix}-exc-cookie-321789";
        var canaryJson = $"{CanaryPrefix}-exc-json-210890";

        var exception = new InvalidOperationException(
            $"Database failure: Server=db;Password={canaryPass}; " +
            $"Bearer {canaryToken} rejected; " +
            $"Cookie: session={canaryCookie}; " +
            $"{{\"password\":\"{canaryJson}\"}}");

        var logEntry = new LogEntry<string>(
            LogLevel.Error,
            "FaultCategory",
            new EventId(500, "Crash"),
            "Critical fault in execution pipeline",
            exception,
            (s, e) => e == null ? s : $"{s}: {e.Message}");

        formatter.Write(logEntry, null, writer);
        var output = writer.ToString();

        Assert.DoesNotContain(canaryPass, output);
        Assert.DoesNotContain(canaryToken, output);
        Assert.DoesNotContain(canaryCookie, output);
        Assert.DoesNotContain(canaryJson, output);
        Assert.DoesNotContain(CanaryPrefix, output);
    }

    [Fact]
    public void Write_WithCorrelationIdInScope_CorrelationIdAppearsInOutput()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        var scopeProvider = new LoggerExternalScopeProvider();
        const string expectedCorrelationId = "CANARY-CORRELATION-ID-7f3a-991188";

        using var scopeDict = scopeProvider.Push(new Dictionary<string, object?>
        {
            ["CorrelationId"] = expectedCorrelationId
        });
        using var scopeKvp = scopeProvider.Push(new KeyValuePair<string, object?>("CorrelationId", expectedCorrelationId));

        using var writer = new StringWriter();
        var logEntry = new LogEntry<string>(
            LogLevel.Information,
            "RequestCategory",
            new EventId(200, "RequestProcessed"),
            "Processed HTTP request successfully",
            null,
            (s, _) => s);

        formatter.Write(logEntry, scopeProvider, writer);
        var output = writer.ToString();

        Assert.Contains(expectedCorrelationId, output);
    }

    [Fact]
    public void Write_OutputsValidJsonDocument()
    {
        var formatter = new RedactingJsonConsoleFormatter();
        using var writer = new StringWriter();

        var logEntry = new LogEntry<string>(
            LogLevel.Warning,
            "HealthCheckCategory",
            new EventId(42, "Degraded"),
            "Subsystem latency elevated",
            null,
            (s, _) => s);

        formatter.Write(logEntry, null, writer);
        var output = writer.ToString().Trim();

        Assert.False(string.IsNullOrWhiteSpace(output));
        using var doc = JsonDocument.Parse(output);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }
}
