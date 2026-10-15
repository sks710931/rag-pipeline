namespace RagPipeline.BuildingBlocks.Configuration;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

public static class ConfigurationSanitizer
{
    private static readonly Regex SecretPattern = new(
        @"(?:password|pwd|secret|key|token|connectionstring)\s*=\s*[^;\r\n]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Sanitize(OptionsValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var typeName = exception.OptionsType?.Name;
        var header = !string.IsNullOrWhiteSpace(typeName) && typeName != "Object"
            ? $"Configuration validation failed for '{typeName}':"
            : !string.IsNullOrWhiteSpace(exception.OptionsName)
                ? $"Configuration validation failed for '{exception.OptionsName}':"
                : "Configuration validation failed:";

        var lines = new List<string> { header };

        var failures = exception.Failures ?? Enumerable.Empty<string>();
        var hasFailures = false;

        foreach (var failure in failures)
        {
            if (!string.IsNullOrWhiteSpace(failure))
            {
                lines.Add($"  - {SanitizeMessage(failure)}");
                hasFailures = true;
            }
        }

        if (!hasFailures && !string.IsNullOrWhiteSpace(exception.Message))
        {
            lines.Add($"  - {SanitizeMessage(exception.Message)}");
        }

        var result = string.Join(Environment.NewLine, lines);
        return RagPipeline.BuildingBlocks.Observability.SensitiveDataRedactor.Redact(result) ?? result;
    }

    public static string Sanitize(AggregateException aggregateException)
    {
        ArgumentNullException.ThrowIfNull(aggregateException);

        var optionsExceptions = aggregateException.InnerExceptions.OfType<OptionsValidationException>().ToList();
        if (optionsExceptions.Count > 0)
        {
            return string.Join(Environment.NewLine, optionsExceptions.Select(Sanitize));
        }

        return SanitizeMessage(aggregateException.Message);
    }

    public static string Sanitize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OptionsValidationException optionsEx)
        {
            return Sanitize(optionsEx);
        }

        if (exception is AggregateException aggEx)
        {
            return Sanitize(aggEx);
        }

        return SanitizeMessage(exception.Message);
    }

    public static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // Strip stack trace indicators if present
        var stackTraceIndex = message.IndexOf("   at ", StringComparison.OrdinalIgnoreCase);
        if (stackTraceIndex >= 0)
        {
            message = message[..stackTraceIndex].TrimEnd();
        }

        // Mask secret-looking key=value pairs
        var sanitized = SecretPattern.Replace(message, match =>
        {
            var equalsIndex = match.Value.IndexOf('=');
            if (equalsIndex > 0)
            {
                var prefix = match.Value[..equalsIndex];
                return $"{prefix}=***";
            }

            return match.Value;
        });

        sanitized = RagPipeline.BuildingBlocks.Observability.SensitiveDataRedactor.Redact(sanitized) ?? sanitized;

        return sanitized;
    }
}
