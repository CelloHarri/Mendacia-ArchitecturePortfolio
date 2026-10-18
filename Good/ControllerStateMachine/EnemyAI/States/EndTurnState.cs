using Godot;
using System.Threading.Tasks;
// Ends the enemy's turn and returns control to BattleManager.
// Name this node exactly "EndTurn" in the scene tree.
[GlobalClass]
public partial class EndTurnState : TileCombatAIState
{
    public async override Task OnEnterState()
    {
        await base.OnEnterState();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        combatUI?.infoUI?.HideCurrentCharacterInfoUI();
        await Sleep(0.2f);
        BattleManager.battleManager.TakeTurn();
    }
}
