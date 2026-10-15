using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagPipeline.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableTenantRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- 1. tenant_profiles: Enable and FORCE Row Level Security
                ALTER TABLE tenant_profiles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_profiles FORCE ROW LEVEL SECURITY;

                CREATE POLICY tenant_isolation_policy ON tenant_profiles
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

                -- 2. tenant_memberships: Enable and FORCE Row Level Security
                ALTER TABLE tenant_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_memberships FORCE ROW LEVEL SECURITY;

                CREATE POLICY tenant_isolation_policy ON tenant_memberships
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation_policy ON tenant_memberships;
                ALTER TABLE tenant_memberships NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE tenant_memberships DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation_policy ON tenant_profiles;
                ALTER TABLE tenant_profiles NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE tenant_profiles DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
