using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum ActeeType
{
    Enemy,
    Objective
}

[GlobalClass]
public partial class PlayerCombatActState : PlayerCombatState
{
    public Godot.Collections.Dictionary<Character, Vector2I> validEnemiesInRange = new Godot.Collections.Dictionary<Character, Vector2I>();
    public Godot.Collections.Dictionary<Objective, Vector2I> validObjectivesInRange = new Godot.Collections.Dictionary<Objective, Vector2I>();
    AttackPattern actPattern = new AttackPattern { shape = AttackShape.Cross, minRange = 0, maxRange = 0 };
    private bool actSequenceStarted = false;


    public override void EnterState()
    {
        base.EnterState();
        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            playerUnit.currentCell,
            playerUnit.characterStats.actRange,
            minRange: 1);
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            targetCells,
            tileCombatPlayerController.tileSourceId,
            tileCombatPlayerController.atlasCoords
        );
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            patternCells,
            patternCellIndicatorId,
            patternCellIndicatorAtlas,
            BattleManager.battleManager.combatTileMapLayer.actionIndicatorLayer
        );
        validEnemiesInRange = BattleManager.battleManager.GetCharactersInRangeOf(
            playerUnit.currentCell,
            1,
            playerUnit.characterStats.actRange,
            Faction.Enemy
        );
        validObjectivesInRange = BattleManager.battleManager.GetObjectivesInRangeOf(
            playerUnit.currentCell,
            1,
            playerUnit.characterStats.actRange
        );

        playerCombatUI?.infoUI?.UpdatePhaseLabel(CombatPhase.Action);

    }

    public override void ProcessState(double delta)
    {
        if (actSequenceStarted)
            getActPhaseInput();
        else
            getInput((float)delta);

    }

    public override void ExitState()
    {
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
        validEnemiesInRange.Clear();
        actSequenceStarted = false; // Reset the act sequence flag when exiting the state
        wasLeftMousePressed = false; // Reset the mouse press state when exiting the state
        validObjectivesInRange.Clear();
        base.ExitState();
    }

    public void getInput(float delta)
    {
        InputType inputType = GameManager.currentInputType;

        if (playerUnit == null)
        {
            GD.PrintErr("TileCombatPlayerController: playerUnit is not assigned, or is not a Player.");
            return;
        }

        if (inputType == InputType.KeyboardAndMouse)
        {
            hoveredCell = getKeyboardAndMouseHoveredCell(actPattern);

            if (!targetCells.Contains(hoveredCell))
            {
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles( // Keeps target cells drawn when hovering over invalid cells
                    targetCells,
                    tileCombatPlayerController.tileSourceId,
                    tileCombatPlayerController.atlasCoords
                );
                previousHoveredCell = new Vector2I(int.MinValue, int.MinValue); // Reset previousHoveredCell to an invalid state
            }
            else if (hoveredCell != previousHoveredCell)
            {
                RecomputePatternCellsforHover(hoveredCell);
            }

            bool isLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            if ((isLeftMousePressed && !wasLeftMousePressed) || Input.IsActionJustPressed("combat_confirm"))
            {
                if (targetCells.Contains(hoveredCell))
                {
                    if (validEnemiesInRange.Count > 0)
                    {
                        foreach (var (enemy, enemyCell) in validEnemiesInRange)
                        {
                            if (enemyCell == hoveredCell)
                            {
                                if (enemy is Enemy enemyUnit)
                                {
                                    GD.Print($"Acting on enemy: {enemyUnit.characterStats.characterName} at cell {enemyCell}");
                                    wasLeftMousePressed = isLeftMousePressed;
                                    actSequenceStarted = true;
                                    LoadActs(enemyUnit);
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        GD.Print("No valid enemies in range to act upon.");
                    }
                    if (validObjectivesInRange.Count > 0)
                    {
                        foreach (var (objective, objectiveCell) in validObjectivesInRange)
                        {
                            if (objectiveCell == hoveredCell)
                            {
                                GD.Print($"Acting on objective: {objective.Name} at cell {objectiveCell}");
                                LoadActs(objective);
                                return;
                            }
                        }
                    }
                    else
                    {
                        GD.Print("No valid objectives in range to act upon.");
                    }
                }
                else
                {
                    GD.Print("Clicked on an invalid cell for acting.");
                }
            }
            wasLeftMousePressed = isLeftMousePressed; // Update the mouse press state for the next frame
        }
        else if (inputType == InputType.Controller || inputType == InputType.KeyboardOnly)
        {
            moveOffset = Vector2I.Zero;
            if (inputType == InputType.Controller)
            {
                Vector2 stick = Input.GetVector("stick_left", "stick_right", "stick_up", "stick_down", JoystickDeadzone);
                Vector2I joystickOffset = TickJoystickOffset(stick, delta);
                if (joystickOffset != Vector2I.Zero && moveOffset == Vector2I.Zero)
                    moveOffset = joystickOffset;
            }
            getWASD();

            if (moveOffset != Vector2I.Zero)
            {
                Vector2I newHoveredCell = hoveredCell + moveOffset;
                if (targetCells.Contains(newHoveredCell))
                    hoveredCell = newHoveredCell;
                else
                {
                    Vector2I found = FindNextAvailableCell(newHoveredCell);
                    if (targetCells.Contains(found))
                        hoveredCell = found;
                }
            }

            Vector2I offsetFromPlayer = hoveredCell - playerUnit.currentCell;
            AttackFacing hoveredFacing = AttackPattern.GetFacing(offsetFromPlayer, flipAttackPattern);
            if (hoveredFacing != currentAttackFacing)
            {
                currentAttackFacing = hoveredFacing;
                RecomputePatternCellsforHover(hoveredCell);
            }
            else if (hoveredCell != previousHoveredCell)
            {
                RecomputePatternCellsforHover(hoveredCell);
            }

            if (Input.IsActionJustPressed("combat_confirm"))
            {
                if (targetCells.Contains(hoveredCell))
                {
                    if (validEnemiesInRange.Count > 0)
                    {
                        foreach (var (enemy, enemyCell) in validEnemiesInRange)
                        {
                            if (enemyCell == hoveredCell)
                            {
                                if (enemy is Enemy enemyUnit)
                                {
                                    GD.Print($"Acting on enemy: {enemyUnit.characterStats.characterName} at cell {enemyCell}");
                                    actSequenceStarted = true;
                                    LoadActs(enemyUnit);
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        GD.Print("No valid enemies in range to act upon.");
                    }
                    if (validObjectivesInRange.Count > 0)
                    {
                        foreach (var (objective, objectiveCell) in validObjectivesInRange)
                        {
                            if (objectiveCell == hoveredCell)
                            {
                                GD.Print($"Acting on objective: {objective.Name} at cell {objectiveCell}");
                                LoadActs(objective);
                                return;
                            }
                        }
                    }
                    else
                    {
                        GD.Print("No valid objectives in range to act upon.");
                    }
                }
                else
                {
                    GD.Print("Selected cell is not in targetCells.");
                }
            }

            if (Input.IsActionJustPressed("flip_attack_pattern"))
            {
                flipAttackPattern = !flipAttackPattern;
                currentAttackFacing = AttackPattern.GetFacing(hoveredCell - playerUnit.currentCell, flipAttackPattern);
                RecomputePatternCellsforHover(hoveredCell);
            }

            if (Input.IsActionJustPressed("combat_redo"))
            {
                BattleManager.battleManager.HidePlayerCombatUIButtons();
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
            }
        }
        else
        {
            GD.PrintErr("TileCombatPlayerController: Unsupported input type.");
        }

        if (Input.IsActionJustPressed("combat_redo"))
        {
            BattleManager.battleManager.HidePlayerCombatUIButtons(); // Hide the PlayerCombatUI when the player cancels their attack
            BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles(); // Clear the attack indicator tiles when cancelling the attack
            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
    }

    private async void RecomputePatternCellsforHover(Vector2I hoveredCell)
    {
        previousHoveredCell = hoveredCell;
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(hoveredCell);
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(hoveredCell);
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            targetCells,
            tileCombatPlayerController.tileSourceId,
            tileCombatPlayerController.atlasCoords
        );
        BattleManager.battleManager.combatTileMapLayer.DrawPatternBorders(
            hoveredCell,
            actPattern,
            currentAttackFacing
        );
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            patternCells,
            patternCellIndicatorId,
            patternCellIndicatorAtlas,
            BattleManager.battleManager.combatTileMapLayer.actionIndicatorLayer
        );
        validEnemiesInRange = BattleManager.battleManager.GetCharactersInRangeOf(
            hoveredCell,
            actPattern.minRange,
            actPattern.maxRange,
            Faction.Enemy
        );
        validObjectivesInRange = BattleManager.battleManager.GetObjectivesInRangeOf(
            hoveredCell,
            actPattern.minRange,
            actPattern.maxRange
        );
        await UpdateHoveredCharacterInfo(hoveredCell);
    }

    public async void getActPhaseInput()
    {
        if (Input.IsActionJustPressed("combat_redo"))
        {
            BattleManager.battleManager.HidePlayerCombatUIButtons();
            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
            battleBox.battleUI.ClearActBindings();
            await battleBox.EndActSequence();
            await CameraManager.instance.MoveToCharacter(playerUnit);
            battleBox.battleUI.WillPowerCostLabel.Text = "";
            BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles(); // add this
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
    }

    public async void LoadActs(Node2D node2D)
    {
        if (node2D is Enemy enemyUnit)
        {
            playerUnit.playerActData = enemyUnit.actData.GetAvailableActs(playerUnit.playerActData);

            // Hide all buttons first, then show only what's needed
            foreach (var button in battleBox.battleUI.buttonDictionary.Values)
                button.Visible = false;

            int buttonIndex = 0;
            var sortedActs = new List<ActActions>(playerUnit.playerActData.availableActs);
            sortedActs.Sort((a, b) => a.willPowerCost.CompareTo(b.willPowerCost));
            foreach (var act in sortedActs)
            {
                string key = $"Button{buttonIndex}";
                battleBox.battleUI.BindActToButton(key, playerUnit, act, async (selectedAct) => await ExecuteAct(selectedAct, enemyUnit));
                buttonIndex++;
            }

            await battleBox.BeginActSequence(new Array<Player> { playerUnit }, new Array<Node2D> { enemyUnit }); // Start the act sequence for the player and the selected enemy
            battleBox.battleUI.focusManager?.Refresh();
            battleBox.battleUI.focusManager?.Focus(battleBox.battleUI.focusManager.initialFocus);
        }
        else if (node2D is Objective objectiveUnit)
        {
            playerUnit.playerActData = objectiveUnit.actData.GetAvailableActs(playerUnit.playerActData);
            // Hide all buttons first, then show only what's needed
            foreach (var button in battleBox.battleUI.buttonDictionary.Values)
                button.Visible = false;

            int buttonIndex = 0;
            var sortedActs = new List<ActActions>(playerUnit.playerActData.availableActs);
            sortedActs.Sort((a, b) => a.willPowerCost.CompareTo(b.willPowerCost));
            foreach (var act in sortedActs)
            {
                string key = $"Button{buttonIndex}";
                battleBox.battleUI.BindActToButton(key, playerUnit, act, async (selectedAct) => await ExecuteAct(selectedAct, objectiveUnit));
                buttonIndex++;
            }

            await battleBox.BeginActSequence(new Array<Player> { playerUnit }, new Array<Node2D> { objectiveUnit }); // Start the act sequence for the player and the selected objective
            battleBox.battleUI.focusManager?.Refresh();
            battleBox.battleUI.focusManager?.Focus(battleBox.battleUI.focusManager.initialFocus);
        }
        else
        {
            GD.PrintErr("LoadActs: The provided node is neither an Enemy nor an Objective.");
            return;
        }
    }

    public async Task ExecuteAct(ActActions selectedAct, Node2D targetNode)
    {
        if (playerUnit.characterStats.willPower < selectedAct.willPowerCost)
        {
            GD.Print("Not enough willpower to perform this act.");
            return; // Not enough willpower to perform this act
        }

        playerUnit.hasMadeAnActionThisTurn = true; // Mark that the player has made an action this turn

        if (targetNode is Enemy enemyUnit)
        {
            string resultDialogue = selectedAct.ExecuteActOnEnemy(playerUnit, enemyUnit);

            enemyUnit.actData.currentMercyLevel += selectedAct.mercyIncrease;

            battleBox.battleUI.resultDescriptionLabel.Text = resultDialogue;
            foreach (var (key, button) in battleBox.battleUI.buttonDictionary)
            {
                button.Visible = false; // Hide all buttons after executing the act
            }

            await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);

            battleBox.battleUI.ClearActBindings(); // Clear the act buttons after executing the act
            await battleBox.EndActSequence(); // Await the slide-back before starting the next turn

            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();

            if (tileCombatPlayerController.remainingMoveRange <= 0)
                tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
            else
                tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
        else if (targetNode is Objective objectiveUnit)
        {
            string resultDialogue = selectedAct.ExecuteActOnObjective(playerUnit, objectiveUnit);
            battleBox.battleUI.resultDescriptionLabel.Text = resultDialogue;
            foreach (var (key, button) in battleBox.battleUI.buttonDictionary)
            {
                button.Visible = false; // Hide all buttons after executing the act
            }
            await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
            battleBox.battleUI.ClearActBindings(); // Clear the act buttons after executing the act
            await battleBox.EndActSequence(); // Await the slide-back before starting the next turn

            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();

            if (tileCombatPlayerController.remainingMoveRange <= 0)
                tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
            else
                tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
        else
        {
            GD.PrintErr("ExecuteAct: The provided target node is neither an Enemy nor an Objective.");
            return;
        }
    }
}