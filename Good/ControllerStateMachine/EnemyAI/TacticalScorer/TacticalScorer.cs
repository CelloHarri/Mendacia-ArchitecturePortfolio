using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public static class TacticalScorer
{

    public static float Score(Enemy enemy, ActionCandidate candidate, EnemyAIProfile enemyAIProfile, Character lastTarget)
    {
        Godot.Collections.Dictionary<Character, Vector2I> AllPlayers = BattleManager.battleManager.GetAllPlayers();
        Godot.Collections.Dictionary<Character, Vector2I> AllEnemies = BattleManager.battleManager.GetAllEnemies();

        float score = 100f;
        var tileMap = BattleManager.battleManager.combatTileMapLayer;

        // Positioning
        int distanceTraveled = tileMap.CellDistance(enemy.currentCell, candidate.moveToCell);
        score -= distanceTraveled * (enemyAIProfile.distanceTraveledPenaltyWeight / 10f);

        int nearbyThreats = tileMap.CountOccupiedNeighbors(candidate.moveToCell, Faction.Player);
        int nearbyAllies = tileMap.CountOccupiedNeighbors(candidate.moveToCell, Faction.Enemy);
        float ownHpRatio = enemy.characterStats.currentHealth / enemy.characterStats.maxHealth;

        //selfPreservation amplifies exposuure penalty the lower the enemy's own HP is
        float exposureScale = 1f + (1f - ownHpRatio) * (enemyAIProfile.selfPreservationWeight / 100f);
        score -= nearbyThreats * (enemyAIProfile.exposurePenaltyWeight / 10f) * exposureScale;
        score += nearbyAllies * (enemyAIProfile.clusterWithAlliesWeight / 10f);

        //Action Specific Scoring
        if (candidate.action == AIAction.Attack && candidate.target != null)
        {
            Character target = candidate.target;
            float targetHpRatio = target.characterStats.currentHealth / target.characterStats.maxHealth;
            float maxDefense = 1f;
            foreach (var player in AllPlayers.Keys)
                maxDefense = Mathf.Max(maxDefense, player.characterStats.defense);
            float targetDefRatio = Mathf.Clamp(target.characterStats.defense / maxDefense, 0f, 1f);
            score += (1f - targetHpRatio) * enemyAIProfile.lowHpPlayerWeight;
            score += targetHpRatio * enemyAIProfile.highHpPlayerWeight;
            score += (1f - targetDefRatio) * enemyAIProfile.lowDefPlayerWeight;
            score += targetDefRatio * enemyAIProfile.highDefPlayerWeight;

            score += target == lastTarget ? enemyAIProfile.samePlayerStickinessWeight : enemyAIProfile.differentPlayerSwitchWeight;

            score += candidate.targetsInArea == 1 ? enemyAIProfile.singlePlayerWeight : 0f;
            score += candidate.targetsInArea / 6f * enemyAIProfile.multiplePlayersWeight; // 6 = practical max AoE targets

            int distToTarget = tileMap.CellDistance(candidate.moveToCell, target.currentCell);
            score -= Mathf.Abs(distToTarget - enemyAIProfile.preferredRange) * (enemyAIProfile.rangePenaltyWeight / 10f);
        }
        else if (candidate.action == AIAction.Heal && candidate.target != null)
        {
            Character ally = candidate.target;
            float targetHpRatio = ally.characterStats.currentHealth / ally.characterStats.maxHealth;

            score += (1f - targetHpRatio) * enemyAIProfile.lowHpAllyWeight;
            score += targetHpRatio * enemyAIProfile.highHpAllyWeight;

            score += candidate.targetsInArea == 1 ? enemyAIProfile.singleAllyWeight : 0f;
            score += candidate.targetsInArea / 6f * enemyAIProfile.multipleAlliesWeight;
        }
        else if (candidate.action == AIAction.Reposition)
        {
            float minPlayerDistance = float.MaxValue;
            foreach (var (_, playerCell) in AllPlayers)
            {
                int dist = tileMap.CellDistance(candidate.moveToCell, playerCell);
                minPlayerDistance = Mathf.Min(minPlayerDistance, dist);
            }
            score += minPlayerDistance / 10f * enemyAIProfile.runAwayWeight;

            // Reward tiles closed toward the nearest player this turn, not raw proximity.
            // Staying put earns 0; moving 3 tiles closer earns 3 × weight/10.
            int currentMinDist = int.MaxValue;
            foreach (var (_, playerCell) in AllPlayers)
                currentMinDist = Mathf.Min(currentMinDist, tileMap.CellDistance(enemy.currentCell, playerCell));
            int tilesClosed = currentMinDist - (int)minPlayerDistance;
            score += Mathf.Max(0, tilesClosed) * (enemyAIProfile.approachPlayerWeight / 10f);

            // When hurt past the flee threshold, reward cells closer to a healer ally.
            bool isFleeing = ownHpRatio * 100f < enemyAIProfile.fleeForHealthThreshold;
            if (isFleeing)
            {
                float minHealerDistance = float.MaxValue;
                foreach (var ally in AllEnemies.Keys)
                {
                    if (!ally.characterStats.isHealer) continue;
                    int dist = tileMap.CellDistance(candidate.moveToCell, ally.currentCell);
                    minHealerDistance = Mathf.Min(minHealerDistance, dist);
                }
                if (minHealerDistance < float.MaxValue)
                    score += (1f - Mathf.Clamp(minHealerDistance / 10f, 0f, 1f)) * enemyAIProfile.seekHealingWeight;
            }
        }

        return score;
    }


    /// <summary>
    /// Picks the best-scoring candidate after applying per-candidate random noise.
    /// randomness == 0 always picks the highest score; larger values make the enemy
    /// occasionally choose a merely-good option over the optimal one.
    /// </summary>
    public static ActionCandidate PickWithRandomness(
        List<ActionCandidate> candidates, float randomness, Random rng)
    {
        ActionCandidate best = null;
        float bestAdjusted = float.NegativeInfinity;

        foreach (var c in candidates)
        {
            // Noise is uniformly distributed in [-randomness, +randomness].
            float noise = ((float)rng.NextDouble() * 2f - 1f) * randomness;
            float adjusted = c.score + noise;
            if (adjusted > bestAdjusted)
            {
                bestAdjusted = adjusted;
                best = c;
            }
        }

        return best;
    }
}
