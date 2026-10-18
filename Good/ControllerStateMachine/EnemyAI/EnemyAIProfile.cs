using Godot;

[Tool]
[GlobalClass]
public partial class EnemyAIProfile : Resource
{
    [ExportGroup("Target Selection")]
    /// <summary>
    /// Higher = Attack low HP Players more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float lowHpPlayerWeight = 0f;
    /// <summary>
    /// Higher = Attack high HP Players more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float highHpPlayerWeight = 0f;
    /// <summary>
    /// Higher = Attack when attackArea can hit only one player
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float singlePlayerWeight = 0f;
    /// <summary>
    /// Higher = Attack when attackArea can hit more players
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float multiplePlayersWeight = 0f;
    /// <summary>
    /// Higher = Attack low DEF Players more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float lowDefPlayerWeight = 0f;
    /// <summary>
    /// Higher = Attack high DEF Players more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float highDefPlayerWeight = 0f;
    /// <summary>
    /// Higher = Stick to the same Player more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float samePlayerStickinessWeight = 0f;
    /// <summary>
    /// Higher = Switch to a different Player more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float differentPlayerSwitchWeight = 0f;
    /// <summary>
    /// Higher = Heal low HP allies more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float lowHpAllyWeight = 0f;
    /// <summary>
    /// Higher = Heal high HP allies more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float highHpAllyWeight = 0f;
    /// <summary>
    /// Higher = Heal when healArea can hit only one ally
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float singleAllyWeight = 0f;
    /// <summary>
    /// Higher = Heal when healArea can hit more allies
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float multipleAlliesWeight = 0f;
    /// <summary>
    /// Higher = Run away more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float runAwayWeight = 0f;

    [Export(PropertyHint.Range, "0,100,1")] public float approachPlayerWeight = 0f; // Higher = Move toward the nearest player when no attack is in range

    [ExportGroup("Positioning")]
    [Export] public bool moveAfterAction = false;
    /// <summary>
    /// Higher = Move away after attacking more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float fallBackAfterActionWeight = 0f;
    /// <summary>
    /// Higher = Move in after attacking more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float pushInAfterActionWeight = 0f;
    /// <summary>
    /// The preferred distance from the target
    /// </summary>
    [Export] public int preferredRange = 1;
    /// <summary>
    /// Higher = Penalize being away from the preferred range more aggressively
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float rangePenaltyWeight = 0f;
    /// <summary>
    /// Higher = Penalize moving more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float distanceTraveledPenaltyWeight = 0f;
    /// <summary>
    /// Higher = Penalize being exposed to multiple Players more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float exposurePenaltyWeight = 0f;
    /// <summary>
    /// Higher = Prefer to cluster with allies more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float clusterWithAlliesWeight = 0f;

    [ExportGroup("Risk Tolerance")]
    /// <summary>
    /// Higher = Avoid taking damage more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float selfPreservationWeight = 0f;
    /// <summary>
    /// Flee for health if health is below this percentage (0-100)
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float fleeForHealthThreshold = 0f;
    /// <summary>
    /// Higher = Seek healing more often
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float seekHealingWeight = 0f;

    [ExportGroup("Randomness")]
    /// <summary>
    /// Higher = More randomness in decision-making
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")] public float aiRandomness = 0f;
}