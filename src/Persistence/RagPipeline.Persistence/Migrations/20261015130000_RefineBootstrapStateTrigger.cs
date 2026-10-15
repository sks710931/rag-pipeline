namespace RagPipeline.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class RefineBootstrapStateTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION trg_prevent_bootstrap_state_mutation()
            RETURNS trigger AS $$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'deployment_bootstrap_state rows cannot be deleted';
                END IF;
                IF TG_OP = 'UPDATE' AND OLD.status = 'SEALED' AND (NEW.status IS DISTINCT FROM 'SEALED' OR NEW.id <> OLD.id) THEN
                    RAISE EXCEPTION 'deployment_bootstrap_state cannot be modified once SEALED';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            GRANT SELECT ON ""__EFMigrationsHistory"" TO rag_app;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            REVOKE SELECT ON ""__EFMigrationsHistory"" FROM rag_app;

            CREATE OR REPLACE FUNCTION trg_prevent_bootstrap_state_mutation()
            RETURNS trigger AS $$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'deployment_bootstrap_state rows cannot be deleted';
                END IF;
                IF TG_OP = 'UPDATE' AND OLD.status = 'SEALED' THEN
                    RAISE EXCEPTION 'deployment_bootstrap_state cannot be modified once SEALED';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
        ");
    }
}
