namespace RagPipeline.Persistence.Cli;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RagPipeline.BuildingBlocks.Bootstrap;

public static class RagAdminCli
{
    public static async Task<int> RunAsync(
        string[] args,
        IConfiguration configuration,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        bool? isInteractiveOverride = null,
        IRandomNumberProvider? randomProvider = null,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        var outWriter = stdout ?? Console.Out;
        var errWriter = stderr ?? Console.Error;
        var clock = timeProvider ?? TimeProvider.System;

        if (args.Length == 0)
        {
            PrintUsage(outWriter);
            return RagAdminExitCodes.InvalidArguments;
        }

        var argList = new System.Collections.Generic.List<string>(args);
        if (string.Equals(argList[0], "rag-admin", StringComparison.OrdinalIgnoreCase))
        {
            argList.RemoveAt(0);
        }

        if (argList.Count == 0)
        {
            PrintUsage(outWriter);
            return RagAdminExitCodes.InvalidArguments;
        }

        if (string.Equals(argList[0], "help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argList[0], "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argList[0], "-h", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage(outWriter);
            return RagAdminExitCodes.Success;
        }

        if (string.Equals(argList[0], "bootstrap-proof", StringComparison.OrdinalIgnoreCase))
        {
            argList.RemoveAt(0);
            if (argList.Count == 0 || !string.Equals(argList[0], "generate", StringComparison.OrdinalIgnoreCase))
            {
                errWriter.WriteLine("Error: Unknown subcommand. Supported command: rag-admin bootstrap-proof generate");
                return RagAdminExitCodes.InvalidArguments;
            }

            argList.RemoveAt(0);
            return await HandleGenerateProofAsync(
                argList,
                configuration,
                outWriter,
                errWriter,
                isInteractiveOverride,
                randomProvider,
                clock,
                cancellationToken);
        }

        errWriter.WriteLine($"Error: Unknown command '{args[0]}'.");
        PrintUsage(errWriter);
        return RagAdminExitCodes.InvalidArguments;
    }

    private static async Task<int> HandleGenerateProofAsync(
        System.Collections.Generic.List<string> options,
        IConfiguration configuration,
        TextWriter stdout,
        TextWriter stderr,
        bool? isInteractiveOverride,
        IRandomNumberProvider? randomProvider,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        string? filePath = null;
        var expiryMinutes = 15;

        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt is "--file" or "-f" or "-o" or "--output")
            {
                if (i + 1 >= options.Count || options[i + 1].StartsWith('-'))
                {
                    stderr.WriteLine($"Error: Option '{opt}' requires a file path argument.");
                    return RagAdminExitCodes.InvalidArguments;
                }
                filePath = options[++i];
            }
            else if (opt is "--expiry" or "-e")
            {
                if (i + 1 >= options.Count || !int.TryParse(options[i + 1], out var parsedMinutes) || parsedMinutes <= 0)
                {
                    stderr.WriteLine($"Error: Option '{opt}' requires a positive integer minutes argument.");
                    return RagAdminExitCodes.InvalidArguments;
                }
                expiryMinutes = parsedMinutes;
                i++;
            }
            else if (opt is "--help" or "-h")
            {
                PrintGenerateUsage(stdout);
                return RagAdminExitCodes.Success;
            }
            else
            {
                stderr.WriteLine($"Error: Unrecognized option '{opt}'.");
                return RagAdminExitCodes.InvalidArguments;
            }
        }

        var isInteractive = isInteractiveOverride ?? (!Console.IsOutputRedirected && !Console.IsErrorRedirected);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (!isInteractive)
            {
                stderr.WriteLine("Error: Non-interactive stdout detected. To prevent proof leakage into container logs, specify an output file via --file <path>.");
                return RagAdminExitCodes.SecurityViolation;
            }
        }
        else
        {
            if (File.Exists(filePath))
            {
                stderr.WriteLine($"Error: Output file '{filePath}' already exists. Refusing to overwrite.");
                return RagAdminExitCodes.SecurityViolation;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var fullPath = Path.GetFullPath(filePath);
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    var parentMode = File.GetUnixFileMode(parentDir);
                    if (parentMode.HasFlag(UnixFileMode.OtherRead) && !parentMode.HasFlag(UnixFileMode.UserExecute))
                    {
                        stderr.WriteLine($"Error: Destination directory '{parentDir}' is world-readable/insecure.");
                        return RagAdminExitCodes.SecurityViolation;
                    }
                }
            }
        }

        var connStr = configuration.GetConnectionString("Maintenance")
            ?? configuration["ConnectionStrings:Maintenance"]
            ?? configuration.GetConnectionString("Migrator")
            ?? configuration["ConnectionStrings:Migrator"]
            ?? Environment.GetEnvironmentVariable("RAGPIPELINE_MAINTENANCE_CONNECTION")
            ?? Environment.GetEnvironmentVariable("RAGPIPELINE_MIGRATOR_CONNECTION")
            ?? configuration.GetConnectionString("Database")
            ?? configuration["ConnectionStrings:Database"];

        if (string.IsNullOrWhiteSpace(connStr))
        {
            stderr.WriteLine("Error: No database connection string configured for rag-admin.");
            return RagAdminExitCodes.DatabaseError;
        }

        try
        {
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            // Privilege split check: rag-admin must run under migrator/operator role, never rag_app runtime role
            await using (var roleCmd = conn.CreateCommand())
            {
                roleCmd.CommandText = "SELECT CURRENT_USER;";
                var currentUser = (string?)await roleCmd.ExecuteScalarAsync(cancellationToken);
                if (string.Equals(currentUser, "rag_app", StringComparison.OrdinalIgnoreCase))
                {
                    stderr.WriteLine("Error: rag-admin cannot run under runtime role 'rag_app'. Privileged operator or migrator connection is required.");
                    return RagAdminExitCodes.SecurityViolation;
                }
            }

            // State check: cannot generate proof on SEALED deployment
            await using (var stateCmd = conn.CreateCommand())
            {
                stateCmd.CommandText = "SELECT status FROM deployment_bootstrap_state WHERE id = 1;";
                var currentStatus = (string?)await stateCmd.ExecuteScalarAsync(cancellationToken);
                if (currentStatus == null)
                {
                    stderr.WriteLine("Error: deployment_bootstrap_state row not found. Database migrations may be incomplete.");
                    return RagAdminExitCodes.DatabaseError;
                }

                if (string.Equals(currentStatus, "SEALED", StringComparison.OrdinalIgnoreCase))
                {
                    stderr.WriteLine("Error: Deployment is already SEALED. Bootstrap proof generation is prohibited.");
                    return RagAdminExitCodes.StateConflict;
                }
            }

            // Generate CSPRNG proof token
            var token = BootstrapProofToken.Generate(randomProvider);
            var duration = TimeSpan.FromMinutes(expiryMinutes);
            var expiresAt = clock.GetUtcNow().Add(duration);

            await using (var updateCmd = conn.CreateCommand())
            {
                updateCmd.CommandText = @"
                    UPDATE deployment_bootstrap_state
                    SET bootstrap_proof_hash = @hash,
                        proof_expires_at = @expiresAt,
                        proof_consumed_at = NULL
                    WHERE id = 1 AND status <> 'SEALED';
                ";
                updateCmd.Parameters.AddWithValue("hash", token.ProofHash);
                updateCmd.Parameters.AddWithValue("expiresAt", expiresAt);

                var rows = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                if (rows != 1)
                {
                    stderr.WriteLine("Error: Failed to store bootstrap proof. Deployment may have been sealed concurrently.");
                    return RagAdminExitCodes.StateConflict;
                }
            }

            // Write output
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var fullPath = Path.GetFullPath(filePath);
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                File.WriteAllText(fullPath, token.PlaintextProof, new UTF8Encoding(false));
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                stdout.WriteLine($"Bootstrap proof written to protected file: {fullPath}");
            }
            else
            {
                stdout.WriteLine("================================================================================");
                stdout.WriteLine("BOOTSTRAP PROOF GENERATED (ONE-TIME USE)");
                stdout.WriteLine($"Proof: {token.PlaintextProof}");
                stdout.WriteLine($"Expires At: {expiresAt:u} ({duration.TotalMinutes:F0} minutes)");
                stdout.WriteLine("================================================================================");
                stdout.WriteLine("Save this proof securely. It will NOT be shown again.");
            }

            return RagAdminExitCodes.Success;
        }
        catch (PostgresException ex)
        {
            stderr.WriteLine($"Database error: {ex.MessageText}");
            return RagAdminExitCodes.DatabaseError;
        }
        catch (Exception ex) when (ex is NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
        {
            stderr.WriteLine($"Database connection failed: {ex.Message}");
            return RagAdminExitCodes.DatabaseError;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Unexpected error: {ex.Message}");
            return RagAdminExitCodes.DatabaseError;
        }
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("RagPipeline Administrative CLI (rag-admin)");
        writer.WriteLine("Usage: rag-admin <command> [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  bootstrap-proof generate [--file <path>] [--expiry <minutes>]");
        writer.WriteLine("    Generate a high-entropy, short-lived, one-time bootstrap proof for first-owner setup.");
    }

    private static void PrintGenerateUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: rag-admin bootstrap-proof generate [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -f, --file, -o, --output <path>  Write the proof to a root/operator-only protected file.");
        writer.WriteLine("  -e, --expiry <minutes>           Expiration time in minutes (default: 15).");
        writer.WriteLine("  -h, --help                       Show this help message.");
    }
}
