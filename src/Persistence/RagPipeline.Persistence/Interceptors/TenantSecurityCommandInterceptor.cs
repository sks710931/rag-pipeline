namespace RagPipeline.Persistence.Interceptors;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class TenantSecurityCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ValidateTenantAccess(command, eventData.Context);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantAccess(command, eventData.Context);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateTenantAccess(command, eventData.Context);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantAccess(command, eventData.Context);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        ValidateTenantAccess(command, eventData.Context);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantAccess(command, eventData.Context);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public static void ValidateTenantAccess(DbCommand command, DbContext? context)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.CommandText))
        {
            return;
        }

        var text = command.CommandText;

        // Check if command references tenant-owned tables
        var referencesTenantTables = text.Contains("tenant_profiles", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tenant_memberships", StringComparison.OrdinalIgnoreCase);

        if (!referencesTenantTables)
        {
            return;
        }

        // Allow DDL, migration queries, and schema metadata queries
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("ALTER", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("DROP", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("GRANT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("REVOKE", StringComparison.OrdinalIgnoreCase)
            || text.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)
            || text.Contains("information_schema", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pg_", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context is RagPipelineDbContext ragDbContext)
        {
            if (!ragDbContext.ActiveTenantTransactionId.HasValue)
            {
                throw new InvalidOperationException("Executing tenant SQL outside a tenant transaction is prohibited.");
            }
        }
    }
}
