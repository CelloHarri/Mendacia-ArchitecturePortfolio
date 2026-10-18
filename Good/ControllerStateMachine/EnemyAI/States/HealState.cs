using Godot;
using System.Threading.Tasks;
// Applies healing to the ally chosen by EvaluateState, then transitions to PostAttackMoveState.
// Name this node exactly "HealState" in the scene tree.
[GlobalClass]
public partial class HealState : TileCombatAIState
{
    public async override Task OnEnterState()
    {
        await base.OnEnterState();

        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            stateMachine.currentPosition,
            unit.characterStats.attackRangePattern.maxRange,
            unit.characterStats.faction,
            minRange: unit.characterStats.attackRangePattern.minRange);

        var candidate = stateMachine.chosenCandidate;
        if (candidate?.target == null)
        {
            GD.PrintErr("HealState: no heal target on chosen candidate.");
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        float healAmount = unit.characterStats.healPower;
        candidate.target.characterStats.updateCurrentHealth(healAmount);

        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(
            candidate.target.currentCell, unit.characterStats.healSplashPattern);

        var anchorCell = stateMachine.chosenCandidate.attackAnchorCell;
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(
            anchorCell, unit.characterStats.healSplashPattern, stateMachine.chosenCandidate.facing);

        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(targetCells, unit.characterStats.parentChar.tileSourceId, unit.characterStats.parentChar.atlasCoords);
        await Sleep(0.3f);
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(anchorCell);
        BattleManager.battleManager.combatTileMapLayer.DrawPatternBorders(anchorCell, unit.characterStats.healSplashPattern, stateMachine.chosenCandidate.facing);
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(patternCells, patternCellIndicatorId, patternCellIndicatorAtlas, BattleManager.battleManager.combatTileMapLayer.actionIndicatorLayer);
        await Sleep(0.5f);

        BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.SetAllBorderVisibility(false);

        GD.Print($"{unit.characterStats.characterName} heals {candidate.target.characterStats.characterName} for {healAmount} HP.");

        //Needs a battlebox animation for healing, but for now just wait a moment to simulate the time it would take.
        await Sleep(0.5f);

        stateMachine.ChangeAIState("PostAttackMoveState");
    }
}
