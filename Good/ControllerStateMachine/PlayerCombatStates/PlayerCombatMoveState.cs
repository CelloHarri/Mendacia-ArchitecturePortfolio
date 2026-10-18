using System.Threading.Tasks;
using Godot;

[GlobalClass]
public partial class PlayerCombatMoveState : PlayerCombatState
{
    private bool hasMovedThisTurn = false;
    private bool connectionsSetup = false;

    AttackPattern movementBracketPattern = new AttackPattern { shape = AttackShape.Diamond };

    public override void EnterState()
    {
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        base.EnterState();
        SetupConnections();

        BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
        BattleManager.battleManager.combatTileMapLayer.DeleteBorders();
        BattleManager.battleManager.combatTileMapLayer.SetAllBorderVisibility(false);

        playerUnit.characterStats.isDefending = false;
        hasMovedThisTurn = false;
        keyboardInputDetected = false;

        if (playerCombatUI != null)
        {
            if (playerCombatUI.buttonContainer != null)
                playerCombatUI.buttonContainer.Visible = true;
            playerCombatUI.infoUI?.ShowCurrentCharacterInfoUI(playerUnit);
            if (!playerUnit.hasMadeAnActionThisTurn)
                playerCombatUI?.infoUI?.UpdatePhaseLabel(CombatPhase.Evaluation);
            else
                playerCombatUI?.infoUI?.UpdatePhaseLabel(CombatPhase.PostAction);
        }
        else
        {
            GD.PrintErr("PlayerCombatMoveState: playerCombatUI is null in EnterState.");
        }

        BattleManager.battleManager.playerCombatUI?.buttonContainer?.Hide();

    }

    public override void ProcessState(double delta)
    {
        getInput((float)delta);
    }

    public override void ExitState()
    {
        if (playerCombatUI != null)
        {
            playerCombatUI.attackButton.ButtonUp -= OnAttackButtonUp;
            playerCombatUI.defendButton.ButtonUp -= OnDefendButtonUp;
            playerCombatUI.actButton.ButtonUp -= OnActButtonUp;
            playerCombatUI.itemsButton.ButtonUp -= OnItemButtonUp;
            playerCombatUI.magicButton.ButtonUp -= OnMagicButtonUp;
        }
        connectionsSetup = false;

        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        base.ExitState();
    }

    public void getInput(float delta)
    {
        InputType inputType = GameManager.currentInputType;

        if (playerUnit == null)
        {
            GD.PrintErr("PlayerCombatMoveState: playerUnit is not assigned, or is not a Player.");
            return;
        }

        if (BattleManager.battleManager == null || BattleManager.battleManager.combatTileMapLayer == null)
        {
            GD.PrintErr("PlayerCombatMoveState: BattleManager or combatTileMapLayer is null.");
            return;
        }

        if (tileCombatPlayerController == null)
        {
            GD.PrintErr("PlayerCombatMoveState: tileCombatPlayerController is null.");
            return;
        }

        if (playerUnit.IsTileMoving) return;

        if (inputType == InputType.KeyboardAndMouse)
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
                Vector2I wasdOffset = getWASD(hasMovedThisTurn);
                if (wasdOffset != Vector2I.Zero)
                {
                    Vector2I newHoveredCell = hoveredCell + wasdOffset;
                    if (tileCombatPlayerController.reachableCells.Contains(newHoveredCell))
                        hoveredCell = newHoveredCell;
                }
            }


            bool isLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            if (((isLeftMousePressed && !wasLeftMousePressed) || Input.IsActionJustPressed("combat_confirm")) &&
                (BattleManager.battleManager.playerCombatUI?.buttonContainer == null || BattleManager.battleManager.playerCombatUI.buttonContainer.Visible == false))
            {
                ConfirmMove();
            }
            wasLeftMousePressed = isLeftMousePressed;
        }
        else if (inputType == InputType.Controller)
        {

            moveOffset = Vector2I.Zero;

            getWASD(hasMovedThisTurn);

            if (hasMovedThisTurn == false)
            {
                Vector2 stick = Input.GetVector("stick_left", "stick_right", "stick_up", "stick_down", JoystickDeadzone);
                Vector2I joystickOffset = TickJoystickOffset(stick, delta);
                if (joystickOffset != Vector2I.Zero && moveOffset == Vector2I.Zero)
                    moveOffset = joystickOffset;
            }

            // Apply moveOffset (from DPad or stick) to the hovered cell
            if (moveOffset != Vector2I.Zero)
            {
                Vector2I newHoveredCell = hoveredCell + moveOffset;
                if (tileCombatPlayerController.reachableCells.Contains(newHoveredCell))
                    hoveredCell = newHoveredCell;
            }

            if (Input.IsActionJustPressed("combat_confirm"))
            {
                ConfirmMove();
            }
        }
        else if (inputType == InputType.KeyboardOnly)
        {

            moveOffset = Vector2I.Zero;

            getWASD(hasMovedThisTurn);

            if (moveOffset != Vector2I.Zero)
            {
                Vector2I newHoveredCell = hoveredCell + moveOffset;
                if (tileCombatPlayerController.reachableCells.Contains(newHoveredCell))
                    hoveredCell = newHoveredCell;
            }
            if (Input.IsActionJustPressed("combat_confirm"))
            {
                ConfirmMove();
            }
        }

        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(hoveredCell);

        // playerUnit may be null if ConfirmMove() triggered a state change and ExitState() ran
        if (playerUnit == null) return;

        if (playerUnit.IsTileMoving == false && hasMovedThisTurn == true)
        {
            BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
            if (shouldEndTurnAfterMove)
            {
                shouldEndTurnAfterMove = false;
                BattleManager.battleManager.HidePlayerCombatUIButtons();
                tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
                return;
            }
            BattleManager.battleManager.ShowPlayerCombatUIButtons();
        }

        if (Input.IsActionJustPressed("combat_redo"))
        {
            hasMovedThisTurn = false;
            shouldEndTurnAfterMove = false;
            BattleManager.battleManager.HidePlayerCombatUIButtons();
            playerCombatUI?.infoUI?.ShowCurrentCharacterInfoUI(playerUnit);
        }
    }

    private bool shouldEndTurnAfterMove = false;

    private void ConfirmMove()
    {
        // Selecting current tile skips movement and opens the action menu
        if (hoveredCell == tileCombatPlayerController.currentPosition && !playerUnit.hasMadeAnActionThisTurn)
        {
            hasMovedThisTurn = true;
            BattleManager.battleManager.ShowPlayerCombatUIButtons();
            return;
        }
        else if (hoveredCell == tileCombatPlayerController.currentPosition && playerUnit.hasMadeAnActionThisTurn)
        {
            shouldEndTurnAfterMove = true;
            hasMovedThisTurn = true;
            return;
        }

        if (!tileCombatPlayerController.RequestToMove(hoveredCell))
            return;

        if (playerUnit.hasMadeAnActionThisTurn)
        {
            shouldEndTurnAfterMove = true;
            hasMovedThisTurn = true;
            return;
        }

        hasMovedThisTurn = true;
    }

    public void OnAttackButtonUp()
    {
        if (playerCombatUI == null) return;
        hasMovedThisTurn = false;
        playerCombatUI.buttonContainer.Visible = false;
        tileCombatPlayerController.ChangeCombatState("PlayerCombatAttackState");
    }

    public void OnActButtonUp()
    {
        if (playerCombatUI == null) return;
        hasMovedThisTurn = false;
        playerCombatUI.buttonContainer.Visible = false;
        tileCombatPlayerController.ChangeCombatState("PlayerCombatActState");
    }

    public void OnItemButtonUp()
    {
        if (playerCombatUI == null) return;
        hasMovedThisTurn = false;
        playerCombatUI.buttonContainer.Visible = false;
        tileCombatPlayerController.ChangeCombatState("PlayerCombatItemState");
    }

    public void OnMagicButtonUp()
    {
        if (playerCombatUI == null) return;
        hasMovedThisTurn = false;
        playerCombatUI.buttonContainer.Visible = false;
        tileCombatPlayerController.ChangeCombatState("PlayerCombatMagicState");
    }

    public void OnDefendButtonUp()
    {
        if (playerCombatUI == null) return;
        hasMovedThisTurn = false;
        playerCombatUI.buttonContainer.Visible = false;
        tileCombatPlayerController.ChangeCombatState("PlayerCombatDefendState");
    }

    protected override void SetupConnections()
    {
        if (connectionsSetup)
        {
            playerCombatUI.attackButton.ButtonUp -= OnAttackButtonUp;
            playerCombatUI.defendButton.ButtonUp -= OnDefendButtonUp;
            playerCombatUI.actButton.ButtonUp -= OnActButtonUp;
            playerCombatUI.itemsButton.ButtonUp -= OnItemButtonUp;
            playerCombatUI.magicButton.ButtonUp -= OnMagicButtonUp;
        }
        playerCombatUI.attackButton.ButtonUp += OnAttackButtonUp;
        playerCombatUI.defendButton.ButtonUp += OnDefendButtonUp;
        playerCombatUI.actButton.ButtonUp += OnActButtonUp;
        playerCombatUI.itemsButton.ButtonUp += OnItemButtonUp;
        playerCombatUI.magicButton.ButtonUp += OnMagicButtonUp;
        connectionsSetup = true;
        RefreshMovementRange();
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            targetCells,
            tileCombatPlayerController.tileSourceId,
            tileCombatPlayerController.atlasCoords
        );
    }

    private void RefreshMovementRange()
    {
        if (playerUnit == null || tileCombatPlayerController == null)
            return;

        if (playerUnit.hasMadeAnActionThisTurn)
        {
            tileCombatPlayerController.reachableCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
                tileCombatPlayerController.currentPosition,
                tileCombatPlayerController.remainingMoveRange,
                playerUnit.characterStats.faction,
                useMovementCost: true
            );
        }
        targetCells = tileCombatPlayerController.reachableCells;
    }
}