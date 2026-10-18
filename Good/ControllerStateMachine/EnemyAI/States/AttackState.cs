using Godot;
using System.Text.Unicode;
using System.Threading.Tasks;
// Triggers the Undertale-style attack sequence on the chosen target's BattleBox,
// then waits for it to finish before moving to EndTurn.
// Name this node exactly "Attack" in the scene tree.
[GlobalClass]
public partial class AttackState : TileCombatAIState
{
    private BattleBox battleBox;

    public async override Task OnEnterState()
    {
        await base.OnEnterState();

        BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            stateMachine.currentPosition,
            unit.characterStats.attackRangePattern.maxRange,
            unit.characterStats.faction,
            minRange: unit.characterStats.attackRangePattern.minRange);


        if (unit is not Enemy enemy)
        {
            GD.PrintErr("AttackState: unit is not an Enemy.");
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        // Find BattleBox in the scene (same approach Enemy.StartAttackSequence uses).
        battleBox = FindBattleBox(GetTree().Root);
        if (battleBox == null)
        {
            GD.PrintErr("AttackState: BattleBox not found.");
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        battleBox.AttackSequenceFinished += OnAttackFinished;

        if (stateMachine.chosenCandidate.target is not Player player)
        {
            GD.PrintErr("AttackState: chosen target is not a Player.");
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        var anchorCell = stateMachine.chosenCandidate.attackAnchorCell;
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(
            anchorCell, unit.characterStats.attackSplashPattern, stateMachine.chosenCandidate.facing);

        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(targetCells, unit.characterStats.parentChar.tileSourceId, unit.characterStats.parentChar.atlasCoords);
        await Sleep(0.3f);
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(anchorCell);
        BattleManager.battleManager.combatTileMapLayer.DrawPatternBorders(anchorCell, unit.characterStats.attackSplashPattern, stateMachine.chosenCandidate.facing);
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(patternCells, patternCellIndicatorId, patternCellIndicatorAtlas, BattleManager.battleManager.combatTileMapLayer.actionIndicatorLayer);
        await Sleep(0.5f);

        stateMachine.lastTarget = player; // Store the last target for scoring purposes in future turns.

        BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.SetAllBorderVisibility(false);

        enemy.StartAttackSequence(player);
    }

    public async override Task OnExitState()
    {
        await base.OnExitState();
        // Safety net: unsubscribe if we exit mid-attack (e.g. battle ends).
        if (battleBox != null)
            battleBox.AttackSequenceFinished -= OnAttackFinished;
    }

    private void OnAttackFinished()
    {
        battleBox.AttackSequenceFinished -= OnAttackFinished;
        stateMachine.ChangeAIState("PostAttackMoveState");
    }

    private BattleBox FindBattleBox(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is BattleBox box) return box;
            BattleBox found = FindBattleBox(child);
            if (found != null) return found;
        }
        return null;
    }
}
