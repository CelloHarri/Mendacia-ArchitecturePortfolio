using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class TileCombatPlayerController : TileCombatController
{
    private PlayerCombatState currentCombatState;
    private Dictionary<string, PlayerCombatState> combatStates = new Dictionary<string, PlayerCombatState>();
    public Player playerUnit => unit as Player;
    [Export] private string initialStateName = "PlayerCombatMoveState";
    public bool RequestToMove(Vector2I targetCell) => TryMoveTo(targetCell);

    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            if (child is PlayerCombatState state)
            {
                combatStates[state.Name] = state;
                GD.Print($"Found Player Combat state: {state.Name}"); // Debug
                state.playerUnit = playerUnit; // Assign the player unit to the state
                state.tileCombatPlayerController = this; // Assign the controller to the state
            }
        }
        // Set the initial state — do NOT call EnterState here.
        // The battle hasn't started yet; StartTurn() will fire EnterState at the right time.
        if (combatStates.TryGetValue(initialStateName, out PlayerCombatState initialState))
        {
            currentCombatState = initialState;
            GD.Print($"Initial Player Combat state set to: {initialStateName}"); // Debug
        }
        else
            GD.PrintErr($"Initial Player Combat state '{initialStateName}' not found.");

    }

    // Driven by BattleManager's state machine each frame while it's this unit's turn.
    // Reads player input (hover/click), highlights reachableCells, and calls TryMoveTo on confirm.
    public override void StartTurn()
    {
        base.StartTurn();
        ChangeCombatState(initialStateName); // Always restart from Idle each turn, never resume from EndTurn
    }
    public override void ProcessTurn(double delta)
    {
        currentCombatState?.ProcessState(delta);
    }
    public override void EndTurn()
    {
        currentCombatState?.ExitState();
        base.EndTurn();
    }

    public void ChangeCombatState(string newStateName)
    {
        if (initialStateName != newStateName)
            currentCombatState?.ExitState();
        if (combatStates.TryGetValue(newStateName, out PlayerCombatState newState))
        {
            currentCombatState = newState;
            currentCombatState.EnterState();
        }
        else
        {
            GD.PrintErr($"TileCombatPlayerController: State '{newStateName}' not found.");
        }
    }
}
