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

        var lines = new List<string>
        {
            $"Configuration validation failed for '{exception.OptionsType.Name}':"
        };

        foreach (var failure in exception.Failures)
        {
            lines.Add($"  - {SanitizeMessage(failure)}");
        }

        return string.Join(Environment.NewLine, lines);
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

        return sanitized;
    }
}
