using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

[GlobalClass]
public abstract partial class PlayerCombatState : Node
{
    public Player playerUnit;
    public TileCombatPlayerController tileCombatPlayerController;
    public PlayerCombatUI playerCombatUI;

    protected HashSet<Vector2I> targetCells = new HashSet<Vector2I>();
    protected HashSet<Vector2I> patternCells = new HashSet<Vector2I>();

    protected int patternCellIndicatorId = 0;
    protected Vector2I patternCellIndicatorAtlas = new Vector2I(3, 1);

    protected bool wasLeftMousePressed = false;
    protected Vector2I previousHoveredCell = new Vector2I(int.MinValue, int.MinValue);
    protected AttackFacing currentAttackFacing = AttackFacing.PosX;
    protected bool flipAttackPattern = false;

    public BattleBox battleBox;

    // Constants for joystick input handling
    protected const float JoystickDeadzone = 0.4f;
    protected const float JoystickInitialDelay = 0.35f;
    protected const float JoystickRepeatInterval = 0.15f;
    protected float joystickHoldTimer = 0f;
    protected bool joystickFiredInitial = false;
    protected Vector2I lastJoystickOffset = Vector2I.Zero;

    protected Vector2I moveOffset = Vector2I.Zero;
    protected bool keyboardInputDetected = false;
    protected Vector2I hoveredCell = Vector2I.Zero;
    protected Vector2 previousMousePosition = Vector2.Zero;
    protected Vector2I lastValidHoveredCell = Vector2I.Zero;

    public virtual void EnterState()
    {
        GD.Print("PlayerCombatState: Entering state: " + GetType().Name);
        playerUnit = FindPlayerUnit();
        playerCombatUI = FindPlayerCombatUI(GetTree().Root) ?? BattleManager.battleManager?.playerCombatUI;
        // tileCombatPlayerController assigned to this by the statemachine itself

        if (playerUnit == null)
        {
            GD.PrintErr("PlayerCombatState: unit is not assigned, or is not a Player.");
            return;
        }

        if (BattleManager.battleManager == null || BattleManager.battleManager.combatTileMapLayer == null)
        {
            GD.PrintErr("PlayerCombatState: BattleManager or combatTileMapLayer is null.");
            return;
        }

        hoveredCell = BattleManager.battleManager.combatTileMapLayer.WorldToCell(playerUnit.GlobalPosition);
        previousHoveredCell = hoveredCell;
        lastValidHoveredCell = hoveredCell;
        previousMousePosition = playerUnit.GetViewport().GetMousePosition();

        if (playerCombatUI == null)
        {
            GD.PrintErr("PlayerCombatState: PlayerCombatUI not found in the scene tree or BattleManager.");
        }
        else if (playerCombatUI.infoUI == null)
        {
            GD.PrintErr("PlayerCombatState: PlayerCombatUI.infoUI is not assigned.");
        }

        battleBox = findBattleBox(GetTree().Root);
        if (battleBox == null)
        {
            GD.PrintErr("PlayerCombatState: BattleBox not found in the scene tree.");
            return;
        }
    }

    public abstract void ProcessState(double delta);

    public virtual void ExitState()
    {
        GD.Print("PlayerCombatState: Exiting state: " + GetType().Name);
        playerUnit = null;
    }

    protected Player FindPlayerUnit()
    {
        if (BattleManager.battleManager.currentCharacter is Player player)
        {
            return player;
        }
        return null;
    }

    protected PlayerCombatUI FindPlayerCombatUI(Node node)
    {
        if (node is PlayerCombatUI ui)
            return ui;


        foreach (Node child in node.GetChildren())
        {
            PlayerCombatUI foundUI = FindPlayerCombatUI(child);
            if (foundUI != null)
                return foundUI;
        }
        return null;
    }

    protected virtual void SetupConnections()
    {

    }

    protected BattleBox findBattleBox(Node node)
    {
        foreach (Node child in node.GetChildren())
            if (child is BattleBox battleBox)
                return battleBox;
            else if (child is Node)
            {
                BattleBox found = findBattleBox(child);
                if (found != null)
                    return found;
            }
        return null;
    }

    // Rounds stick angle to nearest 45° sector and maps to isometric cell offset
    protected Vector2I GetJoystickCellOffset(Vector2 stick)
    {
        if (stick.Length() < JoystickDeadzone) return Vector2I.Zero;

        float angle = Mathf.RadToDeg(Mathf.Atan2(stick.Y, stick.X));
        int sector = (int)Mathf.Round(angle / 45f) % 8;
        if (sector < 0) sector += 8;

        if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthEast)
        {
            return sector switch
            {
                0 => new Vector2I(1, 1),  // E
                1 => new Vector2I(0, 1),  // SE
                2 => new Vector2I(-1, 1),  // S
                3 => new Vector2I(-1, 0),  // SW
                4 => new Vector2I(-1, -1),  // W
                5 => new Vector2I(0, -1),  // NW
                6 => new Vector2I(1, -1),  // N
                7 => new Vector2I(1, 0),  // NE
                _ => Vector2I.Zero
            };
        }
        else if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthWest)
        {
            return sector switch
            {
                0 => new Vector2I(0, 1),  // SE
                1 => new Vector2I(-1, 1),  // S
                2 => new Vector2I(-1, 0),  // SW
                3 => new Vector2I(-1, -1),  // W
                4 => new Vector2I(0, -1),  // NW
                5 => new Vector2I(1, -1),  // N
                6 => new Vector2I(1, 0),  // NE
                7 => new Vector2I(1, 1),  // E
                _ => Vector2I.Zero
            };
        }

        return Vector2I.Zero;
    }

    // Returns the joystick cell offset to apply this frame, accounting for initial-delay hold-repeat
    protected Vector2I TickJoystickOffset(Vector2 stick, float delta)
    {
        Vector2I joystickOffset = GetJoystickCellOffset(stick);

        if (joystickOffset == Vector2I.Zero)
        {
            joystickHoldTimer = 0f;
            joystickFiredInitial = false;
            lastJoystickOffset = Vector2I.Zero;
            return Vector2I.Zero;
        }

        if (joystickOffset != lastJoystickOffset)
        {
            joystickHoldTimer = 0f;
            joystickFiredInitial = false;
            lastJoystickOffset = joystickOffset;
        }

        joystickHoldTimer += delta;

        if (!joystickFiredInitial)
        {
            joystickFiredInitial = true;
            joystickHoldTimer = 0f;
            return joystickOffset;
        }

        if (joystickHoldTimer >= JoystickInitialDelay)
        {
            joystickHoldTimer -= JoystickRepeatInterval;
            return joystickOffset;
        }

        return Vector2I.Zero;
    }

    /// <summary>
    /// Gets the WASD Movement for Combat and Stores it in moveOffset
    /// </summary>
    protected Vector2I getWASD()
    {
        if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthEast)
        {
            if (Input.IsActionJustPressed("move_left")) moveOffset = new Vector2I(0, -1);
            else if (Input.IsActionJustPressed("move_right")) moveOffset = new Vector2I(0, 1);
            else if (Input.IsActionJustPressed("move_up")) moveOffset = new Vector2I(1, 0);
            else if (Input.IsActionJustPressed("move_down")) moveOffset = new Vector2I(-1, 0);
        }
        else if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthWest)
        {
            if (Input.IsActionJustPressed("move_left")) moveOffset = new Vector2I(-1, 0);
            else if (Input.IsActionJustPressed("move_right")) moveOffset = new Vector2I(1, 0);
            else if (Input.IsActionJustPressed("move_up")) moveOffset = new Vector2I(0, -1);
            else if (Input.IsActionJustPressed("move_down")) moveOffset = new Vector2I(0, 1);
        }
        return moveOffset;
    }

    /// <summary>
    /// Gets the WASD Movement for Combat and Stores it in moveOffset
    /// Only returns movement if the player has not moved this turn
    /// </summary>
    protected Vector2I getWASD(bool hasMovedThisTurn)
    {
        if (hasMovedThisTurn) return Vector2I.Zero;

        if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthEast)
        {
            if (Input.IsActionJustPressed("move_left")) moveOffset = new Vector2I(0, -1);
            else if (Input.IsActionJustPressed("move_right")) moveOffset = new Vector2I(0, 1);
            else if (Input.IsActionJustPressed("move_up")) moveOffset = new Vector2I(1, 0);
            else if (Input.IsActionJustPressed("move_down")) moveOffset = new Vector2I(-1, 0);
        }
        else if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthWest)
        {
            if (Input.IsActionJustPressed("move_left")) moveOffset = new Vector2I(-1, 0);
            else if (Input.IsActionJustPressed("move_right")) moveOffset = new Vector2I(1, 0);
            else if (Input.IsActionJustPressed("move_up")) moveOffset = new Vector2I(0, -1);
            else if (Input.IsActionJustPressed("move_down")) moveOffset = new Vector2I(0, 1);
        }
        return moveOffset;
    }

    protected Vector2I getKeyboardAndMouseHoveredCell(AttackPattern attackPattern)
    {
        if (Input.IsActionPressed("move_left") || Input.IsActionPressed("move_right") || Input.IsActionPressed("move_up") || Input.IsActionPressed("move_down"))
            keyboardInputDetected = true;

        Vector2 mousePosition = playerUnit.GetViewport().GetMousePosition();

        if (targetCells.Contains(BattleManager.battleManager.combatTileMapLayer.WorldToCell(playerUnit.GetGlobalMousePosition())) && mousePosition != previousMousePosition)
            keyboardInputDetected = false;

        if (!keyboardInputDetected)
        {
            previousMousePosition = mousePosition;
            Vector2I newHoveredCell = BattleManager.battleManager.combatTileMapLayer.WorldToCell(playerUnit.GetGlobalMousePosition());

            if (targetCells.Contains(newHoveredCell))
            {
                hoveredCell = newHoveredCell;
                lastValidHoveredCell = newHoveredCell; // only update when valid
            }
            else
            {
                hoveredCell = lastValidHoveredCell; // stay on last valid cell
            }
        }
        else
        {
            moveOffset = Vector2I.Zero;
            Vector2I wasdOffset = getWASD();
            if (wasdOffset != Vector2I.Zero)
            {
                Vector2I newHoveredCell = hoveredCell + wasdOffset;
                if (targetCells.Contains(newHoveredCell))
                    hoveredCell = newHoveredCell;
                else
                {
                    Vector2I found = FindNextAvailableCell(newHoveredCell);
                    if (targetCells.Contains(found))
                        hoveredCell = found;
                }
            }
        }
        return hoveredCell;
    }

    protected async Task UpdateHoveredCharacterInfo(Vector2I cell, bool playTween = true)
    {
        if (playerCombatUI?.infoUI == null) return;

        Character found = null;
        if (targetCells.Contains(cell))
        {
            foreach (var character in BattleManager.battleManager.allCharacters.Keys)
            {
                if (character.currentCell == cell)
                {
                    found = character;
                    break;
                }
            }
        }

        if (found != null)
        {
            if (playerCombatUI.infoUI.hoveredCharacter != null && playerCombatUI.infoUI.hoveredCharacter != found)
            {
                playerCombatUI.infoUI.HideHoveredCharacterInfoUI();
                if (playTween)
                    await Sleep(playerCombatUI.infoUI.tweenDuration);
            }
            playerCombatUI.infoUI.ShowHoveredCharacterInfoUI(found);
        }
        else
        {
            playerCombatUI.infoUI.HideHoveredCharacterInfoUI();
            if (playTween)
                await Sleep(playerCombatUI.infoUI.tweenDuration);
        }
    }

    private void DrawDebugCircleAtCell(Vector2I cell, Color color)
    {
        var tilemap = BattleManager.battleManager.combatTileMapLayer;
        var line = new Godot.Line2D();
        line.Width = 1.5f;
        line.DefaultColor = color;
        const int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            float angle = i * Mathf.Tau / steps;
            line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5f);
        }
        line.GlobalPosition = tilemap.CellToWorld(cell);
        tilemap.AddChild(line);
        GetTree().CreateTimer(5.0).Timeout += line.QueueFree;
    }

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

    public int FindQuadrant(Vector2I NewHoveredCell)
    {
        Vector2 cellDifferenceFromPlayer = NewHoveredCell - playerUnit.currentCell;

        if (cellDifferenceFromPlayer.X > 0 && cellDifferenceFromPlayer.Y < 0)
            return 1; // Top-right quadrant
        else if (cellDifferenceFromPlayer.X > 0 && cellDifferenceFromPlayer.Y > 0)
            return 2; // Bottom-right quadrant
        else if (cellDifferenceFromPlayer.X < 0 && cellDifferenceFromPlayer.Y > 0)
            return 3; // Bottom-left quadrant
        else if (cellDifferenceFromPlayer.X < 0 && cellDifferenceFromPlayer.Y < 0)
            return 4; // Top-left quadrant
        else if (cellDifferenceFromPlayer.X == 0 && cellDifferenceFromPlayer.Y < 0)
            return 5; // Directly above
        else if (cellDifferenceFromPlayer.X == 0 && cellDifferenceFromPlayer.Y > 0)
            return 6; // Directly below
        else if (cellDifferenceFromPlayer.X < 0 && cellDifferenceFromPlayer.Y == 0)
            return 7; // Directly left
        else if (cellDifferenceFromPlayer.X > 0 && cellDifferenceFromPlayer.Y == 0)
            return 8; // Directly right
        else
            return 0; // Origin or undefined quadrant
    }

    public Vector2I GetNextHoveredCell(Vector2I NewHoveredCell, int quadrant)
    {
        if (quadrant == 0)
            return NewHoveredCell;
        // DrawDebugCircleAtCell(NewHoveredCell, Colors.Yellow);
        Vector2 cellDifferenceFromPlayer = NewHoveredCell - playerUnit.currentCell;

        if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthWest) // Works perfectly
        {
            if (moveOffset == new Vector2I(1, 0))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 5 || quadrant == 7)
                {
                    moveOffset = new Vector2I(0, 1);
                    NewHoveredCell += new Vector2I(0, 1);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 6)
                {
                    moveOffset = new Vector2I(0, -1);
                    NewHoveredCell += new Vector2I(0, -1);
                }
                else if (quadrant == 7 || quadrant == 8)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
            else if (moveOffset == new Vector2I(0, 1))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 7)
                {
                    moveOffset = new Vector2I(1, 0);
                    NewHoveredCell += new Vector2I(1, 0);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 8 || quadrant == 5)
                {
                    moveOffset = new Vector2I(-1, 0);
                    NewHoveredCell += new Vector2I(-1, 0);
                }
                else if (quadrant == 5 || quadrant == 6)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
            else if (moveOffset == new Vector2I(-1, 0))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 6 || quadrant == 8)
                {
                    moveOffset = new Vector2I(0, -1);
                    NewHoveredCell += new Vector2I(0, -1);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 5)
                {
                    moveOffset = new Vector2I(0, 1);
                    NewHoveredCell += new Vector2I(0, 1);
                }
                else if (quadrant == 7 || quadrant == 8)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
            else if (moveOffset == new Vector2I(0, -1))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 8)
                {
                    moveOffset = new Vector2I(-1, 0);
                    NewHoveredCell += new Vector2I(-1, 0);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 7 || quadrant == 6)
                {
                    moveOffset = new Vector2I(1, 0);
                    NewHoveredCell += new Vector2I(1, 0);
                }
                else if (quadrant == 5 || quadrant == 6)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
        }
        else if (GameManager.currentHoverDirectionOnUpInput == HoverDirectionOnUpInput.NorthEast)
        {
            if (moveOffset == new Vector2I(1, 0))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 5)
                {
                    moveOffset = new Vector2I(0, 1);
                    NewHoveredCell += new Vector2I(0, 1);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 6 || quadrant == 7)
                {
                    moveOffset = new Vector2I(0, -1);
                    NewHoveredCell += new Vector2I(0, -1);
                }
                else if (quadrant == 7 || quadrant == 8)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
            else if (moveOffset == new Vector2I(0, 1))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 7 || quadrant == 5)
                {
                    moveOffset = new Vector2I(1, 0);
                    NewHoveredCell += new Vector2I(1, 0);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 8)
                {
                    moveOffset = new Vector2I(-1, 0);
                    NewHoveredCell += new Vector2I(-1, 0);
                }
                else if (quadrant == 5 || quadrant == 6)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
            else if (moveOffset == new Vector2I(-1, 0))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 6)
                {
                    moveOffset = new Vector2I(0, -1);
                    NewHoveredCell += new Vector2I(0, -1);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 5 || quadrant == 8)
                {
                    moveOffset = new Vector2I(0, 1);
                    NewHoveredCell += new Vector2I(0, 1);
                }
                else if (quadrant == 7 || quadrant == 8)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
            else if (moveOffset == new Vector2I(0, -1))
            {
                if (quadrant == 1 || quadrant == 3 || quadrant == 8 || quadrant == 6)
                {
                    moveOffset = new Vector2I(-1, 0);
                    NewHoveredCell += new Vector2I(-1, 0);
                }
                else if (quadrant == 2 || quadrant == 4 || quadrant == 7)
                {
                    moveOffset = new Vector2I(1, 0);
                    NewHoveredCell += new Vector2I(1, 0);
                }
                else if (quadrant == 5 || quadrant == 6)
                {
                    moveOffset = new Vector2I(0, 0);
                    NewHoveredCell += new Vector2I(0, 0); // No change for directly left or right
                }
            }
        }


        return NewHoveredCell;
    }
    public Vector2I FindNextAvailableCell(Vector2I NewHoveredCell)
    {
        Vector2I originalHoveredCell = NewHoveredCell;
        Vector2I originalMoveOffset = moveOffset;

        HashSet<Vector2I> availableSearchCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            playerUnit.currentCell,
            playerUnit.characterStats.attackRangePattern.maxRange + 2);

        while (!targetCells.Contains(NewHoveredCell) && availableSearchCells.Contains(NewHoveredCell))
        {
            originalHoveredCell = NewHoveredCell - moveOffset; // update originalHoveredCell for the current iteration
            Vector2I prev = NewHoveredCell;
            NewHoveredCell = GetNextHoveredCell(NewHoveredCell, FindQuadrant(originalHoveredCell));
            if (NewHoveredCell == prev)
            {
                NewHoveredCell = originalHoveredCell;
                break;
            }
            if (!availableSearchCells.Contains(NewHoveredCell))
            {
                NewHoveredCell = originalHoveredCell;
                break;
            }
            else if (NewHoveredCell == originalHoveredCell)
            {
                break;
            }
        }
        moveOffset = originalMoveOffset; // Reset moveOffset to its original value after finding the next available cell
        return NewHoveredCell;
    }
}