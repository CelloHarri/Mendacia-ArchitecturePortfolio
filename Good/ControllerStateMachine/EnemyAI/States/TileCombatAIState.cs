using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[GlobalClass]
public abstract partial class TileCombatAIState : Node
{
    public Character unit;
    protected PlayerCombatUI combatUI => BattleManager.battleManager?.playerCombatUI;
    public TileCombatAIStateMachine stateMachine;
    protected AttackFacing currentFacing = AttackFacing.PosX; // Default facing for custom attack patterns; can be changed by EvaluateState if needed.

    protected int patternCellIndicatorId = 0;
    protected Vector2I patternCellIndicatorAtlas = new Vector2I(3, 1);

    protected HashSet<Vector2I> targetCells = new HashSet<Vector2I>();
    protected HashSet<Vector2I> patternCells = new HashSet<Vector2I>();

    public virtual Task OnEnterState()
    {
        GD.Print($"Entering state: {Name}");
        return Task.CompletedTask;
    }
    public virtual Task OnExitState()
    {
        GD.Print($"Exiting state: {Name}");
        return Task.CompletedTask;
    }
    public virtual void StateProcess(float delta) { }

    protected async Task Sleep(float seconds)
    {
        // Use a timer to wait for the specified duration before continuing.
        var timer = new Timer();
        timer.WaitTime = seconds;
        timer.OneShot = true;
        AddChild(timer);
        timer.Start();
        await ToSignal(timer, "timeout");
        timer.QueueFree(); // Clean up the timer after it finishes.
    }

    //Different Ways to access the unit and state machine from AI states:
    // unit                                   // the Character (cast to Enemy if needed)
    // unit.characterStats                   // stats, faction, aiRandomness, attackRange
    // unit.currentCell                      // grid position
    // unit.IsTileMoving                     // true while movement tween is running
    // stateMachine.chosenCandidate          // ActionCandidate picked by EvaluateState
    // stateMachine.ReachableCells           // HashSet<Vector2I> of cells in move range
    // stateMachine.RequestMoveTo(cell)      // triggers movement, returns false if unreachable
    // stateMachine.ChangeAIState("Name")    // transition to another state
    // BattleManager.battleManager           // turn queue, all characters, TakeTurn()
}
