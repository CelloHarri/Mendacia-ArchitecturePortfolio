using Godot;
using System;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class BattleManager : Node
{
    public static BattleManager battleManager { get; private set; }

    public CombatTileMapLayer combatTileMapLayer; // Set by TileMapLayerController in _Ready() and cleared in _ExitTree().

    [Export] public bool battleActive { get; set; } = false;

    public Queue<Character> turnQueue = new Queue<Character>();
    public System.Collections.Generic.Dictionary<Character, int> allCharacters = new System.Collections.Generic.Dictionary<Character, int>();
    public Character currentCharacter { get; set; } = null;
    public int characterCount = 0;

    public PlayerCombatUI playerCombatUI = null; // Set by PlayerCombatUI in StartBattle() and cleared in EndBattle().

    public int RoundCount = 1;
    public int TurnCount = 1;
    public int totalTurns;

    private Vector2 battleCameraZoom = new Vector2(1f, 1f); // Zoom level for the battle camera

    // Reused each frame by _Process to avoid a LINQ/ToList GC allocation every tick.
    private readonly List<Character> charactersToFreeBuffer = new List<Character>();

    private Control lastFocusTarget = null; // Track the last focused control in the PlayerCombatUI

    public override void _Ready()
    {
        battleManager = this;
    }

    // Call this when a battle actually begins (e.g. from TileMapLayerController._Ready()), not from this
    // singleton's own _Ready(). As an autoload, BattleManager._Ready() fires once at game startup,
    // long before any battle scene/characters exist - so battle setup can't live there.
    public void StartBattle()
    {
        turnQueue.Clear();
        allCharacters.Clear();
        characterCount = 0;
        RoundCount = 1;
        TurnCount = 1;

        IterateTree(GetTree().Root); // Populate allCharacters with all Character nodes in the scene tree.
        CalculateTurnOrder(); // Computed once here; the same order then cycles for the rest of combat via TakeTurn().
        totalTurns = turnQueue.Count;

        // Switch all units from free-roam to tile-combat Controller.
        foreach (Character character in allCharacters.Keys)
        {
            character.ChangeControllerState(ControllerState.TileCombat);
            character.animator.movementType = MovementType.OctalMovement;
            character.ActivateCollision(false); // Disable collision during tile-combat Controller to prevent the Gem from shoving units around
            Vector2I cell = combatTileMapLayer.WorldToCell(character.GlobalPosition);
            character.currentCell = cell; // Initialise so GetCharactersInRangeOf has correct positions from turn 1.
            character.ZIndex = 1000 + cell.Y - cell.X;
            combatTileMapLayer.SetCellOccupied(cell, true, character.characterStats.faction);
            if (character.animator != null)
            {
                character.animator.Offset += character.animator.combatOffset; // Apply combat offset to the animator for tile-combat positioning
                character.animator.shadowSprite.Offset += character.animator.combatOffset; // Apply combat offset to the shadow sprite for tile-combat positioning
            }
            character.OnTileSendPathInstant(new Array<Vector2I> { cell }); // Snap the character to the center of its current tile at the start of the battle
        }

        currentCharacter = turnQueue.Peek();
        if (currentCharacter is Player player)
            playerCombatUI.playerUnit = player; // Set the playerUnit reference in the PlayerCombatUI to the current character
        else
            playerCombatUI.playerUnit = null; // Clear the playerUnit reference if the current character is not a Player
        CameraManager.instance.SnapToCharacter(currentCharacter); // Snap the camera to the current character at the start of the battle
        CameraManager.instance.SetZoom(CameraManager.BattleZoom); // Set the camera zoom for the battle
        currentCharacter.GetController<TileCombatController>()?.StartTurn();
    }

    // Call this when the battle ends (all enemies defeated, fled, etc.).
    public void EndBattle()
    {
        currentCharacter?.GetController<TileCombatController>()?.EndTurn();

        // Return all units to free-roam world Controller.
        // TODO: Consider adding a check to see if the character is still valid before calling ChangeControllerState,
        // in case the character was freed during the battle.
        foreach (Character character in allCharacters.Keys)
        {
            character.ChangeControllerState(ControllerState.World);
            character.ActivateCollision(true); // Re-enable collision after tile-combat Controller ends
            character.animator.movementType = MovementType.QuadMovement;

            if (character.animator != null)
            {
                character.animator.Offset -= character.animator.combatOffset; // Remove the combat offset from the animator, restoring its world-movement position
                character.animator.shadowSprite.Offset -= character.animator.combatOffset; // Remove the combat offset from the shadow sprite, restoring its world-movement position
            }

            if (character.characterStats.characterName == "Sierra")
            {
                _ = CameraManager.instance.MoveToCharacter(character); // Move the camera back to the player character after the battle ends
            }
        }

        playerCombatUI.playerUnit = null; // Clear the playerUnit reference in the PlayerCombatUI
        playerCombatUI?.QueueFree(); // Remove the PlayerCombatUI from the scene tree
        playerCombatUI = null;
        turnQueue.Clear();
        allCharacters.Clear();
        currentCharacter = null;
        characterCount = 0;
        battleActive = false;

    }

    public override void _Process(double delta)
    {
        if (combatTileMapLayer != null)
        {
            battleActive = true;
        }
        else
        {
            // combatTileMapLayer was cleared (scene unloaded). Drop all character references
            // without calling any methods on them - they may already be freed by the scene change.
            if (battleActive)
            {
                currentCharacter = null;
                turnQueue.Clear();
                allCharacters.Clear();
                characterCount = 0;
            }
            battleActive = false;
            return;
        }

        // Clean up characters whose death animation has finished.
        // Collected manually into a reused buffer instead of a LINQ chain to avoid a GC allocation every frame.
        charactersToFreeBuffer.Clear();
        foreach (var c in allCharacters.Keys)
        {
            if (c != null && GodotObject.IsInstanceValid(c) && c.IsDeathAnimationFinished)
                charactersToFreeBuffer.Add(c);
        }

        foreach (Character character in charactersToFreeBuffer)
        {
            RemoveCharacterFromBattle(character);
            character.QueueFree();
        }

    }

    public override void _PhysicsProcess(double delta)
    {
        // Also guard IsInsideTree(): during a scene transition the unit node can still be a valid,
        // non-freed object for a frame while detached from the tree, which has no Viewport - calling
        // into anything that needs one (e.g. GetGlobalMousePosition in the move state) throws.
        if (currentCharacter != null && GodotObject.IsInstanceValid(currentCharacter) && currentCharacter.IsInsideTree())
        {
            currentCharacter.GetController<TileCombatController>()?.ProcessTurn(delta);
        }
        else
        {
            currentCharacter = null;
        }
    }

    private void IterateTree(Node currentNode)
    {
        // Example implementation: recursively iterate through the scene tree
        foreach (Node child in currentNode.GetChildren())
        {
            if (child is Character character)
            {
                allCharacters.Add(character, character.characterStats.initiative);
                characterCount++;
            }
            if (child is PlayerCombatUI ui)
            {
                playerCombatUI = ui; // Store a reference to the PlayerCombatUI for later use
            }
            IterateTree(child);
        }
    }

    // Builds the turn order once for the whole battle. A working copy of each character's initiative is
    // halved every time that character is picked, so characters with higher initiative can be picked again before
    // slower ones catch up. Stops once we've generated exactly `characterCount` turns (the number of
    // turns available at the start). TakeTurn() then re-enqueues each character after their turn so
    // this same fixed order repeats for the rest of combat, without ever recalculating.
    private void CalculateTurnOrder()
    {
        turnQueue.Clear();

        System.Collections.Generic.Dictionary<Character, int> remainingInitiative = new System.Collections.Generic.Dictionary<Character, int>(allCharacters);
        HashSet<Character> seenCharacters = new HashSet<Character>();
        int turnsAvailable = characterCount;

        // Keep picking the current fastest character (allowing repeats for fast characters), but only
        // count a "turn available" as consumed once every distinct character has appeared at least once.
        // This prevents slow characters from being starved out of the cycle entirely.
        while (turnsAvailable > 0)
        {
            Character next = remainingInitiative.OrderByDescending(kv => kv.Value).First().Key;
            turnQueue.Enqueue(next);
            remainingInitiative[next] /= 2;

            if (seenCharacters.Add(next))
            {
                turnsAvailable--;
            }
        }
    }

    // Advances the battle to the next character's turn, immediately re-enqueuing them so the
    // order computed by CalculateTurnOrder() keeps cycling for the rest of combat.
    public Character TakeTurn()
    {
        currentCharacter.GetController<TileCombatController>()?.EndTurn();
        Character next = turnQueue.Dequeue();
        TurnCount++;
        if (TurnCount > totalTurns)
        {
            TurnCount = 1;
            RoundCount++;
        }
        turnQueue.Enqueue(next);
        currentCharacter = turnQueue.Peek();
        _ = CameraManager.instance.MoveToCharacter(currentCharacter); // Move the camera to the current character at the start of their turn
        currentCharacter.GetController<TileCombatController>()?.StartTurn();
        return next;
    }

    // Returns all living characters of the given faction whose current cell is within
    // range of `cell`. When `pattern` is provided the AttackPattern shape (and facing for
    // Custom patterns) is respected; otherwise falls back to Manhattan-distance.
    public Godot.Collections.Dictionary<Character, Vector2I> GetCharactersInRangeOf(Vector2I cell, int minRange, int maxRange, Faction faction, AttackPattern pattern = null, AttackFacing facing = AttackFacing.PosX)
    {
        var result = new Godot.Collections.Dictionary<Character, Vector2I>();
        foreach (Character character in allCharacters.Keys)
        {
            if (!GodotObject.IsInstanceValid(character)) continue;
            if (character.characterStats.faction != faction) continue;
            bool inRange = pattern != null
                ? pattern.IsValidOffset(character.currentCell - cell, minRange, maxRange, facing)
                : (combatTileMapLayer.CellDistance(cell, character.currentCell) >= minRange &&
                   combatTileMapLayer.CellDistance(cell, character.currentCell) <= maxRange);
            if (inRange)
                result.Add(character, character.currentCell);
        }
        return result;
    }

    public Godot.Collections.Dictionary<Objective, Vector2I> GetObjectivesInRangeOf(Vector2I cell, int minRange, int maxRange)
    {
        var result = new Godot.Collections.Dictionary<Objective, Vector2I>();
        foreach (Objective objective in combatTileMapLayer.objectives)
        {
            if (!GodotObject.IsInstanceValid(objective)) continue;
            int distance = combatTileMapLayer.CellDistance(cell, objective.currentCell);
            if (distance >= minRange && distance <= maxRange)
                result.Add(objective, objective.currentCell);
        }
        return result;
    }

    public async void ShowPlayerCombatUIButtons()
    {
        Vector2 ButtonContainerPosition = new Vector2(0, 0); // Default position for the button container

        if (playerCombatUI == null) return;

        playerCombatUI.Show();
        bool wasHidden = !playerCombatUI.buttonContainer.Visible;
        playerCombatUI.buttonContainer.Show();

        Tween tween = playerCombatUI.CreateTween();
        tween.SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenProperty(playerCombatUI.buttonContainer, "position", ButtonContainerPosition, 0.10f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, "finished");
        if (wasHidden && playerCombatUI.focusManager != null && playerCombatUI.actButton != null)
        {
            playerCombatUI.focusManager.Refresh();
            playerCombatUI.focusManager.Focus(playerCombatUI.actButton);
            lastFocusTarget = playerCombatUI.actButton;
        }
    }

    public async void HidePlayerCombatUIButtons()
    {
        Vector2 ButtonContainerHiddenPosition = new Vector2(0, 112); // Position to move the button container when hiding
        Tween tween = playerCombatUI.CreateTween();
        tween.SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenProperty(playerCombatUI.buttonContainer, "position", ButtonContainerHiddenPosition, 0.10f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, "finished");
        playerCombatUI.buttonContainer.Hide();
    }

    public Godot.Collections.Dictionary<Character, Vector2I> GetAllCharacters(Faction faction = Faction.None)
    {
        var result = new Godot.Collections.Dictionary<Character, Vector2I>();
        foreach (Character character in allCharacters.Keys)
        {
            if (!GodotObject.IsInstanceValid(character)) continue;
            if (faction != Faction.None && character.characterStats.faction != faction) continue;
            result.Add(character, character.currentCell);
        }
        return result;
    }

    public Godot.Collections.Dictionary<Character, Vector2I> GetAllPlayers()
    {
        return GetAllCharacters(Faction.Player);
    }

    public Godot.Collections.Dictionary<Character, Vector2I> GetAllEnemies()
    {
        return GetAllCharacters(Faction.Enemy);
    }

    public Godot.Collections.Dictionary<Objective, Vector2I> GetAllObjectives()
    {
        var result = new Godot.Collections.Dictionary<Objective, Vector2I>();
        foreach (Objective objective in combatTileMapLayer.objectives)
        {
            if (!GodotObject.IsInstanceValid(objective)) continue;
            result.Add(objective, objective.currentCell);
        }
        return result;
    }

    public void RemoveCharacterFromBattle(Character character)
    {
        if (character == null || !GodotObject.IsInstanceValid(character)) return;

        bool wasCurrentCharacter = currentCharacter == character;

        // Clear tile occupancy for the removed character.
        if (combatTileMapLayer != null)
        {
            combatTileMapLayer.SetCellOccupied(character.currentCell, false, character.characterStats.faction);
        }

        // Remove the character from the turn queue and allCharacters dictionary
        if (turnQueue.Contains(character))
        {
            var tempQueue = new Queue<Character>(turnQueue.Where(c => c != character));
            turnQueue = tempQueue;
        }

        if (allCharacters.ContainsKey(character))
        {
            allCharacters.Remove(character);
            characterCount--;
        }

        if (wasCurrentCharacter)
        {
            currentCharacter = turnQueue.Count > 0 ? turnQueue.Peek() : null;
            currentCharacter?.GetController<TileCombatController>()?.StartTurn();
        }

        CheckForEndBattle();
    }

    public void CheckForEndBattle()
    {
        if (GetAllPlayers().Count == 0)
            PlayerDefeat();
        else if (GetAllEnemies().Count == 0)
            PlayerVictory();
    }

    public void PlayerDefeat()
    {
        // TODO: Handle player defeat logic, such as showing a defeat screen or triggering game over events.
        EndBattle();
    }

    public void PlayerVictory()
    {
        // TODO: Handle player victory logic, such as showing a victory screen or triggering rewards.
        EndBattle();
    }
}