namespace RagPipeline.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class EnableAuditAppendOnlyAndRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            -- Drop legacy unconditional trigger from InitialS0Schema
            DROP TRIGGER IF EXISTS trg_audit_events_no_update_delete ON audit_events;
            DROP FUNCTION IF EXISTS trg_prevent_audit_events_mutation();

            -- 1. Append-only trigger for audit_events (rejects direct UPDATE and DELETE unless retention purge flag is set)
            CREATE OR REPLACE FUNCTION reject_audit_mutation()
            RETURNS trigger AS $$
            BEGIN
                IF current_setting('app.allow_audit_retention_purge', true) = 'true' THEN
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                END IF;
                RAISE EXCEPTION 'audit_events is append-only: updates and deletes are prohibited';
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS trg_audit_events_immutable ON audit_events;
            CREATE TRIGGER trg_audit_events_immutable
                BEFORE UPDATE OR DELETE ON audit_events
                FOR EACH ROW
                EXECUTE FUNCTION reject_audit_mutation();

            -- 2. Revoke direct UPDATE, DELETE, TRUNCATE on audit_events from runtime role, and UPDATE, TRUNCATE from migrator
            REVOKE UPDATE, DELETE, TRUNCATE ON audit_events FROM rag_app;
            REVOKE UPDATE, TRUNCATE ON audit_events FROM migrator;
            GRANT DELETE ON audit_events TO migrator;

            -- 3. Row Level Security on audit_events
            ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
            ALTER TABLE audit_events FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS audit_events_tenant_isolation_policy ON audit_events;
            CREATE POLICY audit_events_tenant_isolation_policy ON audit_events
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (
                    (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    OR (tenant_id IS NULL AND NULLIF(current_setting('app.current_tenant_id', true), '') IS NULL)
                );

            -- 4. Authorized retention purge function (SECURITY DEFINER allows controlled deletion past cutoff)
            CREATE OR REPLACE FUNCTION purge_expired_audit_events(p_cutoff timestamptz)
            RETURNS integer AS $$
            DECLARE
                v_deleted integer;
                v_min_retention_floor timestamptz := clock_timestamp() - interval '30 days';
            BEGIN
                IF p_cutoff > v_min_retention_floor THEN
                    RAISE EXCEPTION 'Retention cutoff cannot be newer than 30 days prior to current time';
                END IF;

                PERFORM set_config('app.allow_audit_retention_purge', 'true', true);
                DELETE FROM public.audit_events WHERE occurred_at < p_cutoff;
                GET DIAGNOSTICS v_deleted = ROW_COUNT;
                RETURN v_deleted;
            END;
            $$ LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public;

            ALTER FUNCTION purge_expired_audit_events(timestamptz) OWNER TO migrator;
            REVOKE ALL ON FUNCTION purge_expired_audit_events(timestamptz) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION purge_expired_audit_events(timestamptz) TO migrator;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP FUNCTION IF EXISTS purge_expired_audit_events(timestamptz);

            DROP POLICY IF EXISTS audit_events_tenant_isolation_policy ON audit_events;
            ALTER TABLE audit_events NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE audit_events DISABLE ROW LEVEL SECURITY;

            DROP TRIGGER IF EXISTS trg_audit_events_immutable ON audit_events;
            DROP FUNCTION IF EXISTS reject_audit_mutation();

            -- Restore legacy trigger from InitialS0Schema
            CREATE OR REPLACE FUNCTION trg_prevent_audit_events_mutation()
            RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'audit_events is append-only: updates and deletes are prohibited';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER trg_audit_events_no_update_delete
            BEFORE UPDATE OR DELETE ON audit_events
            FOR EACH ROW EXECUTE FUNCTION trg_prevent_audit_events_mutation();

            GRANT UPDATE, DELETE ON audit_events TO rag_app;
            GRANT UPDATE, DELETE, TRUNCATE ON audit_events TO migrator;
        ");
    }
}
