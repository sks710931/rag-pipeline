namespace RagPipeline.UnitTests.Session;

using System;
using System.Linq;
using System.Reflection;
using RagPipeline.BuildingBlocks.Session;
using Xunit;

public class SessionRecordTests
{
    [Fact]
    public void SessionRecord_StoresOnlyMinimalFields_NoRawToken()
    {
        var recordType = typeof(SessionRecord);
        var properties = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var propertyNames = properties.Select(p => p.Name).ToList();

        // Must contain RefreshTokenHash
        Assert.Contains("RefreshTokenHash", propertyNames);

        // Must NOT contain any raw token, password, or secret fields
        foreach (var name in propertyNames)
        {
            if (name == "RefreshTokenHash" || name == "RefreshTokenFamilyId")
            {
                continue;
            }

            Assert.DoesNotContain("Token", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Raw", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret", name, StringComparison.OrdinalIgnoreCase);
        }

        // Test instantiation
        var now = DateTimeOffset.UtcNow;
        var session = new SessionRecord(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RefreshTokenFamilyId: Guid.NewGuid(),
            RefreshTokenHash: "sha256-hash-only",
            ExpiresAt: now.AddDays(7),
            CreatedAt: now,
            LastSeenAt: now,
            IsRevoked: false,
            RevokedAt: null,
            RevocationReason: null);

        Assert.Equal("sha256-hash-only", session.RefreshTokenHash);
        Assert.False(session.IsRevoked);
    }

    [Fact]
    public void SessionRevocation_IsOneWay()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new SessionRecord(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RefreshTokenFamilyId: Guid.NewGuid(),
            RefreshTokenHash: "hash-123",
            ExpiresAt: now.AddDays(7),
            CreatedAt: now,
            LastSeenAt: now,
            IsRevoked: false,
            RevokedAt: null,
            RevocationReason: null);

        // 1. Initial revocation succeeds
        var revoked = session.Revoke("User logged out", now);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(now, revoked.RevokedAt);
        Assert.Equal("User logged out", revoked.RevocationReason);

        // 2. Calling revoke again on a revoked session throws InvalidOperationException (cannot un-revoke)
        Assert.Throws<InvalidOperationException>(() => revoked.Revoke("Attempt second revoke"));
    }
}
