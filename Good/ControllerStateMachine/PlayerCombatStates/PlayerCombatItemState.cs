using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum ItemStatePhase
{
    ChoosingItem,
    ChoosingTarget,
}

[GlobalClass]
public partial class PlayerCombatItemState : PlayerCombatState
{
    public Godot.Collections.Dictionary<Character, Vector2I> validCharactersInRange = new Godot.Collections.Dictionary<Character, Vector2I>();
    public Godot.Collections.Dictionary<Objective, Vector2I> validObjectivesInRange = new Godot.Collections.Dictionary<Objective, Vector2I>();

    private bool itemSequenceStarted = false;
    private ItemStatePhase phase = ItemStatePhase.ChoosingItem;
    private Item selectedItem = null;
    private bool isExecuting = false;

    public async override void EnterState()
    {
        base.EnterState();
        phase = ItemStatePhase.ChoosingItem;
        selectedItem = null;
        wasLeftMousePressed = false;
        itemSequenceStarted = false;

        battleBox.battleUI.currentMode = BattleUIMode.Item;
        LoadItems(0, 0); // Load the first page of items when entering the item state
        await battleBox.BeginItemSequenceNoTarget(playerUnit);
        itemSequenceStarted = true;

        playerCombatUI?.infoUI?.UpdatePhaseLabel(CombatPhase.Action);
    }

    public override void ProcessState(double delta)
    {
        if (phase == ItemStatePhase.ChoosingTarget && isExecuting == false)
            getTargetInput((float)delta);
        if (phase == ItemStatePhase.ChoosingItem)
            getItemPhaseInput();
    }

    public override void ExitState()
    {
        battleBox.battleUI.ClearItemBindings();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        validCharactersInRange.Clear();
        validObjectivesInRange.Clear();
        itemSequenceStarted = false;
        isExecuting = false;
        phase = ItemStatePhase.ChoosingItem;
        selectedItem = null;
        wasLeftMousePressed = false;
        base.ExitState();
    }

    public void getTargetInput(float delta = 0)
    {
        InputType inputType = GameManager.currentInputType;

        if (selectedItem == null)
        {
            GD.PrintErr("PlayerCombatItemState: selectedItem is null, cannot get target input.");
            return;
        }

        if (inputType == InputType.KeyboardAndMouse)
        {
            hoveredCell = getKeyboardAndMouseHoveredCell(selectedItem.itemTargetPattern);

            if (Input.IsActionJustPressed("flip_attack_pattern"))
                flipAttackPattern = !flipAttackPattern;

            Vector2I offsetFromPlayer = hoveredCell - playerUnit.currentCell;
            AttackFacing hoveredFacing = AttackPattern.GetFacing(offsetFromPlayer, flipAttackPattern);


            bool facingChanged = hoveredFacing != currentAttackFacing;
            if (!targetCells.Contains(hoveredCell))
            {
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
                    targetCells,
                    tileCombatPlayerController.tileSourceId,
                    tileCombatPlayerController.atlasCoords
                );
                previousHoveredCell = new Vector2I(int.MinValue, int.MinValue);
                return;
            }
            else if (facingChanged)
            {
                currentAttackFacing = hoveredFacing;
                RecomputePatternCellsForHover(hoveredCell);
            }
            else if (hoveredCell != previousHoveredCell)
            {
                RecomputePatternCellsForHover(hoveredCell);
            }

            // "left_click" isn't a defined InputMap action, so track the raw mouse button ourselves and
            // only trigger on the frame it's first pressed (not every frame while held).
            bool isLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            if (((isLeftMousePressed && !wasLeftMousePressed) || Input.IsActionJustPressed("combat_confirm")) && !isExecuting)
            {
                if (targetCells.Contains(hoveredCell))
                {
                    var characterTargets = new Array<Character>();
                    var objectiveTargets = new Array<Objective>();
                    if (selectedItem.itemTargetType == ItemTargetType.Allies || selectedItem.itemTargetType == ItemTargetType.Enemies || selectedItem.itemTargetType == ItemTargetType.Any)
                    {
                        foreach (var (character, characterCell) in validCharactersInRange)
                            if (patternCells.Contains(characterCell))
                                characterTargets.Add(character);

                        if (selectedItem.itemTargetType == ItemTargetType.Any)
                            foreach (var (objective, objectiveCell) in validObjectivesInRange)
                                if (patternCells.Contains(objectiveCell))
                                    objectiveTargets.Add(objective);
                    }
                    else if (selectedItem.itemTargetType == ItemTargetType.Objectives)
                    {
                        foreach (var (objective, objectiveCell) in validObjectivesInRange)
                        {
                            if (patternCells.Contains(objectiveCell))
                                objectiveTargets.Add(objective);
                        }
                    }
                    if (characterTargets.Count > 0)
                    {
                        isExecuting = true;
                        _ = ExecuteItem(selectedItem, playerUnit, characterTargets);
                    }
                    else if (objectiveTargets.Count > 0)
                    {
                        isExecuting = true;
                        _ = ExecuteItemOnObjective(selectedItem, playerUnit, objectiveTargets);
                    }
                    else
                    {
                        GD.Print("No valid targets in range for the selected item.");
                    }
                }
                else
                {
                    GD.Print("Clicked cell is not in targetCells.");
                }
            }
            wasLeftMousePressed = isLeftMousePressed;

            if (Input.IsActionJustPressed("combat_redo"))
            {
                phase = ItemStatePhase.ChoosingItem;
                selectedItem = null;
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                validCharactersInRange.Clear();
                validObjectivesInRange.Clear();
                LoadItems(0, 0); // Reload the first page of items when going back to choosing an item
            }
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
            AttackFacing facing = AttackPattern.GetFacing(offsetFromPlayer, flipAttackPattern);
            if (facing != currentAttackFacing)
            {
                currentAttackFacing = facing;
                RecomputePatternCellsForHover(hoveredCell);
            }
            else if (hoveredCell != previousHoveredCell)
            {
                RecomputePatternCellsForHover(hoveredCell);
            }

            if (Input.IsActionJustPressed("combat_confirm") && !isExecuting)
            {
                if (targetCells.Contains(hoveredCell))
                {
                    var characterTargets = new Array<Character>();
                    var objectiveTargets = new Array<Objective>();
                    if (selectedItem.itemTargetType == ItemTargetType.Allies || selectedItem.itemTargetType == ItemTargetType.Enemies || selectedItem.itemTargetType == ItemTargetType.Any)
                    {
                        foreach (var (character, characterCell) in validCharactersInRange)
                            if (patternCells.Contains(characterCell))
                                characterTargets.Add(character);
                        if (selectedItem.itemTargetType == ItemTargetType.Any)
                            foreach (var (objective, objectiveCell) in validObjectivesInRange)
                                if (patternCells.Contains(objectiveCell))
                                    objectiveTargets.Add(objective);
                    }
                    else if (selectedItem.itemTargetType == ItemTargetType.Objectives)
                    {
                        foreach (var (objective, objectiveCell) in validObjectivesInRange)
                            if (patternCells.Contains(objectiveCell))
                                objectiveTargets.Add(objective);
                    }
                    if (characterTargets.Count > 0)
                    {
                        isExecuting = true;
                        _ = ExecuteItem(selectedItem, playerUnit, characterTargets);
                    }
                    else if (objectiveTargets.Count > 0)
                    {
                        isExecuting = true;
                        _ = ExecuteItemOnObjective(selectedItem, playerUnit, objectiveTargets);
                    }
                    else GD.Print("No valid targets in range for the selected item.");
                }
            }

            if (Input.IsActionJustPressed("flip_attack_pattern"))
            {
                flipAttackPattern = !flipAttackPattern;
                currentAttackFacing = AttackPattern.GetFacing(hoveredCell - playerUnit.currentCell, flipAttackPattern);
                RecomputePatternCellsForHover(hoveredCell);
            }

            if (Input.IsActionJustPressed("combat_redo"))
            {
                phase = ItemStatePhase.ChoosingItem;
                playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
                selectedItem = null;
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                validCharactersInRange.Clear();
                validObjectivesInRange.Clear();
                LoadItems(0, 0);
            }
        }
    }

    private async void RecomputePatternCellsForHover(Vector2I hoveredCell)
    {
        previousHoveredCell = hoveredCell;
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(hoveredCell);
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(
            hoveredCell,
            selectedItem.itemSplashPattern,
            currentAttackFacing
        );
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
        BattleManager.battleManager.combatTileMapLayer.DrawPatternBorders(
            hoveredCell,
            selectedItem.itemSplashPattern,
            currentAttackFacing
        );
        switch (selectedItem.itemTargetType)
        {
            case ItemTargetType.Allies:
                validCharactersInRange = BattleManager.battleManager.GetCharactersInRangeOf(
                    hoveredCell, selectedItem.itemSplashPattern.minRange, selectedItem.itemSplashPattern.maxRange, Faction.Player);
                break;

            case ItemTargetType.Enemies:
                validCharactersInRange = BattleManager.battleManager.GetCharactersInRangeOf(
                    hoveredCell, selectedItem.itemSplashPattern.minRange, selectedItem.itemSplashPattern.maxRange, Faction.Enemy);
                break;
            case ItemTargetType.Objectives:
                validObjectivesInRange = BattleManager.battleManager.GetObjectivesInRangeOf(
                    hoveredCell, selectedItem.itemSplashPattern.minRange, selectedItem.itemSplashPattern.maxRange);
                break;
            case ItemTargetType.Any:
                var allies = BattleManager.battleManager.GetCharactersInRangeOf(
                    hoveredCell, selectedItem.itemSplashPattern.minRange, selectedItem.itemSplashPattern.maxRange, Faction.Player);
                var enemies = BattleManager.battleManager.GetCharactersInRangeOf(
                    hoveredCell, selectedItem.itemSplashPattern.minRange, selectedItem.itemSplashPattern.maxRange, Faction.Enemy);
                foreach (var kvp in allies) validCharactersInRange[kvp.Key] = kvp.Value;
                foreach (var kvp in enemies) validCharactersInRange[kvp.Key] = kvp.Value;
                validObjectivesInRange = BattleManager.battleManager.GetObjectivesInRangeOf(
                    hoveredCell, selectedItem.itemSplashPattern.minRange, selectedItem.itemSplashPattern.maxRange);
                break;
        }
        await UpdateHoveredCharacterInfo(hoveredCell);
    }

    public async void getItemPhaseInput()
    {
        if (Input.IsActionJustPressed("combat_redo"))
        {
            BattleManager.battleManager.HidePlayerCombatUIButtons();
            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
            await battleBox.EndItemSequenceNoTarget(); // slide back before changing State
            battleBox.battleUI.WillPowerCostLabel.Text = "";
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
    }

    public void LoadItems(int xPageIndex, int yPageIndex) // XPageIndex will have a seperate tab for keyItems, yPageIndex will be for scrolling through the items
    {
        battleBox.battleUI.resultDescriptionLabel.Text = "";
        GD.Print($"LoadItems called. Inventory item count: {playerUnit.playerInventory.items.Count}");
        foreach (var item in playerUnit.playerInventory.items)
            GD.Print($"  - {item.itemName}, isUsableInCombat={item.isUsableInCombat}, isKeyItem={item.isKeyItem}");

        foreach (var button in battleBox.battleUI.buttonDictionary.Values)
            button.Visible = false; // Hide all buttons to show only item buttons later

        var combatItems = new List<Item>();

        if (xPageIndex == 1) // If the xPageIndex is 1, we want to show key items instead of basic combat items
        {
            foreach (var item in playerUnit.playerInventory.items)
            {
                if (item.isUsableInCombat && item.isKeyItem)
                    combatItems.Add(item);
            }
        }
        else // If the xPageIndex is 0, we want to show basic combat items
        {
            foreach (var item in playerUnit.playerInventory.items)
            {
                if (item.isUsableInCombat && !item.isKeyItem)
                    combatItems.Add(item);
            }
        }

        if (battleBox.battleUI.WillPowerCostLabel != null)
        {
            if (xPageIndex == 0)
            {
                battleBox.battleUI.WillPowerCostLabel.Text = "Basic Items"; //I didn't want make a new label, or change its name
                battleBox.battleUI.WillPowerCostLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1)); // Set the color to white for basic items
            }

            else if (xPageIndex == 1)
            {
                battleBox.battleUI.WillPowerCostLabel.Text = "Key Items"; //I didn't want make a new label, or change its name
                battleBox.battleUI.WillPowerCostLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0)); // Set the color to yellow for key items
            }
        }

        int startIndex = yPageIndex * 8; // Assuming 8 items per page
        int buttonIndex = 0;

        for (int i = startIndex; i < combatItems.Count && buttonIndex < 8; i++)
        {
            string buttonKey = $"Button{buttonIndex}";
            battleBox.battleUI.BindItemToButton(buttonKey, combatItems[i], playerUnit, OnItemSelected);
            buttonIndex++;
        }

        battleBox.battleUI.focusManager?.Refresh();
        battleBox.battleUI.focusManager?.Focus(battleBox.battleUI.focusManager.initialFocus);
    }

    public async void OnItemSelected(Item item, Character user)
    {
        GD.Print("OnItemSelected called with item: " + item.itemName);
        selectedItem = item;
        battleBox.battleUI.ClearItemBindings();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        validCharactersInRange.Clear();
        validObjectivesInRange.Clear();
        battleBox.battleUI.WillPowerCostLabel.Text = "";

        if (item.itemTargetType == ItemTargetType.None || item.itemTargetType == ItemTargetType.World)
        {
            GD.PrintErr("Item Does not have a Target Viable for Combat, game will freeze");
            _ = ExecuteItemNoTarget(item, user);
            return;
        }

        // Slide back and close BattleBox before entering target selection
        await battleBox.EndItemSequenceNoTarget();

        phase = ItemStatePhase.ChoosingTarget;

        // Now draw indicator tiles on the normal map view
        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            playerUnit.currentCell,
            item.itemTargetPattern.maxRange,
            minRange: item.itemTargetPattern.minRange
        );
    }

    private async Task ExecuteItem(Item item, Character user, Array<Character> targets)
    {
        // Validate all targets first, then separate the user from other targets
        // (never mutate the collection inside a foreach — it skips elements)
        bool userIncludedInTargets = false;
        var otherTargets = new Array<Character>();

        playerUnit.hasMadeAnActionThisTurn = true; // Mark that the player has made an action this turn


        foreach (var target in targets)
        {
            if (target == user && !item.isUsableOnSelf)
            {
                GD.Print($"{item.itemName} cannot be used on self.");
                phase = ItemStatePhase.ChoosingTarget;
                return;
            }
            if (target != user && !item.isUsableOnAllies && target is Player)
            {
                GD.Print($"{item.itemName} cannot be used on allies.");
                phase = ItemStatePhase.ChoosingTarget;
                return;
            }
            if (target is Enemy && !item.isUsableOnEnemies)
            {
                GD.Print($"{item.itemName} cannot be used on enemies.");
                phase = ItemStatePhase.ChoosingTarget;
                return;
            }
            GD.Print("ExecuteItem called with item: " + item.itemName + ", target: " + target.characterStats.characterName);
            if (target == user)
                userIncludedInTargets = true;
            else
                otherTargets.Add(target);
        }

        await battleBox.BeginItemSequence(new Array<Player> { playerUnit }, new Array<Node2D>(otherTargets));

        foreach (var target in otherTargets)
        {
            item.UseItemOnCharacter(user, target);
        }
        if (userIncludedInTargets)
        {
            item.UseItemOnCharacter(user, user);
        }



        string result = $"{user.characterStats.characterName} used {item.itemName} on {targets.Count} target(s).";
        playerUnit.playerInventory.RemoveItem(item);

        battleBox.battleUI.resultDescriptionLabel.Text = result;
        battleBox.battleUI.ClearItemBindings();

        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        await battleBox.EndItemSequence();

        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();

        if (tileCombatPlayerController.remainingMoveRange <= 0)
            tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
        else
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
    }

    private async Task ExecuteItemNoTarget(Item item, Character user)
    {
        playerUnit.hasMadeAnActionThisTurn = true; // Mark that the player has made an action this turn
        GD.Print("ExecuteItemNoTarget called with item: " + item.itemName);
        string result = item.UseItemOnCharacter(user, null);
        playerUnit.playerInventory.RemoveItem(item);

        battleBox.battleUI.resultDescriptionLabel.Text = result;

        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        battleBox.battleUI.ClearItemBindings();
        await battleBox.EndItemSequenceNoTarget(); // use NoTarget version since no BeginItemSequence was called
        if (tileCombatPlayerController.remainingMoveRange <= 0)
            tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
        else
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
    }

    private async Task ExecuteItemOnObjective(Item item, Character user, Array<Objective> objectives)
    {
        playerUnit.hasMadeAnActionThisTurn = true; // Mark that the player has made an action this turn


        foreach (var objective in objectives)
        {
            if (!item.isUsableOnObjectives)
            {
                GD.Print($"{item.itemName} cannot be used on {objective.objectiveName}.");
                phase = ItemStatePhase.ChoosingTarget;
                return;
            }
            GD.Print("ExecuteItemOnObjective called with item: " + item.itemName + ", target: " + (objective != null ? objective.objectiveName : "null"));
        }

        await battleBox.BeginItemSequence(new Array<Player> { playerUnit }, new Array<Node2D>(objectives));

        foreach (var objective in objectives)
        {
            string result = item.UseItemOnObjective(user, objective);
        }
        playerUnit.playerInventory.RemoveItem(item);

        //battleBox.battleUI.actResultDescriptionLabel.Text = result;
        battleBox.battleUI.ClearItemBindings();

        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
        await battleBox.EndItemSequence();

        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();

        if (tileCombatPlayerController.remainingMoveRange <= 0)
            tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
        else
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
    }


}
