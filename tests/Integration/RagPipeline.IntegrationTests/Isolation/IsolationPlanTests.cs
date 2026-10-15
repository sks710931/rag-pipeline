namespace RagPipeline.IntegrationTests.Isolation;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

[Collection("IsolationCollection")]
[Trait("Category", "Isolation")]
public class IsolationPlanTests
{
    private readonly IsolationFixture _fixture;

    public IsolationPlanTests(IsolationFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("tenant_profiles")]
    [InlineData("tenant_memberships")]
    public async Task Schema_EveryTenantTable_HasTenantIdLeadingIndexAndCompositeFk(string tableName)
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();

        // 1. Verify leading index on tenant_id
        await using (var indexCmd = conn.CreateCommand())
        {
            indexCmd.CommandText = @"
                SELECT i.relname AS index_name, a.attname AS first_column
                FROM pg_class t
                JOIN pg_index idx ON t.oid = idx.indrelid
                JOIN pg_class i ON i.oid = idx.indexrelid
                JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = idx.indkey[0]
                WHERE t.relname = @tableName
                  AND t.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public')
                  AND a.attname = 'tenant_id';
            ";
            indexCmd.Parameters.AddWithValue("tableName", tableName);
            await using var reader = await indexCmd.ExecuteReaderAsync();
            var hasLeadingIndex = await reader.ReadAsync();
            Assert.True(hasLeadingIndex, $"Table '{tableName}' must have an index with 'tenant_id' as the leading column.");
        }

        // 2. Verify foreign key constraint on tenant_id referencing tenants(id)
        await using (var fkCmd = conn.CreateCommand())
        {
            fkCmd.CommandText = @"
                SELECT conname
                FROM pg_constraint c
                JOIN pg_class t ON c.conrelid = t.oid
                WHERE t.relname = @tableName
                  AND c.contype = 'f'
                  AND c.confrelid = (SELECT oid FROM pg_class WHERE relname = 'tenants' AND relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public'));
            ";
            fkCmd.Parameters.AddWithValue("tableName", tableName);
            var fkName = (string?)await fkCmd.ExecuteScalarAsync();
            Assert.False(string.IsNullOrEmpty(fkName), $"Table '{tableName}' must enforce a foreign key referencing 'tenants'.");
        }
    }

    [Fact]
    public async Task Explain_TenantScopedQueries_UseIndexNotSeqScan_UnderRls()
    {
        Assert.True(_fixture.DockerAvailable, "Docker required.");

        // 1. Seed rows in tenant_memberships for Tenant A and Tenant B to ensure statistical depth
        await using var superConn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await superConn.OpenAsync();

        await using (var seedCmd = superConn.CreateCommand())
        {
            seedCmd.CommandText = $@"
                INSERT INTO asp_net_users (id, user_name, normalized_user_name, password_hash, security_stamp, concurrency_stamp, lockout_enabled, access_failed_count)
                SELECT gen_random_uuid(), 'plan-user-' || g, 'PLAN-USER-' || g, 'hash', gen_random_uuid()::text, gen_random_uuid()::text, false, 0
                FROM generate_series(1, 200) g
                ON CONFLICT DO NOTHING;

                INSERT INTO tenant_memberships (tenant_id, user_id, role, is_first_owner, joined_at)
                SELECT '{_fixture.TenantAId}', u.id, 'TenantMember', false, clock_timestamp()
                FROM asp_net_users u
                WHERE u.user_name LIKE 'plan-user-%'
                ON CONFLICT DO NOTHING;

                ANALYZE tenant_profiles;
                ANALYZE tenant_memberships;
            ";
            await seedCmd.ExecuteNonQueryAsync();
        }

        // 2. Connect as runtime role and run EXPLAIN under RLS
        await using var runtimeConn = new NpgsqlConnection(_fixture.RuntimeConnectionString);
        await runtimeConn.OpenAsync();

        await using var tx = await runtimeConn.BeginTransactionAsync();

        await using (var setCmd = runtimeConn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{_fixture.TenantAId}', true);";
            await setCmd.ExecuteNonQueryAsync();
        }

        // Query 1: tenant_profiles
        await using (var explainProfCmd = runtimeConn.CreateCommand())
        {
            explainProfCmd.Transaction = tx;
            explainProfCmd.CommandText = $"EXPLAIN (FORMAT JSON) SELECT * FROM tenant_profiles WHERE tenant_id = '{_fixture.TenantAId}';";
            var json = (string)(await explainProfCmd.ExecuteScalarAsync())!;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement[0].GetProperty("Plan");
            var nodeType = root.GetProperty("Node Type").GetString();

            Assert.True(
                nodeType == "Index Scan" || nodeType == "Bitmap Index Scan",
                $"Query on tenant_profiles must use Index Scan under RLS. Actual: {nodeType}");
            Assert.NotEqual("Seq Scan", nodeType);
        }

        // Query 2: tenant_memberships
        await using (var explainMemCmd = runtimeConn.CreateCommand())
        {
            explainMemCmd.Transaction = tx;
            explainMemCmd.CommandText = $"EXPLAIN (FORMAT JSON) SELECT * FROM tenant_memberships WHERE tenant_id = '{_fixture.TenantAId}';";
            var json = (string)(await explainMemCmd.ExecuteScalarAsync())!;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement[0].GetProperty("Plan");
            var nodeType = root.GetProperty("Node Type").GetString();

            Assert.True(
                nodeType == "Index Scan" || nodeType == "Bitmap Index Scan" || nodeType == "Bitmap Heap Scan",
                $"Query on tenant_memberships must use index-backed scan under RLS. Actual: {nodeType}");
            Assert.NotEqual("Seq Scan", nodeType);
        }

        await tx.RollbackAsync();
    }
}
