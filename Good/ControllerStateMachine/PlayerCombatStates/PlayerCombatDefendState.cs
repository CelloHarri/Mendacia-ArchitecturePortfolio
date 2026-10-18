using Godot;

[GlobalClass]
public partial class PlayerCombatDefendState : PlayerCombatState
{
    public override void EnterState()
    {
        base.EnterState();
        playerUnit.characterStats.isDefending = true;
        tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
    }

    public override void ProcessState(double delta)
    {
        // Logic for processing the defend state can be added here
    }

    public override void ExitState()
    {
        base.ExitState();
    }
}