using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

[GlobalClass]
public partial class TileCombatAIStateMachine : TileCombatController
{

    private TileCombatAIState currentAIState;
    private Dictionary<string, TileCombatAIState> aIStates = new Dictionary<string, TileCombatAIState>();

    [Export] private string initialStateName = "EvaluateState"; // Default initial state name

    // The candidate chosen by EvaluateState, read by MoveState and AttackState.
    public ActionCandidate chosenCandidate;

    public Character lastTarget;

    // Expose protected reachableCells to AI states so EvaluateState can iterate them.
    public System.Collections.Generic.HashSet<Godot.Vector2I> ReachableCells => reachableCells;

    // Expose TryMoveTo publicly so MoveState can trigger movement.
    public bool RequestMoveTo(Vector2I targetCell) => TryMoveTo(targetCell);


    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            if (child is TileCombatAIState state)
            {
                aIStates[state.Name] = state;
                GD.Print($"Found AI state: {state.Name}"); // Debug
                state.unit = GetParent<Character>(); // Assign the unit to the state
                state.stateMachine = this; // Assign the state machine to the state
            }
        }
        // Set the initial state — do NOT call OnEnterState here.
        // The battle hasn't started yet; StartTurn() will fire OnEnterState at the right time.
        if (aIStates.TryGetValue(initialStateName, out TileCombatAIState initialState))
        {
            currentAIState = initialState;
            GD.Print($"Initial AI state set to: {initialStateName}"); // Debug
        }
        else
            GD.PrintErr($"Initial AI state '{initialStateName}' not found.");
    }

    public override void StartTurn()
    {
        base.StartTurn();
        ChangeAIState(initialStateName); // Always restart from Evaluate each turn, never resume from EndTurn
    }

    public override void ProcessTurn(double delta)
    {
        currentAIState?.StateProcess((float)delta);
    }

    public override void EndTurn()
    {
        currentAIState?.OnExitState();
        base.EndTurn();
    }

    public async void ChangeAIState(string newStateName)
    {
        if (currentAIState != null)
        {
            await currentAIState.OnExitState();
        }

        if (aIStates.TryGetValue(newStateName, out TileCombatAIState newState))
        {
            currentAIState = newState;
            await currentAIState.OnEnterState();
        }
        else
        {
            GD.PrintErr($"TileCombatAIStateMachine: State '{newStateName}' not found.");
        }
    }
}