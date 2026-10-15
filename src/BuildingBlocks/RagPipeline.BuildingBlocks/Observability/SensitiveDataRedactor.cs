namespace RagPipeline.BuildingBlocks.Observability;

using System;
using System.Text.RegularExpressions;

public static class SensitiveDataRedactor
{
    public const string Mask = "***";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    // Matches Authorization: <header value>
    private static readonly Regex AuthorizationHeaderPattern = new(
        @"(?i)(?<prefix>\bAuthorization\s*:\s*)[^\r\n]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Matches Cookie: <header value> or Set-Cookie: <header value>
    private static readonly Regex CookieHeaderPattern = new(
        @"(?i)(?<prefix>\b(?:Set-Cookie|Cookie)\s*:\s*)[^\r\n]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Matches JSON-style "password":"x" string values for sensitive keys
    private static readonly Regex JsonStringPattern = new(
        @"(?i)(?<prefix>""(?<key>password|pwd|secret|key|token|connection[-_]?string|proof|cookie|authorization|apikey|api[-_]?key|bearer|session|access[-_]?token|refresh[-_]?token)""\s*:\s*)""[^""\r\n]*""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Matches URI user-info (e.g. postgres://user:pw@host)
    private static readonly Regex UriUserInfoPattern = new(
        @"(?i)(?<prefix>(?:[a-zA-Z][a-zA-Z0-9+.-]*:)?\/\/)[^\s/@]+(?<suffix>@)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Matches Bearer <token>
    private static readonly Regex BearerPattern = new(
        @"(?i)(?<prefix>\bBearer\s+)[^\s,;""\r\n]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Matches key=value pairs for sensitive keys:
    // password|pwd|secret|key|token|connectionstring|proof|cookie|authorization|apikey|bearer|session
    private static readonly Regex KeyValuePattern = new(
        @"(?i)(?<prefix>(?:^|[;\s,;&?])(?<key>password|pwd|secret|key|token|connection[-_]?string|proof|cookie|authorization|apikey|api[-_]?key|bearer|session|access[-_]?token|refresh[-_]?token)\s*=\s*)(?<val>""[^""]*""|'[^']*'|[^;\r\n\s,;&]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        try
        {
            var result = input;
            result = AuthorizationHeaderPattern.Replace(result, "${prefix}" + Mask);
            result = CookieHeaderPattern.Replace(result, "${prefix}" + Mask);
            result = JsonStringPattern.Replace(result, "${prefix}\"" + Mask + "\"");
            result = UriUserInfoPattern.Replace(result, "${prefix}" + Mask + "${suffix}");
            result = BearerPattern.Replace(result, "${prefix}" + Mask);
            result = KeyValuePattern.Replace(result, "${prefix}" + Mask);
            return result;
        }
        catch
        {
            return Mask;
        }
    }
}
