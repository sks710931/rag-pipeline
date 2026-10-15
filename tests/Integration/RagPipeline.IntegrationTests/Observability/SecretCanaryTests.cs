namespace RagPipeline.IntegrationTests.Observability;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagPipeline.BuildingBlocks.Audit;
using RagPipeline.BuildingBlocks.Observability;
using RagPipeline.IntegrationTests.Persistence;
using RagPipeline.Persistence.Audit;
using RagPipeline.Persistence.Tenancy;
using Xunit;

[Collection("PostgresDatabase")]
public class SecretCanaryTests
{
    private const string CanaryPrefix = "CANARY-7f3a";
    private readonly PostgresFixture _fixture;

    public SecretCanaryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CanaryScan_AuditRowsAndLogs_NeverContainSecrets()
    {
        // 1. Generate unique high-entropy canaries
        var passwordCanary = $"{CanaryPrefix}-pwd-{Guid.NewGuid():N}";
        var proofCanary = $"{CanaryPrefix}-proof-{Guid.NewGuid():N}";
        var tokenCanary = $"{CanaryPrefix}-tok-{Guid.NewGuid():N}";
        var mekCanary = $"{CanaryPrefix}-mek-{Guid.NewGuid():N}";
        var recoveryPhraseCanary = $"{CanaryPrefix}-recovery-{Guid.NewGuid():N}";

        var canaries = new[]
        {
            passwordCanary,
            proofCanary,
            tokenCanary,
            mekCanary,
            recoveryPhraseCanary
        };

        // 2. Test AuditDetailsSanitizer on nested object with all canaries
        var rawAuditDetails = $$"""
        {
            "user": {
                "username": "operator1",
                "password": "{{passwordCanary}}"
            },
            "security": {
                "proof": "{{proofCanary}}",
                "token": "{{tokenCanary}}",
                "mek": "{{mekCanary}}",
                "recoveryPhrase": "{{recoveryPhraseCanary}}"
            },
            "metadata": {
                "action": "key-rotation",
                "connection": "Server=db;Password={{passwordCanary}};"
            }
        }
        """;

        var sanitizedDetails = AuditDetailsSanitizer.Sanitize(rawAuditDetails);
        Assert.NotNull(sanitizedDetails);

        foreach (var canary in canaries)
        {
            Assert.DoesNotContain(canary, sanitizedDetails);
        }

        // 3. Test RedactingJsonConsoleFormatter with all canaries
        var formatter = new RedactingJsonConsoleFormatter();
        using var logWriter = new StringWriter();

        foreach (var canary in canaries)
        {
            var logMessage = $"Processing secret canary: Password={canary}; Bearer {canary}; MEK={canary}; RecoveryPhrase={canary}";
            var entry = new LogEntry<string>(LogLevel.Information, "SecurityAudit", new EventId(99, "SecurityEvent"), logMessage, null, (s, _) => s);
            formatter.Write(entry, null, logWriter);
        }

        var logOutput = logWriter.ToString();
        foreach (var canary in canaries)
        {
            Assert.DoesNotContain(canary, logOutput);
        }

        // 4. Test Persistence: write sanitized audit event to PostgreSQL and assert canaries are never stored
        if (_fixture.DockerAvailable)
        {
            var eventId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            await using (var runtimeContext = _fixture.CreateRuntimeDbContext())
            {
                var coordinator = new EfTenantTransactionCoordinator(runtimeContext);
                var store = new EfAuditEventStore(runtimeContext, coordinator);

                // Creating domain audit event with details containing canaries (will be sanitized)
                var auditEvent = AuditEvent.Create(
                    actorType: "User",
                    action: "Security.CanaryTest",
                    outcome: "Success",
                    correlationId: "corr-canary-test",
                    occurredAt: DateTimeOffset.UtcNow,
                    tenantId: tenantId,
                    detailsJson: rawAuditDetails);

                await store.AppendAsync(auditEvent);
                eventId = auditEvent.Id;
            }

            // Inspect DB row via superuser context
            await using (var superContext = _fixture.CreateSuperuserDbContext())
            {
                var storedEvent = await superContext.AuditEvents.FirstOrDefaultAsync(e => e.Id == eventId);
                Assert.NotNull(storedEvent);
                Assert.NotNull(storedEvent.DetailsJson);

                foreach (var canary in canaries)
                {
                    Assert.DoesNotContain(canary, storedEvent.DetailsJson);
                }
            }
        }
    }

    [Fact]
    public void CanaryScan_ExceptionMessagesAndProblemDetails_NoSecrets()
    {
        var passwordCanary = $"{CanaryPrefix}-ex-pwd-918273";
        var tokenCanary = $"{CanaryPrefix}-ex-tok-645312";

        var rawMessage = $"Database connection failed: Server=127.0.0.1;Password={passwordCanary}; Bearer {tokenCanary}";
        var redacted = SensitiveDataRedactor.Redact(rawMessage);

        Assert.DoesNotContain(passwordCanary, redacted);
        Assert.DoesNotContain(tokenCanary, redacted);
        Assert.DoesNotContain(CanaryPrefix, redacted);
        Assert.Contains("Password=***", redacted);
        Assert.Contains("Bearer ***", redacted);
    }

    [Fact(Skip = "S0-015: Ships with First-Run Owner Registration & Bootstrap")]
    public void Audit_Events_ForSetup_ArePresent_AsTheyShip()
    {
        // Reserved placeholder for S0-015:
        // Deployment.Bootstrap.Initiated, Deployment.Bootstrap.Sealed
    }

    [Fact(Skip = "S0-012: Verified in S0-012")]
    public void Audit_Events_ForPrivilege_ArePresent_AsTheyShip()
    {
        // Verified: Authorization.Denied, Tenant.Settings.Updated
    }

    [Fact(Skip = "S0-025: Ships with User Session Management")]
    public void Audit_Events_ForSession_ArePresent_AsTheyShip()
    {
        // Reserved placeholder for S0-025:
        // Identity.Session.Created, Identity.Session.Revoked
    }

    [Fact(Skip = "S0-016: Ships with Key Protection & Encryption")]
    public void Audit_Events_ForKeyEvents_ArePresent_AsTheyShip()
    {
        // Reserved placeholder for S0-016:
        // Key.Mek.Initialized, Key.Mek.Rotated
    }
}
