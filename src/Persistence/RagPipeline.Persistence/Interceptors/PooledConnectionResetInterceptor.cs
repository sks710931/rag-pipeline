namespace RagPipeline.Persistence.Interceptors;

using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class PooledConnectionResetInterceptor : DbConnectionInterceptor
{
    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        ResetTenantState(eventData);
        base.ConnectionClosed(connection, eventData);
    }

    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
    {
        ResetTenantState(eventData);
        return base.ConnectionClosedAsync(connection, eventData);
    }

    private static void ResetTenantState(ConnectionEndEventData eventData)
    {
        if (eventData.Context is RagPipelineDbContext dbContext)
        {
            dbContext.ActiveTenantTransactionId = null;
        }
    }
}
