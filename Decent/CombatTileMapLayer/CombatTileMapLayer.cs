using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class CombatTileMapLayer : TileMapLayer
{
    // Custom data layer names expected on the TileSet (set these in the TileSet editor):
    //  - "Walkable"      (bool)
    //  - "MovementCost"  (int)
    private const string WalkableDataName = "Walkable";
    private const string MovementCostDataName = "MovementCost";

    [Export] public AnimatedSprite2D leftHoverBracket;
    [Export] public AnimatedSprite2D rightHoverBracket;
    [Export] public AnimatedSprite2D topHoverBracket;
    [Export] public AnimatedSprite2D bottomHoverBracket;

    [Export] public Sprite2D NorthEastBorder;
    [Export] public Sprite2D NorthWestBorder;
    [Export] public Sprite2D SouthEastBorder;
    [Export] public Sprite2D SouthWestBorder;

    public List<Sprite2D> NorthEastBorders = new List<Sprite2D>();
    public List<Sprite2D> NorthWestBorders = new List<Sprite2D>();
    public List<Sprite2D> SouthEastBorders = new List<Sprite2D>();
    public List<Sprite2D> SouthWestBorders = new List<Sprite2D>();

    // A separate TileMapLayer stacked on top of this one, using its own TileSet (e.g.
    // WalkableIndicaterTileSet.tres) to draw movement-range highlights without touching the
    // underlying floor tiles. Assign the sibling "IndicatorTileMapLayer" node in the editor.
    [Export] public TileMapLayer indicatorLayer;
    [Export] public TileMapLayer actionIndicatorLayer;

    // Optional layer whose tiles override the base walkability of any cell they occupy.
    // Each tile must have a "Walkable" bool custom-data layer on its TileSet:
    //   true  → force walkable even if the floor tile beneath is not (e.g. a bridge)
    //   false → force solid   even if the floor tile beneath is walkable (e.g. a rock)
    [Export] public TileMapLayer objectLayer;

    private static readonly Vector2I[] neighborDirections =
    {
        new Vector2I(1, 0), new Vector2I(-1, 0),
        new Vector2I(0, 1), new Vector2I(0, -1)
    };

    private AStarGrid2D aStarGrid2D = new AStarGrid2D();
    // Ally cells (Player + NonControlledAlly) can be walked through but not stopped on.
    // Enemy cells are completely impassable.
    private HashSet<Vector2I> allyOccupiedCells = new HashSet<Vector2I>();
    private HashSet<Vector2I> enemyOccupiedCells = new HashSet<Vector2I>();
    // Objective cells block all movement — no faction can pass through or stop on them.
    private HashSet<Vector2I> objectiveOccupiedCells = new HashSet<Vector2I>();
    // All Objective nodes currently registered in this battle.
    public List<Objective> objectives = new List<Objective>();



    public override async void _Ready()
    {
        // Ensure our _Process runs *after* the AnimatedSprite2D nodes have advanced their frame
        // for this tick (they use the default process priority of 0). Without this, the clones'
        // frame is copied from the original's frame *before* it updates, giving a permanent 1-frame lag.
        ProcessPriority = 4096;

        BattleManager.battleManager.combatTileMapLayer = this;

        // Clear any tiles left over from editor painting before the battle starts.
        if (indicatorLayer != null)
        {
            indicatorLayer.Clear();
            indicatorLayer.Modulate = new Color(1, 1, 1, 0.5f); // 50% opacity so floor texture shows through
        }
        GD.Print("Waiting 1 second before starting the battle to avoid bugs");
        await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout); // Wait for 1 second before starting the battle to ensure all characters are ready
        GD.Print("Battle is Active");
        SetupAstarGrid(); // Must run before StartBattle(), which immediately queries reachable cells.
        BattleManager.battleManager.StartBattle();

        if (leftHoverBracket != null) leftHoverBracket.Play("default");
        if (rightHoverBracket != null) rightHoverBracket.Play("default");
        if (topHoverBracket != null) topHoverBracket.Play("default");
        if (bottomHoverBracket != null) bottomHoverBracket.Play("default");

        AddBorders();
        indicatorLayer.ZAsRelative = false;
    }

    public override void _ExitTree()
    {
        if (BattleManager.battleManager != null)
        {
            BattleManager.battleManager.combatTileMapLayer = null;
        }

        GD.Print("Battle is Inactive");
    }


    // Builds the pathfinding grid from the tiles currently painted on this layer,
    // reading walkability/cost from each tile's custom data.
    private void SetupAstarGrid()
    {
        Rect2I usedRect = GetUsedRect();

        aStarGrid2D.Region = usedRect;
        aStarGrid2D.CellSize = Vector2I.One; // Use unit size so heuristic distances are equal in all directions
        aStarGrid2D.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        aStarGrid2D.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Manhattan;
        aStarGrid2D.DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Manhattan;
        aStarGrid2D.Update();

        // Block any empty cells inside the used rect, otherwise GetUsedRect()'s bounding box can
        // allow movement across empty space between tiles.
        for (int x = usedRect.Position.X; x < usedRect.End.X; x++)
        {
            for (int y = usedRect.Position.Y; y < usedRect.End.Y; y++)
            {
                aStarGrid2D.SetPointSolid(new Vector2I(x, y), true);
            }
        }

        foreach (Vector2I cell in GetUsedCells())
        {
            TileData tileData = GetCellTileData(cell);
            bool walkable = tileData != null && (bool)tileData.GetCustomData(WalkableDataName);

            // Object layer can override walkability: a tile present here takes priority.
            if (objectLayer != null)
            {
                TileData objData = objectLayer.GetCellTileData(cell);
                if (objData != null)
                    walkable = (bool)objData.GetCustomData(WalkableDataName);
            }

            aStarGrid2D.SetPointSolid(cell, !walkable);

            if (walkable)
            {
                aStarGrid2D.SetPointWeightScale(cell, GetMovementCost(cell));
            }
        }
    }

    // Returns the ordered list of cells from startCell to endCell, or an empty list if no path exists.
    // Enemy-occupied cells are temporarily marked solid so the path cannot route through them.
    // Ally-occupied cells remain passable (can walk through but not stop on).
    // Returns the ordered list of cells from startCell to endCell.
    // movingFaction determines which occupied cells are treated as impassable:
    // a Player-faction mover is blocked by enemyOccupiedCells; an Enemy-faction
    // mover is blocked by allyOccupiedCells. Units can pass through same-faction cells.
    // allowedCells: when provided, the path is constrained to only travel through those cells
    // (i.e. cells outside the set are treated as impassable for this query).
    public List<Vector2I> GetPath(Vector2I startCell, Vector2I endCell,
    Faction movingFaction = Faction.Player, HashSet<Vector2I> allowedCells = null)
    {
        HashSet<Vector2I> hostileCells = movingFaction == Faction.Enemy
            ? allyOccupiedCells : enemyOccupiedCells;

        var tempSolid = new List<Vector2I>();
        var tempWeightReset = new List<Vector2I>(); // cells we temporarily set to weight 1

        foreach (Vector2I cell in hostileCells)
        {
            if (!aStarGrid2D.IsPointSolid(cell))
            {
                aStarGrid2D.SetPointSolid(cell, true);
                tempSolid.Add(cell);
            }
        }

        foreach (Vector2I cell in objectiveOccupiedCells)
        {
            if (!aStarGrid2D.IsPointSolid(cell))
            {
                aStarGrid2D.SetPointSolid(cell, true);
                tempSolid.Add(cell);
            }
        }

        // Friendly cells: make sure they're passable AND not penalized by weight
        // so AStar routes through them rather than around them
        HashSet<Vector2I> friendlyCells = movingFaction == Faction.Enemy
            ? enemyOccupiedCells : allyOccupiedCells;
        foreach (Vector2I cell in friendlyCells)
        {
            if (!aStarGrid2D.IsPointSolid(cell))
            {
                // Reset weight to 1 so pathfinding doesn't avoid them due to high movement cost
                float existingWeight = aStarGrid2D.GetPointWeightScale(cell);
                if (existingWeight != 1f)
                {
                    aStarGrid2D.SetPointWeightScale(cell, 1f);
                    tempWeightReset.Add(cell);
                }
            }
        }

        if (allowedCells != null)
        {
            foreach (Vector2I cell in GetUsedCells())
            {
                if (!allowedCells.Contains(cell) && !aStarGrid2D.IsPointSolid(cell))
                {
                    aStarGrid2D.SetPointSolid(cell, true);
                    tempSolid.Add(cell);
                }
            }
        }

        Godot.Collections.Array<Vector2I> idPath = aStarGrid2D.GetIdPath(startCell, endCell);

        // Restore everything
        foreach (Vector2I cell in tempSolid)
            aStarGrid2D.SetPointSolid(cell, false);
        foreach (Vector2I cell in tempWeightReset)
            aStarGrid2D.SetPointWeightScale(cell, GetMovementCost(cell));

        return new List<Vector2I>(idPath);
    }

    // Flood-fills outward from startCell, returning every cell reachable within moveRange movement cost.
    // movingFaction determines which occupied cells block movement vs allow pass-through.
    // Returns all cells reachable from origin within maxRange.
    // useMovementCost=true: tile-weighted BFS for movement (hostile cells block, friendlies are pass-through).
    // useMovementCost=false: plain distance flood-fill for attack/act/item/spell targeting.
    // When pattern is provided (non-movement only), only cells valid for that shape are returned.
    public HashSet<Vector2I> GetReachableCells(
        Vector2I origin, int maxRange, Faction faction = Faction.Player,
        int minRange = 0, bool useMovementCost = false,
        AttackPattern pattern = null, AttackFacing facing = AttackFacing.PosX)
    {
        if (useMovementCost)
        {
            HashSet<Vector2I> hostileCells = faction == Faction.Enemy ? allyOccupiedCells : enemyOccupiedCells;
            HashSet<Vector2I> friendlyCells = faction == Faction.Enemy ? enemyOccupiedCells : allyOccupiedCells;

            var reachable = new HashSet<Vector2I> { origin };
            var costSoFar = new System.Collections.Generic.Dictionary<Vector2I, int> { { origin, 0 } };
            var frontier = new Queue<Vector2I>();
            frontier.Enqueue(origin);

            while (frontier.Count > 0)
            {
                Vector2I current = frontier.Dequeue();
                int currentCost = costSoFar[current];

                foreach (Vector2I direction in neighborDirections)
                {
                    Vector2I neighbor = current + direction;

                    // Hostile faction cells fully block traversal; friendly cells can be passed through but not stopped on.
                    // Objective cells block all movement regardless of faction.
                    if (!aStarGrid2D.IsInBoundsv(neighbor) || aStarGrid2D.IsPointSolid(neighbor) || hostileCells.Contains(neighbor) || objectiveOccupiedCells.Contains(neighbor))
                        continue;

                    int neighborCost = currentCost + GetMovementCost(neighbor);
                    if (neighborCost > maxRange)
                        continue;

                    if (!costSoFar.ContainsKey(neighbor) || neighborCost < costSoFar[neighbor])
                    {
                        costSoFar[neighbor] = neighborCost;
                        if (!friendlyCells.Contains(neighbor))
                            reachable.Add(neighbor);
                        frontier.Enqueue(neighbor);
                    }
                }
            }
            return reachable;
        }
        else
        {
            if (pattern != null && pattern.shape == AttackShape.Custom)
            {
                var result = new HashSet<Vector2I>();
                foreach (Vector2I baseOffset in pattern.customOffsets)
                {
                    Vector2I cell = origin + AttackPattern.RotateOffset(baseOffset, facing);
                    if (aStarGrid2D.IsInBoundsv(cell) && !aStarGrid2D.IsPointSolid(cell))
                        result.Add(cell);
                }
                return result;
            }

            var reachable = new HashSet<Vector2I>();
            var visited = new HashSet<Vector2I> { origin };
            var frontier = new Queue<(Vector2I cell, int distance)>();
            frontier.Enqueue((origin, 0));

            if (minRange == 0)
                reachable.Add(origin);

            while (frontier.Count > 0)
            {
                var (current, distance) = frontier.Dequeue();

                if (distance > 0)
                {
                    Vector2I offset = current - origin;
                    bool valid = pattern != null
                        ? pattern.IsValidOffset(offset, minRange, maxRange, facing)
                        : distance >= minRange;
                    if (valid)
                        reachable.Add(current);
                }

                if (distance >= maxRange)
                    continue;

                foreach (Vector2I direction in neighborDirections)
                {
                    Vector2I neighbor = current + direction;

                    if (!aStarGrid2D.IsInBoundsv(neighbor) || visited.Contains(neighbor))
                        continue;
                    if (aStarGrid2D.IsPointSolid(neighbor))
                        continue;
                    visited.Add(neighbor);
                    if (objectiveOccupiedCells.Contains(neighbor))
                        continue;
                    frontier.Enqueue((neighbor, distance + 1));
                }
            }
            return reachable;
        }
    }

    // Returns the cells covered by pattern applied at origin.
    // If pattern is null, returns only origin itself (single-cell hit).
    public HashSet<Vector2I> GetPatternCells(
        Vector2I origin, AttackPattern pattern = null, AttackFacing facing = AttackFacing.PosX)
    {
        if (pattern == null)
            return new HashSet<Vector2I> { origin };

        if (pattern.shape == AttackShape.Custom)
        {
            var result = new HashSet<Vector2I>();
            foreach (Vector2I baseOffset in pattern.customOffsets)
            {
                Vector2I cell = origin + AttackPattern.RotateOffset(baseOffset, facing);
                if (aStarGrid2D.IsInBoundsv(cell) && !aStarGrid2D.IsPointSolid(cell))
                    result.Add(cell);
            }
            return result;
        }

        var attackable = new HashSet<Vector2I>();
        var visited = new HashSet<Vector2I> { origin };
        var frontier = new Queue<(Vector2I cell, int distance)>();
        frontier.Enqueue((origin, 0));

        if (pattern.minRange == 0)
            attackable.Add(origin);

        while (frontier.Count > 0)
        {
            var (current, distance) = frontier.Dequeue();

            if (distance > 0)
            {
                Vector2I offset = current - origin;
                if (pattern.IsValidOffset(offset, pattern.minRange, pattern.maxRange, facing))
                    attackable.Add(current);
            }

            if (distance >= pattern.maxRange)
                continue;

            foreach (Vector2I direction in neighborDirections)
            {
                Vector2I neighbor = current + direction;
                if (!aStarGrid2D.IsInBoundsv(neighbor) || visited.Contains(neighbor))
                    continue;
                if (aStarGrid2D.IsPointSolid(neighbor))
                    continue;
                visited.Add(neighbor);
                if (objectiveOccupiedCells.Contains(neighbor))
                    continue;
                frontier.Enqueue((neighbor, distance + 1));
            }
        }
        return attackable;
    }

    public int GetMovementCost(Vector2I cell)
    {
        TileData tileData = GetCellTileData(cell);

        if (tileData == null)
        {
            return 1;
        }

        Variant cost = tileData.GetCustomData(MovementCostDataName);
        return cost.VariantType == Variant.Type.Nil ? 1 : (int)cost;
    }

    public void SetCellOccupied(Vector2I cell, bool occupied, Faction faction)
    {
        HashSet<Vector2I> targetSet = faction == Faction.Enemy ? enemyOccupiedCells : allyOccupiedCells;
        if (occupied)
            targetSet.Add(cell);
        else
            targetSet.Remove(cell);
    }

    public bool IsCellOccupied(Vector2I cell) => allyOccupiedCells.Contains(cell) || enemyOccupiedCells.Contains(cell) || objectiveOccupiedCells.Contains(cell);

    public void RegisterObjective(Objective objective)
    {
        if (!objectives.Contains(objective))
            objectives.Add(objective);
        objectiveOccupiedCells.Add(objective.currentCell);
    }

    public void UnregisterObjective(Objective objective)
    {
        objectives.Remove(objective);
        objectiveOccupiedCells.Remove(objective.currentCell);
    }

    public bool IsObjectiveCell(Vector2I cell) => objectiveOccupiedCells.Contains(cell);

    // Manhattan distance between two grid cells (matches the 4-directional movement grid).
    public int CellDistance(Vector2I a, Vector2I b)
        => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

    // Returns how many immediate neighbours of `cell` are occupied by units of the given faction.
    public int CountOccupiedNeighbors(Vector2I cell, Faction faction)
    {
        int count = 0;
        HashSet<Vector2I> targetSet = faction == Faction.Enemy ? enemyOccupiedCells : allyOccupiedCells;
        foreach (Vector2I dir in neighborDirections)
            if (targetSet.Contains(cell + dir))
                count++;
        return count;
    }

    public Vector2I WorldToCell(Vector2 worldPosition) => LocalToMap(ToLocal(worldPosition));

    public Vector2 CellToWorld(Vector2I cell) => ToGlobal(MapToLocal(cell));

    // Paints a movement-indicator tile on the dedicated indicator layer (not on this layer's own TileSet).
    public void SetIndicatorCell(Vector2I cell, int sourceId, Vector2I atlasCoords)
    {
        if (indicatorLayer == null)
        {
            GD.PrintErr("SetIndicatorCell: indicatorLayer is null — assign it in the Inspector on TileMapLayerMovement.");
            return;
        }
        if (indicatorLayer.TileSet == null)
        {
            GD.PrintErr("SetIndicatorCell: indicatorLayer has no TileSet assigned.");
            return;
        }
        if (indicatorLayer.TileSet.GetSource(sourceId) == null)
        {
            GD.PrintErr($"SetIndicatorCell: sourceId {sourceId} does not exist in the indicator layer's TileSet.");
            return;
        }
        indicatorLayer.SetCell(cell, sourceId, atlasCoords);
    }

    public void DrawIndicatorTiles(HashSet<Vector2I> cells, int sourceId, Vector2I atlasCoords, TileMapLayer targetLayer = null)
    {

        if (targetLayer == null)
        {
            targetLayer = indicatorLayer;
        }

        if (targetLayer == null)
        {
            GD.PrintErr("DrawIndicatorTiles: targetLayer is null — assign it in the Inspector on TileMapLayerMovement.");
            return;
        }
        if (targetLayer.TileSet == null)
        {
            GD.PrintErr("DrawIndicatorTiles: targetLayer has no TileSet assigned.");
            return;
        }
        if (targetLayer.TileSet.GetSource(sourceId) == null)
        {
            GD.PrintErr($"DrawIndicatorTiles: sourceId {sourceId} does not exist in the target layer's TileSet.");
            return;
        }

        foreach (Vector2I cell in cells)
        {
            targetLayer.SetCell(cell, sourceId, atlasCoords);
        }
    }

    // Clears every indicator tile currently drawn.
    public void ClearIndicatorTiles()
    {
        indicatorLayer?.Clear();
        actionIndicatorLayer?.Clear();
    }

    // Positions the four hover bracket sprites on startingCell and plays their animation.
    public void PlayHoverBracketsAt(Vector2I startingCell)
    {
        if (leftHoverBracket == null || rightHoverBracket == null || topHoverBracket == null || bottomHoverBracket == null)
            return;
        Vector2 worldPos = CellToWorld(startingCell);
        leftHoverBracket.Position = worldPos;
        rightHoverBracket.Position = worldPos;
        topHoverBracket.Position = worldPos;
        bottomHoverBracket.Position = worldPos;
        leftHoverBracket.Visible = true;
        rightHoverBracket.Visible = true;
        topHoverBracket.Visible = true;
        bottomHoverBracket.Visible = true;
    }

    public void HideHoverBrackets()
    {
        if (leftHoverBracket != null) leftHoverBracket.Visible = false;
        if (rightHoverBracket != null) rightHoverBracket.Visible = false;
        if (topHoverBracket != null) topHoverBracket.Visible = false;
        if (bottomHoverBracket != null) bottomHoverBracket.Visible = false;
    }

    private void AddBorders()
    {
        if (NorthEastBorder == null || NorthWestBorder == null || SouthEastBorder == null || SouthWestBorder == null)
        {
            GD.PrintErr("AddBorders: One or more border exports are not assigned in the Inspector.");
            return;
        }
        NorthEastBorder.ZIndex = indicatorLayer.ZIndex + 1;
        NorthWestBorder.ZIndex = indicatorLayer.ZIndex + 1;
        SouthEastBorder.ZIndex = indicatorLayer.ZIndex + 1;
        SouthWestBorder.ZIndex = indicatorLayer.ZIndex + 1;
        NorthEastBorders.Add(NorthEastBorder);
        NorthEastBorder.Visible = false;
        NorthWestBorders.Add(NorthWestBorder);
        NorthWestBorder.Visible = false;
        SouthEastBorders.Add(SouthEastBorder);
        SouthEastBorder.Visible = false;
        SouthWestBorders.Add(SouthWestBorder);
        SouthWestBorder.Visible = false;
    }

    private void duplicateBorders(int northEastAmount, int northWestAmount, int southEastAmount, int southWestAmount)
    {
        for (int i = 0; i < northEastAmount; i++)
        {
            var newBorder = NorthEastBorder.Duplicate() as Sprite2D;
            newBorder.Name = $"NorthEastBorder_{i + 1}";
            AddChild(newBorder);
            NorthEastBorders.Add(newBorder);
            newBorder.Visible = false;
        }
        for (int i = 0; i < northWestAmount; i++)
        {
            var newBorder = NorthWestBorder.Duplicate() as Sprite2D;
            newBorder.Name = $"NorthWestBorder_{i + 1}";
            AddChild(newBorder);
            NorthWestBorders.Add(newBorder);
            newBorder.Visible = false;
        }
        for (int i = 0; i < southEastAmount; i++)
        {
            var newBorder = SouthEastBorder.Duplicate() as Sprite2D;
            newBorder.Name = $"SouthEastBorder_{i + 1}";
            AddChild(newBorder);
            SouthEastBorders.Add(newBorder);
            newBorder.Visible = false;
        }
        for (int i = 0; i < southWestAmount; i++)
        {
            var newBorder = SouthWestBorder.Duplicate() as Sprite2D;
            newBorder.Name = $"SouthWestBorder_{i + 1}";
            AddChild(newBorder);
            SouthWestBorders.Add(newBorder);
            newBorder.Visible = false;
        }
    }

    public void SetAllBorderVisibility(bool visible)
    {
        foreach (var border in NorthEastBorders)
            if (border != null) border.Visible = visible;
        foreach (var border in NorthWestBorders)
            if (border != null) border.Visible = visible;
        foreach (var border in SouthEastBorders)
            if (border != null) border.Visible = visible;
        foreach (var border in SouthWestBorders)
            if (border != null) border.Visible = visible;
    }

    public void DeleteBorders()
    {
        foreach (var border in NorthEastBorders)
            if (border != null && border != NorthEastBorder) border.QueueFree();
        NorthEastBorders.Clear();
        NorthEastBorders.Add(NorthEastBorder);

        foreach (var border in NorthWestBorders)
            if (border != null && border != NorthWestBorder) border.QueueFree();
        NorthWestBorders.Clear();
        NorthWestBorders.Add(NorthWestBorder);

        foreach (var border in SouthEastBorders)
            if (border != null && border != SouthEastBorder) border.QueueFree();
        SouthEastBorders.Clear();
        SouthEastBorders.Add(SouthEastBorder);

        foreach (var border in SouthWestBorders)
            if (border != null && border != SouthWestBorder) border.QueueFree();
        SouthWestBorders.Clear();
        SouthWestBorders.Add(SouthWestBorder);
    }

    public void DrawPatternBorders(Vector2I origin, AttackPattern pattern, AttackFacing facing)
    {
        if (pattern == null)
            return;
        if (pattern.maxRange < 0)
            return;

        DeleteBorders();
        SetAllBorderVisibility(true);

        List<CellAdjecentInfo> adjecentInfos = new List<CellAdjecentInfo>();

        int totalExtraNorthEastBordersNeeded = 0;
        int totalExtraNorthWestBordersNeeded = 0;
        int totalExtraSouthEastBordersNeeded = 0;
        int totalExtraSouthWestBordersNeeded = 0;
        List<Vector2I> NorthEastBorderCells = new List<Vector2I>();
        List<Vector2I> NorthWestBorderCells = new List<Vector2I>();
        List<Vector2I> SouthEastBorderCells = new List<Vector2I>();
        List<Vector2I> SouthWestBorderCells = new List<Vector2I>();

        HashSet<Vector2I> patternCells = GetPatternCells(origin, pattern, facing);

        foreach (Vector2I cell in patternCells)
        {
            CellAdjecentInfo adjecentInfo = new CellAdjecentInfo
            {
                cell = cell,
                PosXOccupied = patternCells.Contains(cell + new Vector2I(1, 0)),
                NegXOccupied = patternCells.Contains(cell + new Vector2I(-1, 0)),
                PosYOccupied = patternCells.Contains(cell + new Vector2I(0, 1)),
                NegYOccupied = patternCells.Contains(cell + new Vector2I(0, -1))
            };

            adjecentInfos.Add(adjecentInfo);

            if (!adjecentInfo.PosXOccupied)
            {
                totalExtraNorthEastBordersNeeded++;
                NorthEastBorderCells.Add(adjecentInfo.cell);
            }
            if (!adjecentInfo.NegXOccupied)
            {
                totalExtraSouthWestBordersNeeded++;
                SouthWestBorderCells.Add(adjecentInfo.cell);
            }
            if (!adjecentInfo.PosYOccupied)
            {
                totalExtraSouthEastBordersNeeded++;
                SouthEastBorderCells.Add(adjecentInfo.cell);
            }
            if (!adjecentInfo.NegYOccupied)
            {
                totalExtraNorthWestBordersNeeded++;
                NorthWestBorderCells.Add(adjecentInfo.cell);
            }
        }

        duplicateBorders(totalExtraNorthEastBordersNeeded, totalExtraNorthWestBordersNeeded, totalExtraSouthEastBordersNeeded, totalExtraSouthWestBordersNeeded);

        for (int i = 0; i < NorthEastBorderCells.Count; i++)
        {
            Vector2I cell = NorthEastBorderCells[i];
            if (i < NorthEastBorders.Count)
            {
                Sprite2D border = NorthEastBorders[i];
                border.Position = CellToWorld(cell);
                border.Visible = true;
            }
        }
        for (int i = 0; i < NorthWestBorderCells.Count; i++)
        {
            Vector2I cell = NorthWestBorderCells[i];
            if (i < NorthWestBorders.Count)
            {
                Sprite2D border = NorthWestBorders[i];
                border.Position = CellToWorld(cell);
                border.Visible = true;
            }
        }
        for (int i = 0; i < SouthEastBorderCells.Count; i++)
        {
            Vector2I cell = SouthEastBorderCells[i];
            if (i < SouthEastBorders.Count)
            {
                Sprite2D border = SouthEastBorders[i];
                border.Position = CellToWorld(cell);
                border.Visible = true;
            }
        }
        for (int i = 0; i < SouthWestBorderCells.Count; i++)
        {
            Vector2I cell = SouthWestBorderCells[i];
            if (i < SouthWestBorders.Count)
            {
                Sprite2D border = SouthWestBorders[i];
                border.Position = CellToWorld(cell);
                border.Visible = true;
            }
        }
    }
}



public struct CellAdjecentInfo
{
    public Vector2I cell;
    public bool PosXOccupied;
    public bool NegXOccupied;
    public bool PosYOccupied;
    public bool NegYOccupied;
}
