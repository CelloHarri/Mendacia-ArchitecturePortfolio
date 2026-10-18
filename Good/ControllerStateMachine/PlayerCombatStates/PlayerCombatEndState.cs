using Godot;

[GlobalClass]
public partial class PlayerCombatEndState : PlayerCombatState
{
    public async override void EnterState()
    {
        base.EnterState();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
        BattleManager.battleManager.combatTileMapLayer.DeleteBorders();
        BattleManager.battleManager.combatTileMapLayer.SetAllBorderVisibility(false);
        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
        playerCombatUI?.infoUI?.HideCurrentCharacterInfoUI();
        await Sleep(0.2f); // Optional: Add a slight delay before ending the turn for visual effect

        BattleManager.battleManager.TakeTurn(); // End the player's turn when entering the end state
    }

    public override void ProcessState(double delta)
    {
        // Logic for processing the end state can be added here
    }

    public override void ExitState()
    {
        base.ExitState();
        // Additional logic for exiting the end state can be added here
    }
}