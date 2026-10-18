using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum MagicStatePhase
{
    ChoosingSpell,
    ChoosingTarget,
}

[GlobalClass]
public partial class PlayerCombatMagicState : PlayerCombatState
{

    public Godot.Collections.Dictionary<Character, Vector2I> validCharactersInRange = new Godot.Collections.Dictionary<Character, Vector2I>();
    public Godot.Collections.Dictionary<Objective, Vector2I> validObjectivesInRange = new Godot.Collections.Dictionary<Objective, Vector2I>();

    private bool magicSequenceStarted = false;
    private MagicStatePhase phase = MagicStatePhase.ChoosingSpell;
    private Spell selectedSpell = null;
    private bool isExecuting = false;

    public override async void EnterState()
    {
        base.EnterState();

        phase = MagicStatePhase.ChoosingSpell;
        selectedSpell = null;
        wasLeftMousePressed = false;
        magicSequenceStarted = false;

        await battleBox.BeginSpellSequenceNoTarget(playerUnit);
        magicSequenceStarted = true;
        LoadSpells(0); // Load the first page of spells when entering the magic state

        playerCombatUI?.infoUI?.UpdatePhaseLabel(CombatPhase.Action);
    }

    public override void ProcessState(double delta)
    {
        if (phase == MagicStatePhase.ChoosingSpell)
            getSpellPhaseInput();
        if (phase == MagicStatePhase.ChoosingTarget && isExecuting == false)
            getTargetInput((float)delta);
    }

    public override void ExitState()
    {
        battleBox.battleUI.ClearSpellBindings();
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
        validCharactersInRange.Clear();
        validObjectivesInRange.Clear();
        magicSequenceStarted = false;
        phase = MagicStatePhase.ChoosingSpell;
        isExecuting = false;
        selectedSpell = null;
        wasLeftMousePressed = false;
        base.ExitState();
    }

    private async void getTargetInput(float delta = 0)
    {
        InputType inputType = GameManager.currentInputType;

        if (selectedSpell == null)
        {
            GD.PrintErr("PlayerCombatMagicState: selectedSpell is null in getTargetInput().");
            return;
        }

        if (inputType == InputType.KeyboardAndMouse)
        {
            if (Input.IsActionJustPressed("flip_attack_pattern"))
                flipAttackPattern = !flipAttackPattern;

            hoveredCell = getKeyboardAndMouseHoveredCell(selectedSpell.spellAttackPattern);

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

            bool isLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            if (((isLeftMousePressed && !wasLeftMousePressed) || Input.IsActionJustPressed("combat_confirm")) && !isExecuting)
            {
                if (targetCells.Contains(hoveredCell))
                {
                    var characterTargets = new Array<Character>();
                    var objectiveTargets = new Array<Objective>();
                    if (selectedSpell.spellTargetTypes.Contains(SpellTargetType.Allies) || selectedSpell.spellTargetTypes.Contains(SpellTargetType.Enemies) || selectedSpell.spellTargetTypes.Contains(SpellTargetType.Any))
                    {
                        foreach (var kvp in validCharactersInRange)
                            if (patternCells.Contains(kvp.Value))
                                characterTargets.Add(kvp.Key);
                        if (selectedSpell.spellTargetTypes.Contains(SpellTargetType.Objectives))
                            foreach (var kvp in validObjectivesInRange)
                                if (patternCells.Contains(kvp.Value))
                                    objectiveTargets.Add(kvp.Key);
                    }
                    else if (selectedSpell.spellTargetTypes.Contains(SpellTargetType.Objectives))
                    {
                        foreach (var kvp in validObjectivesInRange)
                            if (patternCells.Contains(kvp.Value))
                                objectiveTargets.Add(kvp.Key);
                    }

                    if (characterTargets.Count > 0 || objectiveTargets.Count > 0)
                    {
                        isExecuting = true;
                        await ExecuteSpell(selectedSpell, playerUnit, characterTargets, objectiveTargets);
                        isExecuting = false;
                    }
                    else
                    {
                        GD.Print("No valid targets in Range for this spell. Please select a different target.");
                    }
                }
                else
                {
                    GD.Print("Please select a valid target cell for this spell.");
                }
            }
            wasLeftMousePressed = isLeftMousePressed;

            if (Input.IsActionJustPressed("combat_redo"))
            {
                phase = MagicStatePhase.ChoosingSpell;
                playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
                selectedSpell = null;
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                validCharactersInRange.Clear();
                validObjectivesInRange.Clear();
                LoadSpells(0); // Reload the first page of spells when going back to choosing a spell
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
                    if (selectedSpell.spellTargetTypes.Contains(SpellTargetType.Allies) || selectedSpell.spellTargetTypes.Contains(SpellTargetType.Enemies) || selectedSpell.spellTargetTypes.Contains(SpellTargetType.Any))
                    {
                        foreach (var kvp in validCharactersInRange)
                            if (patternCells.Contains(kvp.Value))
                                characterTargets.Add(kvp.Key);
                        if (selectedSpell.spellTargetTypes.Contains(SpellTargetType.Objectives))
                            foreach (var kvp in validObjectivesInRange)
                                if (patternCells.Contains(kvp.Value))
                                    objectiveTargets.Add(kvp.Key);
                    }
                    else if (selectedSpell.spellTargetTypes.Contains(SpellTargetType.Objectives))
                    {
                        foreach (var kvp in validObjectivesInRange)
                            if (patternCells.Contains(kvp.Value))
                                objectiveTargets.Add(kvp.Key);
                    }
                    if (characterTargets.Count > 0 || objectiveTargets.Count > 0)
                    {
                        isExecuting = true;
                        await ExecuteSpell(selectedSpell, playerUnit, characterTargets, objectiveTargets);
                        isExecuting = false;
                    }
                    else GD.Print("No valid targets in range for this spell.");
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
                phase = MagicStatePhase.ChoosingSpell;
                playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
                selectedSpell = null;
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                validCharactersInRange.Clear();
                validObjectivesInRange.Clear();
                LoadSpells(0);
            }
        }
    }

    public async void getSpellPhaseInput()
    {
        if (Input.IsActionJustPressed("combat_redo"))
        {
            BattleManager.battleManager.HidePlayerCombatUIButtons();
            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
            await battleBox.EndSpellSequenceNoTarget(); // slide back before changing State
            battleBox.battleUI.WillPowerCostLabel.Text = "";
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
    }

    private async void RecomputePatternCellsForHover(Vector2I hoveredCell)
    {
        previousHoveredCell = hoveredCell;
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(hoveredCell);
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(
            hoveredCell,
            selectedSpell.spellSplashPattern,
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
            selectedSpell.spellSplashPattern,
            currentAttackFacing
        );
        switch (selectedSpell.spellTargetTypes)
        {
            case Array<SpellTargetType> targetTypes:
                foreach (var targetType in targetTypes)
                {
                    switch (targetType)
                    {
                        case SpellTargetType.Allies:
                            validCharactersInRange = BattleManager.battleManager.GetCharactersInRangeOf(
                                hoveredCell, selectedSpell.spellSplashPattern.minRange, selectedSpell.spellSplashPattern.maxRange, Faction.Player);
                            break;
                        case SpellTargetType.Enemies:
                            validCharactersInRange = BattleManager.battleManager.GetCharactersInRangeOf(
                                hoveredCell, selectedSpell.spellSplashPattern.minRange, selectedSpell.spellSplashPattern.maxRange, Faction.Enemy);
                            break;
                        case SpellTargetType.Objectives:
                            validObjectivesInRange = BattleManager.battleManager.GetObjectivesInRangeOf(
                                hoveredCell, selectedSpell.spellSplashPattern.minRange, selectedSpell.spellSplashPattern.maxRange);
                            break;
                        case SpellTargetType.Any:
                            var allies = BattleManager.battleManager.GetCharactersInRangeOf(
                                hoveredCell, selectedSpell.spellSplashPattern.minRange, selectedSpell.spellSplashPattern.maxRange, Faction.Player);
                            var enemies = BattleManager.battleManager.GetCharactersInRangeOf(
                                hoveredCell, selectedSpell.spellSplashPattern.minRange, selectedSpell.spellSplashPattern.maxRange, Faction.Enemy);
                            foreach (var kvp in allies) validCharactersInRange[kvp.Key] = kvp.Value;
                            foreach (var kvp in enemies) validCharactersInRange[kvp.Key] = kvp.Value;
                            validObjectivesInRange = BattleManager.battleManager.GetObjectivesInRangeOf(
                                hoveredCell, selectedSpell.spellSplashPattern.minRange, selectedSpell.spellSplashPattern.maxRange);
                            break;
                    }
                }
                break;
        }
        await UpdateHoveredCharacterInfo(hoveredCell);
    }

    public void LoadSpells(int yPageIndex)
    {
        battleBox.battleUI.resultDescriptionLabel.Text = "";
        GD.Print("Loading Spells for PlayerCombatMagicState, page: " + yPageIndex);

        foreach (var button in battleBox.battleUI.buttonDictionary.Values)
        {
            button.Visible = false;
        }

        int startIndex = yPageIndex * 8;
        int buttonIndex = 0;
        var sortedSpells = new List<Spell>(playerUnit.spellBook.Spells);
        sortedSpells.Sort((a, b) => a.willPowerCost.CompareTo(b.willPowerCost));

        for (int i = startIndex; i < sortedSpells.Count && buttonIndex < 8; i++)
        {
            string buttonKey = $"Button{buttonIndex}";
            battleBox.battleUI.BindSpellToButton(buttonKey, sortedSpells[i], playerUnit, OnSpellSelected);
            buttonIndex++;
        }

        battleBox.battleUI.focusManager?.Refresh();
        battleBox.battleUI.focusManager?.Focus(battleBox.battleUI.focusManager.initialFocus);
    }

    public async void OnSpellSelected(Spell spell, Character user)
    {
        if (playerUnit.characterStats.willPower < spell.willPowerCost)
            return; // Not enough willpower to cast this spell
        GD.Print("OnSpellSelected called with spell: " + spell.spellName);

        selectedSpell = spell;
        battleBox.battleUI.ClearSpellBindings();
        validCharactersInRange.Clear();
        validObjectivesInRange.Clear();
        battleBox.battleUI.WillPowerCostLabel.Text = "";

        if (spell.spellTargetTypes.Contains(SpellTargetType.None) || spell.spellTargetTypes.Contains(SpellTargetType.World))
        {
            GD.Print("Spell has no target type or is usable in world. Executing spell immediately.");
            await ExecuteSpell(spell, user, new Array<Character>(), new Array<Objective>());
            return;
        }

        await battleBox.EndSpellSequenceNoTarget();

        if (GameManager.currentInputType == InputType.KeyboardAndMouse)
            wasLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);

        phase = MagicStatePhase.ChoosingTarget;

        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            user.currentCell,
            spell.spellAttackPattern.maxRange,
            minRange: spell.spellAttackPattern.minRange
        );
    }

    private async Task ExecuteSpell(Spell spell, Character user, Array<Character> characterTargets, Array<Objective> objectiveTargets)
    {
        GD.Print("Executing spell: " + spell.spellName);

        playerUnit.hasMadeAnActionThisTurn = true; // Mark that the player has made an action this turn


        bool userIncludedInTargets = false;
        var otherTargets = new Array<Node2D>();

        string resultDescription = "";

        foreach (var character in characterTargets)
        {
            if (!spell.isUsableOnSelf && user == character)
            {
                GD.Print("Cannot use this spell on self.");
                continue;
            }
            if (character != user && !spell.isUsableOnAllies && character is Player)
            {
                GD.Print("Cannot use this spell on allies.");
                continue;
            }
            if (character is Enemy && !spell.isUsableOnEnemies)
            {
                GD.Print("Cannot use this spell on enemies.");
                continue;
            }
            if (character == user)
                userIncludedInTargets = true;
            else
                otherTargets.Add(character);
        }

        foreach (var objective in objectiveTargets)
        {
            if (!spell.isUsableOnObjectives)
            {
                GD.Print("Cannot use this spell on objectives.");
                continue;
            }
            otherTargets.Add(objective);
        }


        battleBox.battleUI.resultDescriptionLabel.Text = resultDescription.Trim();

        await UpdateHoveredCharacterInfo(characterTargets[0].currentCell, true);

        await battleBox.BeginSpellSequence(spell, new Array<Player> { playerUnit }, new Array<Node2D>(otherTargets));

        foreach (var target in otherTargets)
        {
            spell.UseSpell(user, target);
        }
        if (userIncludedInTargets)
        {
            spell.UseSpell(user, user);
        }

        // TODO: PlaySpellAnimation and Await its finish
        _ = UpdateHoveredCharacterInfo(characterTargets[0].currentCell, false);

        string result = $"{user.characterStats.characterName} used {spell.spellName} on {characterTargets.Count + objectiveTargets.Count} target(s).";

        battleBox.battleUI.resultDescriptionLabel.Text = result;

        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        battleBox.battleUI.ClearSpellBindings();
        await battleBox.EndSpellSequence();

        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();

        if (tileCombatPlayerController.remainingMoveRange <= 0)
            tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
        else
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
    }
}