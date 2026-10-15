using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagPipeline.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateRolesAndGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'migrator') THEN
                        CREATE ROLE migrator WITH LOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_app') THEN
                        CREATE ROLE rag_app WITH LOGIN NOBYPASSRLS;
                    ELSE
                        ALTER ROLE rag_app WITH NOBYPASSRLS;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_probe') THEN
                        CREATE ROLE rag_probe WITH LOGIN;
                    END IF;
                END
                $$;

                -- Assign table ownership to migrator role
                ALTER TABLE IF EXISTS asp_net_users OWNER TO migrator;
                ALTER TABLE IF EXISTS asp_net_user_tokens OWNER TO migrator;
                ALTER TABLE IF EXISTS deployment_bootstrap_state OWNER TO migrator;
                ALTER TABLE IF EXISTS deployment_owners OWNER TO migrator;
                ALTER TABLE IF EXISTS tenants OWNER TO migrator;
                ALTER TABLE IF EXISTS tenant_memberships OWNER TO migrator;
                ALTER TABLE IF EXISTS tenant_profiles OWNER TO migrator;
                ALTER TABLE IF EXISTS auth_sessions OWNER TO migrator;
                ALTER TABLE IF EXISTS auth_session_revocations OWNER TO migrator;
                ALTER TABLE IF EXISTS audit_events OWNER TO migrator;

                -- Revoke table creation privileges on public schema from runtime roles
                REVOKE CREATE ON SCHEMA public FROM rag_app;
                REVOKE CREATE ON SCHEMA public FROM rag_probe;

                -- Grant schema usage
                GRANT USAGE ON SCHEMA public TO rag_app, rag_probe;

                -- Runtime role: DML only on public tables
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO rag_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO rag_app;

                -- Probe role: read-only access to deployment_bootstrap_state and __EFMigrationsHistory
                GRANT SELECT ON deployment_bootstrap_state TO rag_probe;
                GRANT SELECT ON ""__EFMigrationsHistory"" TO rag_probe;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM rag_app;
                REVOKE ALL ON SCHEMA public FROM rag_app, rag_probe;
                REVOKE ALL ON deployment_bootstrap_state FROM rag_probe;
                REVOKE ALL ON ""__EFMigrationsHistory"" FROM rag_probe;
            ");
        }
    }
}
