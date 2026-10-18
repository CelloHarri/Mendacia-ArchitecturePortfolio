// Moves the enemy to the cell chosen by EvaluateState, then transitions to
// AttackState (if a target was chosen) or EndTurnState.
// Name this node exactly "Move" in the scene tree.
using Godot;
using System.Threading.Tasks;

[GlobalClass]
public partial class MoveState : TileCombatAIState
{
    private bool unitStartedMoving { get; set; } = false;



    public async override Task OnEnterState()
    {
        await base.OnEnterState();
        unitStartedMoving = false;
        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(unit.currentCell, unit.characterStats.moveRange, unit.characterStats.faction, useMovementCost: true);
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(stateMachine.chosenCandidate.moveToCell);
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(stateMachine.chosenCandidate.moveToCell);
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            patternCells,
            patternCellIndicatorId,
            patternCellIndicatorAtlas,
            BattleManager.battleManager.combatTileMapLayer.actionIndicatorLayer
        );

        if (stateMachine.RequestMoveTo(stateMachine.chosenCandidate.moveToCell))
        {
            unitStartedMoving = true;
        }
        else
        {
            GD.PrintErr($"MoveState: failed to request move to {stateMachine.chosenCandidate.moveToCell}.");
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }
        // Movement is animated via tween; wait in StateProcess until the tween finishes.
    }

    public override void StateProcess(float delta)
    {
        if (!unitStartedMoving)
            return;

        if (unit.IsTileMoving) return;

        string nextState = stateMachine.chosenCandidate.action switch
        {
            AIAction.Attack => "AttackState",
            AIAction.Heal => "HealState",
            _ => "EndTurnState"
        };
        stateMachine.ChangeAIState(nextState);
    }

    public async override Task OnExitState()
    {
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        await base.OnExitState();
    }
}
