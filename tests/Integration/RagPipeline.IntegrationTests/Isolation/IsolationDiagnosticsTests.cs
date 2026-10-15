namespace RagPipeline.IntegrationTests.Isolation;

using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

[Collection("IsolationCollection")]
[Trait("Category", "Isolation")]
public class IsolationDiagnosticsTests
{
    private readonly IsolationFixture _fixture;

    public IsolationDiagnosticsTests(IsolationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Docker_MustBeAvailable_ForCertificationSuite()
    {
        Assert.True(
            _fixture.DockerAvailable,
            "Docker must be available to execute the tenant isolation certification suite (AC3 / E2E-07). The suite cannot be skipped in CI.");
    }

    [Fact]
    public async Task FailureOutput_IncludesSqlStateTenantIdAndRoleButNoPasswordOrConnectionString()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // Canary on forced failure
        const string passwordCanary = "rag_app_pass";
        const string migratorPasswordCanary = "migrator_pass";
        const string superPasswordCanary = "postgres_password";

        await using var conn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', true);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // Force a tenant RLS check violation
        string diagnosticMessage;
        string? sqlState;
        try
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = $@"
                INSERT INTO tenant_profiles (tenant_id, display_name, settings_json, concurrency_token, created_at, updated_at)
                VALUES ('{_fixture.TenantBId}', 'Diagnostic Canary', '{{}}', gen_random_uuid(), clock_timestamp(), clock_timestamp());
            ";
            await insertCmd.ExecuteNonQueryAsync();
            throw new InvalidOperationException("Insert should have failed RLS.");
        }
        catch (PostgresException pgEx)
        {
            sqlState = pgEx.SqlState;
            diagnosticMessage = $"Tenant={_fixture.TenantAId}; Role=rag_app; SqlState={pgEx.SqlState}; Error={pgEx.MessageText}";
        }

        // 1. Diagnostics MUST include SQLSTATE, TenantId, and Role
        Assert.Equal("44000", sqlState);
        Assert.Contains(_fixture.TenantAId.ToString(), diagnosticMessage);
        Assert.Contains("rag_app", diagnosticMessage);
        Assert.Contains("44000", diagnosticMessage);

        // 2. Diagnostics MUST NOT include passwords or connection string fragments
        Assert.DoesNotContain(passwordCanary, diagnosticMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(migratorPasswordCanary, diagnosticMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(superPasswordCanary, diagnosticMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", diagnosticMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", diagnosticMessage, StringComparison.OrdinalIgnoreCase);

        await tx.RollbackAsync();
    }
}
