using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Shared "template" for any node that drives a Character's Controller during a tactics battle turn.
// BattleManager's turn state machine calls these methods polymorphically without needing to know
// whether the unit is player-controlled (TileCombatPlayerController) or AI-controlled (e.g. a future
// TileCombatEnemyController).
public abstract partial class TileCombatController : ControllerBaseClass
{
    [Signal] public delegate void SendPathEventHandler(Godot.Collections.Array<Vector2I> path);

    [Export] public Character unit;

    List<Vector2I> path = new List<Vector2I>();

    public Vector2I startingPosition;
    public Vector2I currentPosition;
    public int tileSourceId => unit.tileSourceId;
    public Vector2I atlasCoords => unit.atlasCoords;
    public HashSet<Vector2I> reachableCells = new HashSet<Vector2I>();


    public int moveRange => unit.characterStats.moveRange;
    public int remainingMoveRange;

    public float TileMoveTime;

    // Called once by BattleManager when this unit's turn begins.
    // Builds the reachable-cell range from the unit's current position.
    public virtual void StartTurn()
    {
        startingPosition = BattleManager.battleManager.combatTileMapLayer.WorldToCell(unit.GlobalPosition);
        currentPosition = startingPosition;
        unit.currentCell = startingPosition; // Keep currentCell in sync so GetCharactersInRangeOf finds this unit correctly.
        reachableCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(startingPosition, moveRange, unit.characterStats.faction, useMovementCost: true);
        unit.hasMadeAnActionThisTurn = false; // Reset action flag at the start of the turn
        remainingMoveRange = moveRange;
    }

    // Called every frame by BattleManager while this unit's turn is active.
    // Subclasses decide how a destination cell is chosen (player: mouse input, enemy: AI)
    // and should call TryMoveTo once a destination is confirmed.
    public abstract void ProcessTurn(double delta);

    // Erases every indicator tile. Call when the turn ends (or the destination is confirmed) so
    // highlights don't linger into the next unit's turn.
    public virtual void EndTurn() { }

    // Attempts to move the unit to targetCell if it is within the current reachable range.
    // reachableCells is computed once from startingPosition in StartTurn() and never recalculated
    // here, so the range never grows/shrinks as the unit re-moves - the player can freely change
    // their mind and move again (e.g. to undo a bad move) as many times as they like this turn,
    // always measured from where they started.
    // Returns whether the move was accepted.
    protected virtual bool TryMoveTo(Vector2I targetCell)
    {
        if (unit.hasMadeAnActionThisTurn)
        {
            reachableCells.Clear(); // Clear reachable cells to prevent further movement this turn.
            reachableCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(currentPosition, remainingMoveRange, unit.characterStats.faction, useMovementCost: true);
        }
        if (!reachableCells.Contains(targetCell))
            return false;

        if (targetCell == currentPosition)
            return true;

        BattleManager.battleManager.combatTileMapLayer.SetCellOccupied(currentPosition, false, unit.characterStats.faction);

        // Pass null for allowedCells so friendly-occupied cells aren't blocked during pathfinding.
        // The reachableCells check above already ensures the destination is valid.
        path = BattleManager.battleManager.combatTileMapLayer.GetPath(currentPosition, targetCell, unit.characterStats.faction, null);

        if (path.Count <= 1)
        {
            BattleManager.battleManager.combatTileMapLayer.SetCellOccupied(currentPosition, true, unit.characterStats.faction);
            return false;
        }

        BattleManager.battleManager.combatTileMapLayer.SetCellOccupied(targetCell, true, unit.characterStats.faction);

        currentPosition = targetCell;
        unit.currentCell = targetCell;
        unit.hasActedThisTurn = true;

        remainingMoveRange = moveRange - (Mathf.Abs(startingPosition.X - currentPosition.X) + Mathf.Abs(startingPosition.Y - currentPosition.Y));

        EmitSendPathSignal(path);

        return true;
    }

    protected void EmitSendPathSignal(List<Vector2I> path)
    {
        EmitSignal(SignalName.SendPath, new Godot.Collections.Array<Vector2I>(path));
    }

    // === State Machine Hooks ===
    // These hooks are called by BattleManager when the unit's Controller state changes (e.g. from World to TileCombat).
    public override void OnEnterState(ControllerState newState)
    {
        base.OnEnterState(newState);
        // BattleManager drives individual turns (StartTurn/ProcessTurn/EndTurn).
        // This hook is available for subclasses that need entry-side setup.
    }

    public override void OnExitState(ControllerState oldState)
    {
        // No-op at the base level; EndTurn() handles per-turn cleanup.
        // Subclasses can override if they need exit-side teardown.
    }
}
