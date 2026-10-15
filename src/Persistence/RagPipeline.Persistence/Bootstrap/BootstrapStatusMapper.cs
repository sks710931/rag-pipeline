namespace RagPipeline.Persistence.Bootstrap;

using System;
using System.Net.Sockets;
using Npgsql;
using RagPipeline.BuildingBlocks.Persistence;

public static class BootstrapStatusMapper
{
    public static BootstrapStateResult MapStatus(string? status, bool isSaaS = false)
    {
        if (isSaaS)
        {
            // In SaaS public-host mode, owner setup is suppressed entirely.
            // Reports Sealed so clients never prompt for enrollment or setup.
            return BootstrapStateResult.Sealed();
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return BootstrapStateResult.Unavailable("BOOTSTRAP_STATE_EMPTY");
        }

        var normalized = status.Trim().ToUpperInvariant();
        return normalized switch
        {
            "UNINITIALIZED" => BootstrapStateResult.Uninitialized(),
            "BOOTSTRAPPING" => BootstrapStateResult.Bootstrapping(),
            "SEALED" => BootstrapStateResult.Sealed(),
            _ => BootstrapStateResult.Unavailable($"BOOTSTRAP_STATE_{normalized}")
        };
    }

    public static BootstrapStateResult MapDbException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (ex is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                "42501" => BootstrapStateResult.Unavailable("PERMISSION_DENIED"),
                "42P01" => BootstrapStateResult.Unavailable("DATABASE_NOT_MIGRATED"),
                "40001" => BootstrapStateResult.Unavailable("SERIALIZATION_FAILURE"),
                _ => BootstrapStateResult.Unavailable($"POSTGRES_ERROR_{pgEx.SqlState}")
            };
        }

        if (ex is TimeoutException || ex.InnerException is TimeoutException ||
            (ex is NpgsqlException && ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)))
        {
            return BootstrapStateResult.Unavailable("DATABASE_TIMEOUT");
        }

        if (ex is SocketException || ex.InnerException is SocketException ||
            ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("failed to connect", StringComparison.OrdinalIgnoreCase))
        {
            return BootstrapStateResult.Unavailable("CONNECTION_REFUSED");
        }

        return BootstrapStateResult.Unavailable("DATABASE_UNAVAILABLE");
    }
}
