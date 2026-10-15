namespace RagPipeline.UnitTests.Bootstrap;

using System;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Npgsql;
using RagPipeline.BuildingBlocks.Bootstrap;
using RagPipeline.BuildingBlocks.Persistence;
using RagPipeline.Persistence.Bootstrap;
using Xunit;

public class BootstrapStateMachineTests
{
    [Theory]
    [InlineData(BootstrapState.Uninitialized, BootstrapState.Bootstrapping, true)]
    [InlineData(BootstrapState.Bootstrapping, BootstrapState.Sealed, true)]
    [InlineData(BootstrapState.Uninitialized, BootstrapState.Uninitialized, false)]
    [InlineData(BootstrapState.Uninitialized, BootstrapState.Sealed, false)]
    [InlineData(BootstrapState.Bootstrapping, BootstrapState.Uninitialized, false)]
    [InlineData(BootstrapState.Bootstrapping, BootstrapState.Bootstrapping, false)]
    [InlineData(BootstrapState.Sealed, BootstrapState.Uninitialized, false)]
    [InlineData(BootstrapState.Sealed, BootstrapState.Bootstrapping, false)]
    [InlineData(BootstrapState.Sealed, BootstrapState.Sealed, false)]
    public void BootstrapState_Transitions_AllowedOnly(BootstrapState from, BootstrapState to, bool expectedAllowed)
    {
        var machine = new BootstrapStateMachine(from);

        if (expectedAllowed)
        {
            machine.TransitionTo(to);
            Assert.Equal(to, machine.CurrentState);
        }
        else
        {
            var ex = Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(to));
            Assert.Contains(from.ToString(), ex.Message);
            Assert.Contains(to.ToString(), ex.Message);
            Assert.Equal(from, machine.CurrentState);
        }
    }

    [Theory]
    [InlineData("FOO", "BOOTSTRAP_STATE_FOO")]
    [InlineData("UNKNOWN_STATUS", "BOOTSTRAP_STATE_UNKNOWN_STATUS")]
    [InlineData("", "BOOTSTRAP_STATE_EMPTY")]
    [InlineData(null, "BOOTSTRAP_STATE_EMPTY")]
    public void BootstrapState_UnknownPersistedValue_IsTreatedAsUnavailable(string? persistedValue, string expectedReasonCode)
    {
        var result = BootstrapStatusMapper.MapStatus(persistedValue);

        Assert.True(result.IsUnavailable, $"Persisted status '{persistedValue}' must map to Unavailable.");
        Assert.False(result.IsUninitialized, $"Persisted status '{persistedValue}' must never masquerade as Uninitialized.");
        Assert.False(result.IsSealed, $"Persisted status '{persistedValue}' must not map to Sealed.");
        Assert.Equal(expectedReasonCode, result.ReasonCode);
    }

    [Fact]
    public void BootstrapStatusMapper_DbException_MapsToServiceUnavailable()
    {
        // 1. Timeout
        var timeoutEx = new TimeoutException("Database connection timed out.");
        var timeoutResult = BootstrapStatusMapper.MapDbException(timeoutEx);
        Assert.True(timeoutResult.IsUnavailable);
        Assert.Equal("DATABASE_TIMEOUT", timeoutResult.ReasonCode);

        // 2. Connection refused
        var socketEx = new SocketException((int)SocketError.ConnectionRefused);
        var connRefusedResult = BootstrapStatusMapper.MapDbException(socketEx);
        Assert.True(connRefusedResult.IsUnavailable);
        Assert.Equal("CONNECTION_REFUSED", connRefusedResult.ReasonCode);

        // 3. 42P01: undefined table (not migrated)
        var undefinedTableEx = CreatePostgresException("42P01", "relation does not exist");
        var notMigratedResult = BootstrapStatusMapper.MapDbException(undefinedTableEx);
        Assert.True(notMigratedResult.IsUnavailable);
        Assert.Equal("DATABASE_NOT_MIGRATED", notMigratedResult.ReasonCode);

        // 4. 42501: permission denied
        var permissionDeniedEx = CreatePostgresException("42501", "permission denied");
        var deniedResult = BootstrapStatusMapper.MapDbException(permissionDeniedEx);
        Assert.True(deniedResult.IsUnavailable);
        Assert.Equal("PERMISSION_DENIED", deniedResult.ReasonCode);

        // 5. 40001: serialization failure
        var serializationEx = CreatePostgresException("40001", "could not serialize access");
        var serResult = BootstrapStatusMapper.MapDbException(serializationEx);
        Assert.True(serResult.IsUnavailable);
        Assert.Equal("SERIALIZATION_FAILURE", serResult.ReasonCode);

        // 6. Generic exception
        var genericEx = new InvalidOperationException("Generic failure");
        var genericResult = BootstrapStatusMapper.MapDbException(genericEx);
        Assert.True(genericResult.IsUnavailable);
        Assert.Equal("DATABASE_UNAVAILABLE", genericResult.ReasonCode);
    }

    [Fact]
    public void SaasPublicHost_Policy_SuppressesOwnerSetup()
    {
        // In SaaS mode, even if the database row is UNINITIALIZED or BOOTSTRAPPING,
        // the status mapper must return Sealed so that public owner setup is completely suppressed.
        var uninitializedResult = BootstrapStatusMapper.MapStatus("UNINITIALIZED", isSaaS: true);
        Assert.True(uninitializedResult.IsSealed, "SaaS mode must report Sealed when database is UNINITIALIZED.");
        Assert.False(uninitializedResult.IsUninitialized, "SaaS mode must never report Uninitialized.");

        var bootstrappingResult = BootstrapStatusMapper.MapStatus("BOOTSTRAPPING", isSaaS: true);
        Assert.True(bootstrappingResult.IsSealed, "SaaS mode must report Sealed when database is BOOTSTRAPPING.");
        Assert.False(bootstrappingResult.IsBootstrapping, "SaaS mode must not report Bootstrapping.");

        var sealedResult = BootstrapStatusMapper.MapStatus("SEALED", isSaaS: true);
        Assert.True(sealedResult.IsSealed, "SaaS mode must report Sealed when database is SEALED.");
    }

    private static PostgresException CreatePostgresException(string sqlState, string message)
    {
        var ex = (PostgresException)RuntimeHelpers.GetUninitializedObject(typeof(PostgresException));
        var field = typeof(PostgresException).GetField("<SqlState>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(PostgresException).GetField("_sqlState", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(ex, sqlState);

        var msgField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);
        msgField?.SetValue(ex, message);

        return ex;
    }
}
