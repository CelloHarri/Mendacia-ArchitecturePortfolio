using Godot;
public enum AIAction //Possible more like Buff, Debuff, etc. in the future
{
    Reposition,
    Attack,
    Heal
}

public partial class ActionCandidate : Node
{
    public AIAction action;
    public Vector2I moveToCell;
    public Vector2I attackAnchorCell; // the cell the attack pattern is centered on
    public AttackFacing facing;
    public Character target; // null = reposition only, no attack this turn
    public int targetsInArea; // how many players are hit from attackAnchorCell with this facing
    public float score;
}
