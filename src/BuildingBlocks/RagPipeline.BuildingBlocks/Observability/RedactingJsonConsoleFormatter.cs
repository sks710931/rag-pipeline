namespace RagPipeline.BuildingBlocks.Observability;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

public sealed class RedactingJsonConsoleFormatter : ConsoleFormatter
{
    public const string FormatName = "redacted-json";

    public RedactingJsonConsoleFormatter() : base(FormatName)
    {
    }

    public RedactingJsonConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options) : base(FormatName)
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        var message = logEntry.Formatter != null
            ? logEntry.Formatter(logEntry.State, logEntry.Exception)
            : logEntry.State?.ToString();

        var (scopeCorrelationId, scopeTraceId) = ExtractScopeIdentifiers(scopeProvider);

        string? correlationId = scopeCorrelationId != null
            ? SensitiveDataRedactor.Redact(scopeCorrelationId)
            : null;

        string? traceId = scopeTraceId != null
            ? SensitiveDataRedactor.Redact(scopeTraceId)
            : null;

        using var memoryStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memoryStream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("level", logEntry.LogLevel.ToString());
            writer.WriteString("category", SensitiveDataRedactor.Redact(logEntry.Category));
            writer.WriteNumber("eventId", logEntry.EventId.Id);
            writer.WriteString("message", SensitiveDataRedactor.Redact(message));

            if (correlationId != null)
            {
                writer.WriteString("correlationId", correlationId);
            }

            if (traceId != null)
            {
                writer.WriteString("traceId", traceId);
            }

            if (logEntry.Exception != null)
            {
                string exceptionText;
                try
                {
                    exceptionText = logEntry.Exception.ToString();
                }
                catch
                {
                    exceptionText = logEntry.Exception.GetType().FullName ?? "Exception";
                }

                var redactedException = SensitiveDataRedactor.Redact(exceptionText);
                writer.WriteString("exception", redactedException);
            }

            writer.WriteEndObject();
            writer.Flush();
        }

        var jsonLine = Encoding.UTF8.GetString(memoryStream.ToArray());
        textWriter.WriteLine(jsonLine);
    }

    private static (string? CorrelationId, string? TraceId) ExtractScopeIdentifiers(IExternalScopeProvider? scopeProvider)
    {
        if (scopeProvider == null)
        {
            return (null, null);
        }

        var stateContext = new ScopeIdentifiers();
        scopeProvider.ForEachScope((scope, state) =>
        {
            ProcessScope(scope, state);
        }, stateContext);

        return (stateContext.CorrelationId, stateContext.TraceId);
    }

    private static void ProcessScope(object? scope, ScopeIdentifiers state)
    {
        if (scope == null)
        {
            return;
        }

        if (scope is IEnumerable<KeyValuePair<string, object?>> kvpList)
        {
            foreach (var kvp in kvpList)
            {
                ProcessKvp(kvp.Key, kvp.Value, state);
            }
            return;
        }

        if (scope is IEnumerable<KeyValuePair<string, object>> kvpListNonNullable)
        {
            foreach (var kvp in kvpListNonNullable)
            {
                ProcessKvp(kvp.Key, kvp.Value, state);
            }
            return;
        }

        if (scope is IEnumerable<KeyValuePair<string, string>> kvpListString)
        {
            foreach (var kvp in kvpListString)
            {
                ProcessKvp(kvp.Key, kvp.Value, state);
            }
            return;
        }

        if (scope is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                ProcessKvp(entry.Key?.ToString(), entry.Value, state);
            }
            return;
        }

        if (scope is KeyValuePair<string, object?> singleNullableKvp)
        {
            ProcessKvp(singleNullableKvp.Key, singleNullableKvp.Value, state);
            return;
        }

        if (scope is KeyValuePair<string, object> singleKvp)
        {
            ProcessKvp(singleKvp.Key, singleKvp.Value, state);
            return;
        }

        if (scope is not string)
        {
            try
            {
                var properties = scope.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    ProcessKvp(prop.Name, prop.GetValue(scope), state);
                }
            }
            catch
            {
                // Ignore reflection errors on non-standard scope objects
            }
        }
    }

    private static void ProcessKvp(string? key, object? value, ScopeIdentifiers state)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (string.Equals(key, "correlationId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "X-Correlation-Id", StringComparison.OrdinalIgnoreCase))
        {
            state.CorrelationId = value?.ToString();
        }
        else if (string.Equals(key, "traceId", StringComparison.OrdinalIgnoreCase))
        {
            state.TraceId = value?.ToString();
        }
    }

    private sealed class ScopeIdentifiers
    {
        public string? CorrelationId { get; set; }
        public string? TraceId { get; set; }
    }
}
