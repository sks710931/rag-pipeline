namespace RagPipeline.BuildingBlocks.Bootstrap;

using System;

public enum BootstrapState
{
    Uninitialized,
    Bootstrapping,
    Sealed
}

public static class BootstrapStateTransitions
{
    public static bool IsValidTransition(BootstrapState current, BootstrapState next)
    {
        return (current, next) switch
        {
            (BootstrapState.Uninitialized, BootstrapState.Bootstrapping) => true,
            (BootstrapState.Bootstrapping, BootstrapState.Sealed) => true,
            _ => false
        };
    }

    public static void ValidateTransition(BootstrapState current, BootstrapState next)
    {
        if (!IsValidTransition(current, next))
        {
            throw new InvalidOperationException($"Invalid bootstrap state transition from '{current}' to '{next}'.");
        }
    }
}

public sealed class BootstrapStateMachine
{
    public BootstrapState CurrentState { get; private set; }

    public BootstrapStateMachine(BootstrapState initialState)
    {
        CurrentState = initialState;
    }

    public void TransitionTo(BootstrapState nextState)
    {
        BootstrapStateTransitions.ValidateTransition(CurrentState, nextState);
        CurrentState = nextState;
    }
}
