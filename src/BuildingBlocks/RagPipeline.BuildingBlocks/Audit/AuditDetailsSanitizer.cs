namespace RagPipeline.BuildingBlocks.Audit;

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RagPipeline.BuildingBlocks.Observability;

public static class AuditDetailsSanitizer
{
    public const int DefaultMaxFreeTextLength = 1024;
    public const string TruncatedSuffix = "...[TRUNCATED]";

    private static readonly Regex SensitiveKeyPattern = new(
        @"(?i)^(password|pwd|secret|key|token|connection[-_]?string|proof|cookie|authorization|apikey|api[-_]?key|bearer|session|access[-_]?token|refresh[-_]?token|mek|master[-_]?key|recovery[-_]?phrase|recovery[-_]?key|passphrase|credential)s?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    public static string? Sanitize(string? input, int maxFreeTextLength = DefaultMaxFreeTextLength)
    {
        if (input == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(input);
            if (node == null)
            {
                return SanitizeFreeText(input, maxFreeTextLength);
            }

            SanitizeNode(node, maxFreeTextLength);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return SanitizeFreeText(input, maxFreeTextLength);
        }
    }

    public static string? SanitizeObject(object? obj, int maxFreeTextLength = DefaultMaxFreeTextLength)
    {
        if (obj == null)
        {
            return null;
        }

        if (obj is string str)
        {
            return Sanitize(str, maxFreeTextLength);
        }

        var json = JsonSerializer.Serialize(obj);
        return Sanitize(json, maxFreeTextLength);
    }

    public static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return SensitiveKeyPattern.IsMatch(key);
    }

    private static void SanitizeNode(JsonNode node, int maxFreeTextLength)
    {
        if (node is JsonObject obj)
        {
            var propertiesToReplace = new System.Collections.Generic.List<string>();
            foreach (var kvp in obj)
            {
                if (IsSensitiveKey(kvp.Key))
                {
                    propertiesToReplace.Add(kvp.Key);
                }
                else if (kvp.Value != null)
                {
                    SanitizeNode(kvp.Value, maxFreeTextLength);
                }
            }

            foreach (var prop in propertiesToReplace)
            {
                obj[prop] = SensitiveDataRedactor.Mask;
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                var item = arr[i];
                if (item != null)
                {
                    SanitizeNode(item, maxFreeTextLength);
                }
            }
        }
        else if (node is JsonValue val && val.TryGetValue<string>(out var strVal))
        {
            var redacted = SensitiveDataRedactor.Redact(strVal);
            if (redacted.Length > maxFreeTextLength)
            {
                redacted = string.Concat(redacted.AsSpan(0, maxFreeTextLength), TruncatedSuffix);
            }

            // Replace in-place
            if (node.Parent is JsonObject parentObj)
            {
                var key = FindKeyInParent(parentObj, node);
                if (key != null)
                {
                    parentObj[key] = redacted;
                }
            }
            else if (node.Parent is JsonArray parentArr)
            {
                var idx = parentArr.IndexOf(node);
                if (idx >= 0)
                {
                    parentArr[idx] = redacted;
                }
            }
        }
    }

    private static string? FindKeyInParent(JsonObject parent, JsonNode child)
    {
        foreach (var kvp in parent)
        {
            if (ReferenceEquals(kvp.Value, child))
            {
                return kvp.Key;
            }
        }

        return null;
    }

    private static string SanitizeFreeText(string text, int maxFreeTextLength)
    {
        var redacted = SensitiveDataRedactor.Redact(text);
        if (redacted.Length > maxFreeTextLength)
        {
            return string.Concat(redacted.AsSpan(0, maxFreeTextLength), TruncatedSuffix);
        }

        return redacted;
    }
}
