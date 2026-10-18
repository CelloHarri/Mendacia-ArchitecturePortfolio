using Godot;
using System;
using System.Threading.Tasks;

// Moves the enemy after attacking — either falling back from players or pushing toward the last target.
// Skipped entirely when moveAfterAction is false on the profile.
// Name this node exactly "PostAttackMoveState" in the scene tree.
[GlobalClass]
public partial class PostAttackMoveState : TileCombatAIState
{
    private static readonly Random rng = new Random();

    public bool unitStartedMoving { get; private set; } = false;

    public async override Task OnEnterState()
    {
        unitStartedMoving = false;
        await base.OnEnterState();

        var enemy = unit as Enemy;
        var profile = enemy.enemyAIProfile;

        if (!profile.moveAfterAction || stateMachine.remainingMoveRange <= 0)
        {
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        var postMoveCells = BattleManager.battleManager.combatTileMapLayer
            .GetReachableCells(unit.currentCell, stateMachine.remainingMoveRange, unit.characterStats.faction, useMovementCost: true);

        if (postMoveCells.Count <= 1)
        {
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        var allPlayers = BattleManager.battleManager.GetAllPlayers();
        var tileMap = BattleManager.battleManager.combatTileMapLayer;

        Vector2I bestCell = unit.currentCell;
        float bestScore = float.NegativeInfinity;

        foreach (var cell in postMoveCells)
        {
            float cellScore = 0f;

            if (profile.fallBackAfterActionWeight > 0f)
            {
                // Reward distance from every player.
                float totalDist = 0f;
                foreach (var (_, playerCell) in allPlayers)
                    totalDist += tileMap.CellDistance(cell, playerCell);
                cellScore += (totalDist / Mathf.Max(1, allPlayers.Count)) * (profile.fallBackAfterActionWeight / 10f);
            }

            if (profile.pushInAfterActionWeight > 0f && stateMachine.lastTarget != null)
            {
                // Reward proximity to the last attacked target.
                int distToTarget = tileMap.CellDistance(cell, stateMachine.lastTarget.currentCell);
                cellScore -= distToTarget * (profile.pushInAfterActionWeight / 10f);
            }

            float noise = ((float)rng.NextDouble() * 2f - 1f) * profile.aiRandomness;
            if (cellScore + noise > bestScore)
            {
                bestScore = cellScore + noise;
                bestCell = cell;
            }
        }

        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(postMoveCells, unit.characterStats.parentChar.tileSourceId, unit.characterStats.parentChar.atlasCoords);
        await Sleep(0.3f);
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(bestCell);

        if (bestCell == unit.currentCell)
        {
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        if (stateMachine.RequestMoveTo(bestCell))
        {
            unitStartedMoving = true;
        }
        else
        {
            GD.PrintErr($"PostAttackMoveState: failed to request move to {bestCell}.");
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }
    }

    public override void StateProcess(float delta)
    {
        if (!unitStartedMoving)
            return;

        if (!unit.IsTileMoving)
            stateMachine.ChangeAIState("EndTurnState");
    }

    public async override Task OnExitState()
    {
        BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.SetAllBorderVisibility(false);
        await base.OnExitState();
    }
}
