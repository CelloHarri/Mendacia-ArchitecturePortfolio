using Godot;

public enum ControllerState
{
    World,
    TileCombat,
    Cutscene,
    Dialogue,
    Disabled
}

public partial class ControllerBaseClass : Node
{
    public ControllerState CurrentState { get; protected set; } = ControllerState.World;

    // Called by Character.ChangeControllerState() after OnExitState. Sets CurrentState.
    public virtual void OnEnterState(ControllerState newState)
    {
        CurrentState = newState;
    }

    // Called by Character.ChangeControllerState() immediately before the new state's OnEnterState.
    public virtual void OnExitState(ControllerState oldState) { }
}