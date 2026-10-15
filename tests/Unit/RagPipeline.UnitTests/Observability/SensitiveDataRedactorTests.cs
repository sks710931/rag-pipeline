namespace RagPipeline.UnitTests.Observability;

using System;
using RagPipeline.BuildingBlocks.Observability;
using Xunit;

public class SensitiveDataRedactorTests
{
    private const string CanaryPrefix = "CANARY-7f3a";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_WithNullOrEmpty_ReturnsEmptyString(string? input)
    {
        var result = SensitiveDataRedactor.Redact(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void MaskConstant_IsThreeAsterisks()
    {
        Assert.Equal("***", SensitiveDataRedactor.Mask);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("pwd")]
    [InlineData("secret")]
    [InlineData("key")]
    [InlineData("token")]
    [InlineData("apikey")]
    [InlineData("api-key")]
    [InlineData("connectionstring")]
    [InlineData("connection-string")]
    public void Redact_WithConnectionStringKeys_NeverLeaksCanary(string key)
    {
        var canary = $"{CanaryPrefix}-conn-{key}-918273";
        var input = $"Server=10.0.0.1;Database=rag;User Id=appuser;{key}={canary};Port=5432;";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains($"{key}=***", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Database=rag", result);
        Assert.Contains("User Id=appuser", result);
    }

    [Fact]
    public void Redact_WithQuotedConnectionStringValue_NeverLeaksCanary()
    {
        var canary = $"{CanaryPrefix}-quoted-pass-443322";
        var input = $"Server=db.example.com;Password=\"{canary}\";Encrypt=True;";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("Password=***", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redact_WithUriUserInfo_NeverLeaksCanaryCredentials()
    {
        var canary = $"{CanaryPrefix}-uripass-112233";
        var input = $"postgres://svcaccount:{canary}@cluster-primary.internal:5432/ragpipeline";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("postgres://***@cluster-primary.internal:5432/ragpipeline", result);
    }

    [Fact]
    public void Redact_WithBearerToken_NeverLeaksCanaryToken()
    {
        var canary = $"{CanaryPrefix}-bearer-jwt-token-778899";
        var input = $"Bearer {canary}";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("Bearer ***", result);
    }

    [Fact]
    public void Redact_WithAuthorizationHeader_NeverLeaksCanaryToken()
    {
        var canary = $"{CanaryPrefix}-auth-header-998877";
        var input = $"Authorization: Bearer {canary}";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("Authorization: ***", result);
    }

    [Fact]
    public void Redact_WithCookieHeader_NeverLeaksCanaryCookie()
    {
        var canary = $"{CanaryPrefix}-cookie-session-556677";
        var input = $"Cookie: session={canary}; tracking_id=normal-value";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("Cookie: ***", result);
    }

    [Fact]
    public void Redact_WithSetCookieHeader_NeverLeaksCanaryCookie()
    {
        var canary = $"{CanaryPrefix}-setcookie-token-334455";
        var input = $"Set-Cookie: auth_token={canary}; Path=/; Secure; HttpOnly; SameSite=Strict";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("Set-Cookie: ***", result);
    }

    [Fact]
    public void Redact_WithCookieKeyValue_NeverLeaksCanary()
    {
        var canary = $"{CanaryPrefix}-cookie-keyval-123987";
        var input = $"cookie={canary}; domain=example.com";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains("cookie=***", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("apiKey")]
    [InlineData("connectionString")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    public void Redact_WithJsonSensitiveKeys_NeverLeaksCanary(string key)
    {
        var canary = $"{CanaryPrefix}-json-{key}-654321";
        var input = $"{{\"username\":\"testuser\", \"{key}\": \"{canary}\", \"active\": true}}";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(canary, result);
        Assert.DoesNotContain(CanaryPrefix, result);
        Assert.Contains($"\"{key}\": \"***\"", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"username\":\"testuser\"", result);
    }

    [Fact]
    public void Redact_WithExceptionMessageContainingCanarySecrets_MasksAllCanaries()
    {
        var canaryConn = $"{CanaryPrefix}-conn-exc-111";
        var canaryBearer = $"{CanaryPrefix}-bearer-exc-222";
        var canaryCookie = $"{CanaryPrefix}-cookie-exc-333";
        var canaryJson = $"{CanaryPrefix}-json-exc-444";

        var ex = new InvalidOperationException(
            $"Database query failed: Server=sql.corp;Password={canaryConn}; " +
            $"Authorization: Bearer {canaryBearer}; " +
            $"Cookie: session={canaryCookie}; " +
            $"{{\"password\": \"{canaryJson}\"}}");

        var result = SensitiveDataRedactor.Redact(ex.ToString());

        Assert.DoesNotContain(canaryConn, result);
        Assert.DoesNotContain(canaryBearer, result);
        Assert.DoesNotContain(canaryCookie, result);
        Assert.DoesNotContain(canaryJson, result);
        Assert.DoesNotContain(CanaryPrefix, result);
    }

    [Fact]
    public void Redact_WithSafeMessage_PreservesMessageUnchanged()
    {
        const string safeMessage = "Pipeline stage 'EmbeddingGeneration' succeeded in 35ms for doc-42.";

        var result = SensitiveDataRedactor.Redact(safeMessage);

        Assert.Equal(safeMessage, result);
    }
}
