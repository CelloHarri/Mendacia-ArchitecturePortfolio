using Godot;
using System;
using System.Threading.Tasks;


// Builds all move+attack candidates, scores them, picks one (with randomness), then
[GlobalClass]
public partial class EvaluateState : TileCombatAIState
{
    [Export] private bool debugPrintCandidates = false;

    public System.Collections.Generic.List<ActionCandidate> candidates = new System.Collections.Generic.List<ActionCandidate>();

    private static readonly Random rng = new Random();

    public async override Task OnEnterState()
    {
        BattleManager.battleManager.combatTileMapLayer.DrawIndicatorTiles(stateMachine.reachableCells, stateMachine.tileSourceId, stateMachine.atlasCoords);
        combatUI?.infoUI?.ShowCurrentCharacterInfoUI(unit);

        await Sleep(0.7f);

        await base.OnEnterState();

        BuildCandidates();

        int attackCount = candidates.FindAll(c => c.action == AIAction.Attack).Count;
        int repoCount = candidates.FindAll(c => c.action == AIAction.Reposition).Count;
        if (debugPrintCandidates)
            GD.Print($"[EvaluateState] {unit.characterStats.characterName} | reachableCells={stateMachine.reachableCells.Count} | candidates={candidates.Count} (attack={attackCount} reposition={repoCount})");

        if (candidates.Count == 0)
        {
            stateMachine.ChangeAIState("EndTurnState");
            return;
        }

        var enemy = unit as Enemy;
        foreach (var candidate in candidates)
            candidate.score = TacticalScorer.Score(enemy, candidate, enemy.enemyAIProfile, stateMachine.lastTarget);

        stateMachine.chosenCandidate = TacticalScorer.PickWithRandomness(candidates, enemy.enemyAIProfile.aiRandomness, rng);

        if (debugPrintCandidates)
        {
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            GD.Print($"[EvaluateState] Top candidates:");
            for (int i = 0; i < Mathf.Min(5, candidates.Count); i++)
            {
                var c = candidates[i];
                GD.Print($"  [{i}] {c.action} moveToCell={c.moveToCell} target={c.target?.characterStats?.characterName ?? "none"} score={c.score:F1}");
            }
            GD.Print($"[EvaluateState] Chosen: {stateMachine.chosenCandidate.action} moveToCell={stateMachine.chosenCandidate.moveToCell} score={stateMachine.chosenCandidate.score:F1}");
        }

        stateMachine.ChangeAIState("MoveState");
    }

    private void BuildCandidates()
    {
        candidates.Clear();
        var pattern = unit.characterStats.attackSplashPattern;
        bool isCustom = pattern.shape == AttackShape.Custom;

        foreach (var moveToCell in stateMachine.reachableCells)
        {
            // Attackable cells don't depend on facing for non-Custom shapes (they're symmetric).
            var attackableCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
                    moveToCell,
                    unit.characterStats.attackRangePattern.maxRange,
                    Faction.Enemy,
                    minRange: unit.characterStats.attackRangePattern.minRange);
            bool addedAnyAttack = false;

            foreach (var anchorCell in attackableCells)
            {
                Vector2I offset = anchorCell - moveToCell;
                int absX = Mathf.Abs(offset.X), absY = Mathf.Abs(offset.Y);

                // Diagonal offsets are ambiguous for Custom patterns — check both facings.
                AttackFacing facing1 = AttackPattern.GetFacing(offset, false);
                AttackFacing? facing2 = (isCustom && absX == absY && offset != Vector2I.Zero)
                    ? AttackPattern.GetFacing(offset, true)
                    : null;

                addedAnyAttack |= TryAddAttackCandidates(moveToCell, anchorCell, facing1, pattern);
                if (facing2.HasValue && facing2.Value != facing1)
                    addedAnyAttack |= TryAddAttackCandidates(moveToCell, anchorCell, facing2.Value, pattern);
            }

            // Heal candidates — only generated if this unit has a heal range configured.
            if (unit.characterStats.healRangePattern.maxRange > 0)
            {
                var healPattern = unit.characterStats.healSplashPattern;
                bool isHealCustom = healPattern.shape == AttackShape.Custom;
                var healableCells = BattleManager.battleManager.combatTileMapLayer.GetReachableCells(
                    moveToCell,
                    unit.characterStats.healRangePattern.maxRange,
                    Faction.Enemy,
                    minRange: unit.characterStats.healRangePattern.minRange);

                foreach (var anchorCell in healableCells)
                {
                    Vector2I offset = anchorCell - moveToCell;
                    int absX = Mathf.Abs(offset.X), absY = Mathf.Abs(offset.Y);

                    AttackFacing facing1 = AttackPattern.GetFacing(offset, false);
                    AttackFacing? facing2 = (isHealCustom && absX == absY && offset != Vector2I.Zero)
                        ? AttackPattern.GetFacing(offset, true)
                        : null;

                    TryAddHealCandidates(moveToCell, anchorCell, facing1, healPattern);
                    if (facing2.HasValue && facing2.Value != facing1)
                        TryAddHealCandidates(moveToCell, anchorCell, facing2.Value, healPattern);
                }
            }

            if (!addedAnyAttack)
            {
                candidates.Add(new ActionCandidate
                {
                    action = AIAction.Reposition,
                    moveToCell = moveToCell,
                    target = null,
                    score = 0f
                });
            }
        }
    }

    // Returns true if at least one player was hit, adding one candidate per player as primary target.
    private bool TryAddAttackCandidates(Vector2I moveToCell, Vector2I anchorCell, AttackFacing facing, AttackPattern pattern)
    {
        var playersHit = BattleManager.battleManager.GetCharactersInRangeOf(
            anchorCell,
            unit.characterStats.attackSplashPattern.minRange,
            unit.characterStats.attackSplashPattern.maxRange,
            Faction.Player,
            pattern,
            facing);

        if (playersHit.Count == 0) return false;

        foreach (var primaryTarget in playersHit.Keys)
        {
            candidates.Add(new ActionCandidate
            {
                action = AIAction.Attack,
                moveToCell = moveToCell,
                attackAnchorCell = anchorCell,
                facing = facing,
                target = primaryTarget,
                targetsInArea = playersHit.Count,
                score = 0f
            });
        }
        return true;
    }

    // Returns true if at least one injured ally was found, adding one candidate per ally as primary target.
    private bool TryAddHealCandidates(Vector2I moveToCell, Vector2I anchorCell, AttackFacing facing, AttackPattern healPattern)
    {
        var alliesHit = BattleManager.battleManager.GetCharactersInRangeOf(
            anchorCell,
            unit.characterStats.healSplashPattern.minRange,
            unit.characterStats.healSplashPattern.maxRange,
            Faction.Enemy,
            healPattern,
            facing);

        bool anyInjured = false;
        foreach (var ally in alliesHit.Keys)
        {
            if (ally.characterStats.currentHealth >= ally.characterStats.maxHealth) continue;
            anyInjured = true;
            candidates.Add(new ActionCandidate
            {
                action = AIAction.Heal,
                moveToCell = moveToCell,
                attackAnchorCell = anchorCell,
                facing = facing,
                target = ally,
                targetsInArea = alliesHit.Count,
                score = 0f
            });
        }
        return anyInjured;
    }
}
