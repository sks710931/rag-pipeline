using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RagPipeline.IntegrationTests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RagPipeline.BuildingBlocks.Configuration;
using Xunit;

public class SecretConfigurationTests
{
    private static readonly object EnvLock = new();

    [Fact]
    public void Host_WhenSecretFileProvided_TakesPrecedenceOverEnvironmentVariable()
    {
        lock (EnvLock)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ragpipeline-secrets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            const string envConnectionString = "Host=env-db.example.com;Port=5432;Database=env_db;Username=env_user;Password=env_pass";
            const string secretConnectionString = "Host=secret-db.example.com;Port=5432;Database=secret_db;Username=secret_user;Password=secret_pass";

            var secretFile = Path.Combine(tempDir, "ConnectionStrings__Database");
            File.WriteAllText(secretFile, secretConnectionString);

            var originalSecretsDir = Environment.GetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR");
            var originalEnvConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

            try
            {
                Environment.SetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR", tempDir);
                Environment.SetEnvironmentVariable("ConnectionStrings__Database", envConnectionString);

                using var factory = new WebApplicationFactory<Program>();
                using var scope = factory.Services.CreateScope();
                var dbOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

                Assert.NotNull(dbOptions.ConnectionString);
                Assert.Equal(secretConnectionString, dbOptions.ConnectionString);
                Assert.NotEqual(envConnectionString, dbOptions.ConnectionString);
            }
            finally
            {
                Environment.SetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR", originalSecretsDir);
                Environment.SetEnvironmentVariable("ConnectionStrings__Database", originalEnvConnStr);

                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }

    [Fact]
    public void Host_WhenSecretFileValidAndEnvironmentVariableInvalid_StartsSuccessfullyWithSecret()
    {
        lock (EnvLock)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ragpipeline-secrets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            const string invalidEnvConnectionString = "Host=;Database=";
            const string secretConnectionString = "Host=secret-db.example.com;Port=5432;Database=secret_db;Username=secret_user;Password=secret_pass";

            var secretFile = Path.Combine(tempDir, "ConnectionStrings__Database");
            File.WriteAllText(secretFile, secretConnectionString);

            var originalSecretsDir = Environment.GetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR");
            var originalEnvConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

            try
            {
                Environment.SetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR", tempDir);
                Environment.SetEnvironmentVariable("ConnectionStrings__Database", invalidEnvConnectionString);

                using var factory = new WebApplicationFactory<Program>();
                using var client = factory.CreateClient();
                using var scope = factory.Services.CreateScope();
                var dbOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

                Assert.Equal(secretConnectionString, dbOptions.ConnectionString);
            }
            finally
            {
                Environment.SetEnvironmentVariable("RAGPIPELINE_SECRETS_DIR", originalSecretsDir);
                Environment.SetEnvironmentVariable("ConnectionStrings__Database", originalEnvConnStr);

                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }
}
