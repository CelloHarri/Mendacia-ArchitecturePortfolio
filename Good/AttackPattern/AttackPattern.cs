using Godot.Collections;
using Godot;


public enum AttackShape
{
    Cross,    // only cells directly along a cardinal axis (no diagonals ever count)
    Diamond,  // any cells within Manhattan distance (your current behavior)
    Square,   // any cells within Chebyshev distance (true 8-directional diagonals included)
    Custom    // explicit offset list, for weird/unique attack patterns
}

// The direction a Custom attack pattern is currently facing, relative to the cell the
// pattern's offsets were originally authored for (PosX = 0 degrees, unrotated).
public enum AttackFacing
{
    PosX, // 0 degrees
    PosY, // 90 degrees
    NegX, // 180 degrees
    NegY  // 270 degrees
}

[Tool]
[GlobalClass]
public partial class AttackPattern : Resource
{
    [Export] public AttackShape shape;
    [Export] public Array<Vector2I> customOffsets; // only used when shape == Custom
    [Export] public int minRange = 1;
    [Export] public int maxRange = 1;

    public bool IsValidOffset(Vector2I offset, int minRange, int maxRange, AttackFacing facing = AttackFacing.PosX)
    {
        switch (shape)
        {
            case AttackShape.Cross:
                // Only counts if it's purely horizontal or purely vertical from origin.
                if (offset.X != 0 && offset.Y != 0)
                    return false;
                int crossDist = Mathf.Abs(offset.X) + Mathf.Abs(offset.Y);
                return crossDist >= minRange && crossDist <= maxRange;

            case AttackShape.Diamond:
                int manhattanDist = Mathf.Abs(offset.X) + Mathf.Abs(offset.Y);
                return manhattanDist >= minRange && manhattanDist <= maxRange;

            case AttackShape.Square:
                int chebyshevDist = Mathf.Max(Mathf.Abs(offset.X), Mathf.Abs(offset.Y));
                return chebyshevDist >= minRange && chebyshevDist <= maxRange;

            case AttackShape.Custom:
                foreach (Vector2I baseOffset in customOffsets)
                {
                    if (RotateOffset(baseOffset, facing) == offset)
                        return true;
                }
                return false;

            default:
                return false;
        }
    }

    // Rotates a Custom-shape offset (authored assuming AttackFacing.PosX / 0 degrees) so it
    // points toward the given facing instead. PosX->0deg, PosY->90deg, NegX->180deg, NegY->270deg.
    public static Vector2I RotateOffset(Vector2I offset, AttackFacing facing)
    {
        return facing switch
        {
            AttackFacing.PosX => offset,
            AttackFacing.PosY => new Vector2I(-offset.Y, offset.X),
            AttackFacing.NegX => new Vector2I(-offset.X, -offset.Y),
            AttackFacing.NegY => new Vector2I(offset.Y, -offset.X),
            _ => offset
        };
    }

    // The on-screen rotation (degrees, clockwise) that corresponds to a given facing. Matches
    // RotateOffset's rotation sense, so rotating a bracket sprite by this amount visually agrees
    // with where its repositioned offset ends up.

    // Determines which cardinal direction `offset` (target cell - origin cell) mostly points in.
    // When offset lies exactly on a diagonal (|X| == |Y|, including the zero offset), the choice
    // between the two equally-valid axes is ambiguous, so `flipped` picks between them
    // (false = the positive/negative X facing, true = the positive/negative Y facing).
    public static AttackFacing GetFacing(Vector2I offset, bool flipped = false)
    {
        int absX = Mathf.Abs(offset.X);
        int absY = Mathf.Abs(offset.Y);

        if (absX == absY)
            return flipped
                ? (offset.Y >= 0 ? AttackFacing.PosY : AttackFacing.NegY)
                : (offset.X >= 0 ? AttackFacing.PosX : AttackFacing.NegX);

        // The axis with the larger change from the origin determines the facing.
        if (absX > absY)
            return offset.X >= 0 ? AttackFacing.PosX : AttackFacing.NegX;
        else
            return offset.Y >= 0 ? AttackFacing.PosY : AttackFacing.NegY;
    }
}