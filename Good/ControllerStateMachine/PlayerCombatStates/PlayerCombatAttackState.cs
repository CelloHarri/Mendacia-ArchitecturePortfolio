using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class PlayerCombatAttackState : PlayerCombatState
{
    Godot.Collections.Dictionary<Character, Vector2I> validEnemiesInRange = new Godot.Collections.Dictionary<Character, Vector2I>();

    //HOPE TO GOD I DONT NEED TO TOUCH THIS AGAIN
    private bool isSkillCheckActive = false;
    private bool isMissedSequenceActive = false;
    private bool isFinishScheduled = false;
    private List<Character> skillCheckTargets = new List<Character>();
    private float skillCheckTimer = 0.0f;
    private float missedTimer = 0.0f;
    private const int MinHitFrame = 14;
    private const int Frame20 = 19;
    private const int Frame25 = 24;
    private const int PerfectFrame = 29;
    private const int MaxHitFrame = 34;
    private const float SkillCheckFrameRate = 30.0f;
    private const float MaxSkillCheckDuration = 1f;
    private const float MissedAnimationDuration = 6.0f / 30.0f; // 6 frames at 30 FPS
    private const float MissedHitWindow = 5.0f / 30.0f;        // input allowed during first 5 frames
    private const float MissedHitMultiplier = 0.8f;
    private const float AttackFinishDelay = 0.5f;
    private bool attacked = false;

    private float holdAfterAttackTimer = 0.5f;

    public override void EnterState()
    {
        base.EnterState();
        targetCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
            playerUnit.currentCell,
            playerUnit.characterStats.attackRangePattern.maxRange,
            minRange: playerUnit.characterStats.attackRangePattern.minRange,
            pattern: playerUnit.characterStats.attackRangePattern);
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(
            targetCells,
            tileCombatPlayerController.tileSourceId + 1, // AttackIndicator Tiles are on the next tile source ID
            tileCombatPlayerController.atlasCoords
        );

        if (targetCells.Count > 0)
        {
            if (!targetCells.Contains(hoveredCell))
            {
                foreach (var cell in targetCells)
                {
                    hoveredCell = cell;
                    break;
                }
            }
            lastValidHoveredCell = hoveredCell;
            currentAttackFacing = AttackPattern.GetFacing(hoveredCell - playerUnit.currentCell, flipAttackPattern);
            RecomputePatternCellsForHover(hoveredCell);

            playerCombatUI?.infoUI?.UpdatePhaseLabel(CombatPhase.Action);
        }
    }

    public override void ProcessState(double delta)
    {
        if (isSkillCheckActive)
        {
            ProcessSkillCheck(delta);
        }
        else
        {
            getInput((float)delta);
        }
    }

    public override void ExitState()
    {
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
        validEnemiesInRange.Clear();
        playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
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
            hoveredCell = getKeyboardAndMouseHoveredCell(playerUnit.characterStats.attackRangePattern);

            if (Input.IsActionJustPressed("flip_attack_pattern"))
                flipAttackPattern = !flipAttackPattern;

            Vector2I offsetFromPlayer = hoveredCell - playerUnit.currentCell;
            AttackFacing hoveredFacing = AttackPattern.GetFacing(offsetFromPlayer, flipAttackPattern);


            bool facingChanged = hoveredFacing != currentAttackFacing;
            if (!targetCells.Contains(hoveredCell))
            {
                BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
                BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles( // Keeps target cells drawn when hovering over invalid cells
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

            bool isLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            if (isLeftMousePressed && !wasLeftMousePressed)
            {
                if (targetCells.Contains(hoveredCell))
                {
                    var targets = new List<Character>();
                    foreach (var (enemy, enemyCell) in validEnemiesInRange)
                    {
                        if (patternCells.Contains(enemyCell))
                            targets.Add(enemy);
                    }
                    if (targets.Count > 0)
                    {
                        StartAttackSkillCheck(targets);
                        return;
                    }
                    else
                    {
                        GD.Print("No valid enemies in range to attack.");
                    }
                }
                else
                {
                    GD.Print("Hovered cell is not a valid attack cell.");
                }
            }
            wasLeftMousePressed = isLeftMousePressed;

            if (Input.IsActionJustPressed("combat_confirm"))
            {
                BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
                var targets = new List<Character>();
                foreach (var (enemy, enemyCell) in validEnemiesInRange)
                    if (patternCells.Contains(enemyCell))
                        targets.Add(enemy);
                if (targets.Count > 0) { StartAttackSkillCheck(targets); return; }
                else GD.Print("No valid enemies in range to attack.");
            }
        }
        else if (inputType == InputType.Controller)
        {
            moveOffset = Vector2I.Zero;
            Vector2 stick = Input.GetVector("stick_left", "stick_right", "stick_up", "stick_down", JoystickDeadzone);
            Vector2I joystickOffset = TickJoystickOffset(stick, delta);
            if (joystickOffset != Vector2I.Zero && moveOffset == Vector2I.Zero)
                moveOffset = joystickOffset;

            getWASD();

            if (moveOffset != Vector2I.Zero)
            {
                Vector2I newHoveredCell = hoveredCell + moveOffset;
                if (targetCells.Contains(newHoveredCell))
                    hoveredCell = newHoveredCell;
                else
                {
                    newHoveredCell = playerUnit.currentCell + moveOffset;
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

            // Check facing AFTER hoveredCell updates
            Vector2I controllerOffset = hoveredCell - playerUnit.currentCell;
            AttackFacing controllerFacing = AttackPattern.GetFacing(controllerOffset, flipAttackPattern);
            bool controllerFacingChanged = controllerFacing != currentAttackFacing;

            if (controllerFacingChanged)
            {
                currentAttackFacing = controllerFacing;
                RecomputePatternCellsForHover(hoveredCell);
            }
            else if (hoveredCell != previousHoveredCell)
            {
                RecomputePatternCellsForHover(hoveredCell);
            }

            if (Input.IsActionJustPressed("combat_confirm"))
            {
                BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
                var targets = new List<Character>();
                foreach (var (enemy, enemyCell) in validEnemiesInRange)
                    if (patternCells.Contains(enemyCell))
                        targets.Add(enemy);

                if (targets.Count > 0) { StartAttackSkillCheck(targets); return; }
                else GD.Print("No valid enemies in range to attack.");
            }

            if (Input.IsActionJustPressed("flip_attack_pattern"))
            {
                flipAttackPattern = !flipAttackPattern;
                currentAttackFacing = AttackPattern.GetFacing(hoveredCell - playerUnit.currentCell, flipAttackPattern);
                RecomputePatternCellsForHover(hoveredCell);
            }
        }
        else if (inputType == InputType.KeyboardOnly)
        {
            moveOffset = Vector2I.Zero;
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

            // Check facing AFTER hoveredCell updates
            Vector2I keyboardOffset = hoveredCell - playerUnit.currentCell;
            AttackFacing keyboardFacing = AttackPattern.GetFacing(keyboardOffset, flipAttackPattern);
            bool keyboardFacingChanged = keyboardFacing != currentAttackFacing;

            if (keyboardFacingChanged)
            {
                currentAttackFacing = keyboardFacing;
                RecomputePatternCellsForHover(hoveredCell);
            }
            else if (hoveredCell != previousHoveredCell)
            {
                RecomputePatternCellsForHover(hoveredCell);
            }

            if (Input.IsActionJustPressed("combat_confirm"))
            {
                BattleManager.battleManager.combatTileMapLayer.HideHoverBrackets();
                var targets = new List<Character>();
                foreach (var (enemy, enemyCell) in validEnemiesInRange)
                    if (patternCells.Contains(enemyCell))
                        targets.Add(enemy);

                if (targets.Count > 0) { StartAttackSkillCheck(targets); return; }
                else GD.Print("No valid enemies in range to attack.");
            }

            if (Input.IsActionJustPressed("flip_attack_pattern"))
            {
                flipAttackPattern = !flipAttackPattern;
                currentAttackFacing = AttackPattern.GetFacing(hoveredCell - playerUnit.currentCell, flipAttackPattern);
                RecomputePatternCellsForHover(hoveredCell);
            }
        }

        if (Input.IsActionJustPressed("combat_redo"))
        {
            BattleManager.battleManager.HidePlayerCombatUIButtons(); // Hide the PlayerCombatUI when the player cancels their attack
            BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles(); // Clear the attack indicator tiles when cancelling the attack
            playerCombatUI?.infoUI?.HideHoveredCharacterInfoUI();
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");
        }
    }

    // Re-queries which cells can be hovered (targetCells) when facing changes, then updates pattern cells.

    // Re-queries which cells the attack pattern covers for the given hover anchor, and redraws tiles.
    private async void RecomputePatternCellsForHover(Vector2I hoveredCell)
    {
        previousHoveredCell = hoveredCell;
        BattleManager.battleManager.combatTileMapLayer.PlayHoverBracketsAt(hoveredCell);
        patternCells = BattleManager.battleManager.combatTileMapLayer.GetPatternCells(
            hoveredCell,
            playerUnit.characterStats.attackSplashPattern,
            currentAttackFacing
        );
        BattleManager.battleManager.combatTileMapLayer.ClearIndicatorTiles();
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
            playerUnit.characterStats.attackSplashPattern,
            currentAttackFacing
        );
        validEnemiesInRange = BattleManager.battleManager.GetCharactersInRangeOf(
            hoveredCell,
            playerUnit.characterStats.attackSplashPattern.minRange,
            playerUnit.characterStats.attackSplashPattern.maxRange,
            Faction.Enemy,
            playerUnit.characterStats.attackSplashPattern,
            currentAttackFacing
        );
        await UpdateHoveredCharacterInfo(hoveredCell);
    }

    private async void StartAttackSkillCheck(List<Character> targets)
    {
        var enemyNodes = new Godot.Collections.Array<Node2D>();
        foreach (var t in targets)
            enemyNodes.Add(t);
        await battleBox.BeginPlayerAttackSequence(new Godot.Collections.Array<Player> { playerUnit }, enemyNodes);

        ReapplyOpacityToAttackSkillCheckSprite();
        playerCombatUI.attackSkillCheckBackgroundSprite2D.Visible = true;
        playerCombatUI.attackSkillCheckRingSprite2D.Visible = true;
        skillCheckTargets = targets;
        skillCheckTimer = 0.0f;
        attacked = false;
        isMissedSequenceActive = false;
        isSkillCheckActive = true;
        var ring = playerCombatUI?.attackSkillCheckRingSprite2D;
        if (ring != null) { ring.Stop(); ring.Animation = "GrowingRing"; ring.Frame = 0; }
        GD.Print($"PlayerCombatAttackState: Skill check started against {targets.Count} target(s).");
    }

    private void ProcessSkillCheck(double delta)
    {
        // Input is checked first so it matches the frame the player currently sees (set last tick)
        if (Input.IsActionJustPressed("combat_confirm"))
        {
            int frame = GetCurrentSkillCheckFrame();
            if (frame < MinHitFrame)
            {
                GD.Print("PlayerCombatAttackState: Too early to confirm attack.");
            }
            else if (isMissedSequenceActive)
            {
                if (missedTimer >= MissedHitWindow)
                    GD.Print("PlayerCombatAttackState: Too late to hit during missed sequence.");
                else
                {
                    attacked = true;
                    isMissedSequenceActive = false;
                    float missedDamage = playerUnit.characterStats.attackDamage * MissedHitMultiplier;
                    foreach (Character target in skillCheckTargets)
                        target.characterStats.updateCurrentHealth(-missedDamage);
                    _ = UpdateHoveredCharacterInfo(hoveredCell, false);
                    FinishAttack(skillCheckTargets, false, MissedHitMultiplier);
                    return;
                }
            }
            else
            {
                float multiplier = GetDamageMultiplierForFrame(frame);
                attacked = true;
                float damage = playerUnit.characterStats.attackDamage * multiplier;
                foreach (Character target in skillCheckTargets)
                    target.characterStats.updateCurrentHealth(-damage);
                _ = UpdateHoveredCharacterInfo(hoveredCell, false);
                FinishAttack(skillCheckTargets, multiplier == 1.5f, multiplier);
                return;
            }
        }

        skillCheckTimer += (float)delta;

        // Only advance the animation while the skill check is still ongoing
        if (!attacked)
        {
            var ring = playerCombatUI?.attackSkillCheckRingSprite2D;
            if (ring != null)
            {
                if (ring.Animation == "GrowingRing" && !isMissedSequenceActive)
                {
                    int frameCount = ring.SpriteFrames?.GetFrameCount("GrowingRing") ?? 1;
                    ring.SetFrameAndProgress(Mathf.Min(Mathf.FloorToInt(skillCheckTimer * SkillCheckFrameRate), frameCount - 1), 0f);
                }
                else if (ring.Animation == "MissedSC" && isMissedSequenceActive)
                {
                    int frameCount = ring.SpriteFrames?.GetFrameCount("MissedSC") ?? 1;
                    ring.SetFrameAndProgress(Mathf.Min(Mathf.FloorToInt(missedTimer * SkillCheckFrameRate), frameCount - 1), 0f);
                }
            }
        }

        if (skillCheckTimer >= MaxSkillCheckDuration && !isMissedSequenceActive && !attacked)
        {
            playerCombatUI?.attackSkillCheckRingSprite2D?.Stop();
            TriggerMissedSequence();
            return;
        }

        if (isMissedSequenceActive)
        {
            missedTimer += (float)delta;
            if (missedTimer >= MissedAnimationDuration && !attacked)
                FinishAttack(skillCheckTargets, false, 0.0f, true);
        }
    }

    private void FinishAttack(List<Character> targets, bool isPerfect, float multiplier, bool isMiss = false)
    {
        if (targets == null || targets.Count == 0)
            return;

        if (isMiss)
        {
            GD.Print("PlayerCombatAttackState: Skill check missed.");
            EndAttackImmediately();
            return;
        }

        if (isPerfect)
        {
            GD.Print("PlayerCombatAttackState: Perfect attack!");
            if (playerCombatUI?.attackSkillCheckRingSprite2D?.SpriteFrames?.HasAnimation("PerfectSC") == true)
            {
                playerCombatUI.attackSkillCheckRingSprite2D.Play("PerfectSC");
                ScheduleAttackFinish(isPerfect);
            }
        }
        else if (multiplier > 0.0f)
        {
            Tween tween = playerCombatUI?.attackSkillCheckRingSprite2D?.CreateTween();
            if (tween != null)
            {
                playerCombatUI.attackSkillCheckRingSprite2D.Pause();
                tween.TweenProperty(playerCombatUI.attackSkillCheckRingSprite2D, "modulate", new Color(1, 1, 1, 0), AttackFinishDelay)
                     .SetTrans(Tween.TransitionType.Sine)
                     .SetEase(Tween.EaseType.InOut);
                ScheduleAttackFinish();
                return;
            }
        }
    }

    private void ScheduleAttackFinish(bool isPerfect = false)
    {
        if (!isPerfect)
        {
            if (isFinishScheduled)
                return;

            isFinishScheduled = true;
            var timer = GetTree().CreateTimer(AttackFinishDelay);
            timer.Timeout += () => EndAttackImmediately();
        }
        else
        {
            if (isFinishScheduled)
                return;

            isFinishScheduled = true;
            var timer = GetTree().CreateTimer(5.0f / 30.0f); // PerfectSC animation is 6 frames at 30 FPS
            timer.Timeout += () => EndPerfectAttackImmediately();
        }

    }

    private void ReapplyOpacityToAttackSkillCheckSprite()
    {
        if (playerCombatUI?.attackSkillCheckRingSprite2D != null)
        {
            playerCombatUI.attackSkillCheckRingSprite2D.Modulate = new Color(1, 1, 1, 1);
        }
    }

    private async void EndAttackImmediately()
    {
        playerUnit.hasMadeAnActionThisTurn = true;
        BattleManager.battleManager.HidePlayerCombatUIButtons();

        await Sleep(holdAfterAttackTimer);
        playerCombatUI.infoUI?.HideHoveredCharacterInfoUI();

        playerCombatUI.attackSkillCheckBackgroundSprite2D.Visible = false;
        playerCombatUI.attackSkillCheckRingSprite2D.Visible = false;

        await battleBox.EndPlayerAttackSequence();

        if (tileCombatPlayerController.remainingMoveRange <= 0)
            tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
        else
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");

        isSkillCheckActive = false;
        isMissedSequenceActive = false;
        missedTimer = 0.0f;
        isFinishScheduled = false;
        attacked = false;
        skillCheckTargets.Clear();
        playerCombatUI.attackSkillCheckRingSprite2D.Animation = "GrowingRing";
        playerCombatUI?.attackSkillCheckRingSprite2D?.Stop();
    }
    private async void EndPerfectAttackImmediately()
    {
        playerUnit.hasMadeAnActionThisTurn = true;
        playerCombatUI?.attackSkillCheckRingSprite2D?.Pause();
        await ToSignal(GetTree().CreateTimer(AttackFinishDelay - 0.2f), "timeout");
        BattleManager.battleManager.HidePlayerCombatUIButtons();

        await Sleep(holdAfterAttackTimer);
        playerCombatUI.infoUI?.HideHoveredCharacterInfoUI();

        playerCombatUI.attackSkillCheckBackgroundSprite2D.Visible = false;
        playerCombatUI.attackSkillCheckRingSprite2D.Visible = false;

        await battleBox.EndPlayerAttackSequence();

        if (tileCombatPlayerController.remainingMoveRange <= 0)
            tileCombatPlayerController.ChangeCombatState("PlayerCombatEndState");
        else
            tileCombatPlayerController.ChangeCombatState("PlayerCombatMoveState");

        isSkillCheckActive = false;
        isMissedSequenceActive = false;
        missedTimer = 0.0f;
        isFinishScheduled = false;
        attacked = false;
        skillCheckTargets.Clear();
        playerCombatUI.attackSkillCheckRingSprite2D.Animation = "GrowingRing";
        playerCombatUI?.attackSkillCheckRingSprite2D?.Stop();
    }

    private void TriggerMissedSequence()
    {
        playerUnit.hasMadeAnActionThisTurn = true; // Mark that the player has made an action this turn
        isMissedSequenceActive = true;
        missedTimer = 0.0f;
        var missedRing = playerCombatUI?.attackSkillCheckRingSprite2D;
        if (missedRing?.SpriteFrames?.HasAnimation("MissedSC") == true)
        {
            missedRing.Stop();
            missedRing.Animation = "MissedSC";
            missedRing.Frame = 0;
        }
    }

    private int GetCurrentSkillCheckFrame()
    {
        int frame = Mathf.Max(0, Mathf.FloorToInt(skillCheckTimer * SkillCheckFrameRate));
        // Cap below PerfectFrame when in missed sequence to prevent accidental perfect
        if (isMissedSequenceActive)
            return Mathf.Min(frame, PerfectFrame - 1);
        return frame;
    }

    private float GetDamageMultiplierForFrame(int frame)
    {
        if (frame < MinHitFrame)
            return 0.0f;
        if (frame > PerfectFrame && frame <= MaxHitFrame)
            return 1.0f;
        if (frame == PerfectFrame)
            return 1.5f;
        if (frame >= Frame25 && frame <= PerfectFrame - 1)
            return 1.2f;
        if (frame >= Frame20 && frame <= Frame25 - 1)
            return 1.0f;
        if (frame >= MinHitFrame && frame < Frame20)
            return 0.8f;
        return 0.0f;
    }
}
