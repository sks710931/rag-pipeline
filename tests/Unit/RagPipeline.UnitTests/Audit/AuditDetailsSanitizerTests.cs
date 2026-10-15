namespace RagPipeline.UnitTests.Audit;

using System;
using System.Text.Json;
using RagPipeline.BuildingBlocks.Audit;
using Xunit;

public class AuditDetailsSanitizerTests
{
    private const string CanaryPrefix = "CANARY-7f3a";

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("proof")]
    [InlineData("token")]
    [InlineData("mek")]
    [InlineData("MEK")]
    [InlineData("master-key")]
    [InlineData("master_key")]
    [InlineData("recovery-phrase")]
    [InlineData("recoveryPhrase")]
    [InlineData("recovery_phrase")]
    [InlineData("apiKey")]
    [InlineData("secret")]
    [InlineData("connection-string")]
    public void AuditDetailsSanitizer_RedactsPasswordProofTokenMekRecoveryPhrase(string key)
    {
        var canary = $"{CanaryPrefix}-{key}-918273";
        var json = $"{{\"{key}\":\"{canary}\",\"otherField\":\"safe-value\"}}";

        var sanitized = AuditDetailsSanitizer.Sanitize(json);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain(canary, sanitized);
        Assert.DoesNotContain(CanaryPrefix, sanitized);
        Assert.Contains("\"***\"", sanitized);
        Assert.Contains("safe-value", sanitized);
    }

    [Fact]
    public void AuditDetailsSanitizer_RedactsNestedJsonStructures()
    {
        var canary1 = $"{CanaryPrefix}-nested-mek-998877";
        var canary2 = $"{CanaryPrefix}-nested-recovery-554433";
        var canary3 = $"{CanaryPrefix}-nested-token-221100";

        var nestedJson = $$"""
        {
            "deployment": {
                "name": "Production-Cluster",
                "keys": {
                    "mek": "{{canary1}}",
                    "phrase": {
                        "recoveryPhrase": "{{canary2}}"
                    }
                }
            },
            "sessions": [
                {
                    "user": "alice",
                    "token": "{{canary3}}"
                }
            ]
        }
        """;

        var sanitized = AuditDetailsSanitizer.Sanitize(nestedJson);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain(canary1, sanitized);
        Assert.DoesNotContain(canary2, sanitized);
        Assert.DoesNotContain(canary3, sanitized);
        Assert.DoesNotContain(CanaryPrefix, sanitized);
        Assert.Contains("Production-Cluster", sanitized);
        Assert.Contains("alice", sanitized);
    }

    [Fact]
    public void AuditDetailsSanitizer_UnknownFreeTextFields_AreNotAcceptedOrAreTruncated()
    {
        // 1. Oversized field in JSON
        var longString = new string('A', 2048);
        var json = JsonSerializer.Serialize(new { note = longString, count = 42 });

        var sanitized = AuditDetailsSanitizer.Sanitize(json, maxFreeTextLength: 1024);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain(longString, sanitized);
        Assert.Contains(AuditDetailsSanitizer.TruncatedSuffix, sanitized);
        Assert.Contains("42", sanitized);

        // 2. Free text input that is not valid JSON
        var freeText = "Free-form audit log message: " + new string('X', 2000);
        var sanitizedFreeText = AuditDetailsSanitizer.Sanitize(freeText, maxFreeTextLength: 100);

        Assert.NotNull(sanitizedFreeText);
        Assert.EndsWith(AuditDetailsSanitizer.TruncatedSuffix, sanitizedFreeText);
        Assert.True(sanitizedFreeText.Length <= 100 + AuditDetailsSanitizer.TruncatedSuffix.Length);
    }
}
