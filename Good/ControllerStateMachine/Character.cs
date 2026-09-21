using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

public enum Faction
{
    Player,
    NonControlledAlly,
    Enemy,
    None
}

// Shared base for any unit that can take part in a battle (Player or Enemy).
// Holds only the data the turn/grid systems need to treat units uniformly.
[GlobalClass]
public partial class Character : CharacterBody2D
{
    private Tween tileMoveTween; // Tracks the in-flight tile-movement tween so a new move can cleanly replace it.
    private bool isTileMoving = false; // Blocks WorldPlayerMovement from zeroing Velocity during tile animation.
    public bool IsTileMoving => isTileMoving; // Read-only access for TileCombatMovement subclasses to gate ProcessTurn.
    CollisionShape2D collisionShape;

    [Export] public CharacterStats characterStats;
    public bool isDead;
    public bool isDownButNotOut;
    public bool hasMadeAnActionThisTurn;

    public CharacterBody2DAnimator animator;

    // Populated in _Ready() by scanning direct children for ControllerBaseClass nodes.
    // Add any new movement type as a child node — it will be discovered automatically.
    private List<ControllerBaseClass> _ControllerNodes = new();
    public ControllerState currentControllerState { get; private set; } = ControllerState.World;

    // Returns the first child Controller node assignable to T (works with subclasses).
    public T GetController<T>() where T : ControllerBaseClass
        => _ControllerNodes.OfType<T>().FirstOrDefault();

    public Camera2D camera2D;

    public Vector2 LastVelocityNormalized { get; private set; } = Vector2.Zero; // Tracks the last non-zero velocity direction for animation purposes.

    [ExportGroup("CharacterBody2D Animator Settings")]
    private float currentZPosition = 0f; // Track current Z height for isometric display
    [Export] private AnimatedSprite2D MainSpriteForOffsettingFromShadow; // Sprite to visually offset for jumping effect (child of player node)

    [ExportGroup("Tile Movement Indicator")]
    [Export] public int tileSourceId = 0; // The source ID within the indicator TileMapLayer's own TileSet to use for this character's movement indicator
    [Export] public Vector2I atlasCoords = new Vector2I(0, 0); // The coordinates of the tile in that source's atlas to use for this character's movement indicator

    public Vector2I currentCell { get; set; }
    public bool hasActedThisTurn { get; set; } = false; // Set true once the unit moves; no longer blocks re-moving (players can move multiple times per turn within their original range).

    public float totalPathDuration { get; private set; } = 0f; // Total duration of the entire path (sum of all step durations) for the current tile-combat move. Used to time the end-of-turn callback.

    [ExportGroup("Debug")]
    [Export] public bool debugShowPositionTrail = false; // Draws a red (physics-tick) / cyan (rendered-frame) trail for diagnosing pixel jitter.

    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            if (child is ControllerBaseClass movement)
                _ControllerNodes.Add(movement);
            if (child is Camera2D cam)
                camera2D = cam;
            if (child is CharacterBody2DAnimator anim)
                animator = anim;
        }

        collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        connectSignals();

        if (debugShowPositionTrail)
            AddChild(new PositionTrailDebugDraw { Target = this });
    }

    public override void _Process(double delta)
    {
    }

    public bool IsDeathAnimationFinished => animator?.IsDeathFinished ?? false;

    private void UpdateVelocity(Vector2 movementVector)
    {
        Velocity = movementVector;
        if (movementVector.LengthSquared() > 0f)
            LastVelocityNormalized = movementVector.Normalized();
    }

    public void OnWorldSendVelocity(Vector2 movementVector, float zVelocity)
    {
        // Don't let the world movement system zero out Velocity while a tile-move tween is running.
        if (isTileMoving) return;

        UpdateVelocity(movementVector);
        MoveAndSlide();
        Position = (Position * 3f).Round() / 3f; // snap to 1920x1080 upscaled pixel grid (1/3 logical pixel)

        // Update Z position (height) - integrate Z velocity over time. This runs off the
        // SendVelocity signal, which fires from _PhysicsProcess, so use the physics delta
        // (not the render-frame delta) or the integration rate drifts from the actual call rate.
        currentZPosition += zVelocity * (float)GetPhysicsProcessDeltaTime();

        // Clamp Z position so player doesn't go below ground
        if (currentZPosition > 0)
        {
            currentZPosition = 0;
        }

        // Apply Z offset to visual node only (negative Z moves sprite up in isometric view)
        // This creates the jumping/height effect without affecting physics collision
        if (MainSpriteForOffsettingFromShadow != null)
        {
            MainSpriteForOffsettingFromShadow.Position = new Vector2(0, currentZPosition);
        }
    }

    private void UpdateTileCombatZIndex(Vector2I cell)
    {
        const int zIndexBase = 1000;
        ZIndex = zIndexBase + cell.Y - cell.X;
    }

    public void OnTileSendPath(Godot.Collections.Array<Vector2I> path)
    {
        // Animate smoothly through each cell of the path in sequence. Tile movement is tweened directly
        // on GlobalPosition (rather than driving Velocity/MoveAndSlide) so the unit lands exactly on each
        // tile's center regardless of collisions.
        // Kill any tween still running from a previous move (the player can re-move multiple times per
        // turn) so the two tweens don't fight over global_position - that fight is what caused the
        // rubber-banding/teleport-back glitch when a new move was issued mid-animation.
        tileMoveTween?.Kill();
        isTileMoving = true;
        totalPathDuration = 0f;
        tileMoveTween = CreateTween();
        // Physics mode ties position updates to the fixed 60Hz physics clock instead of the
        // render clock, whose frame pacing (run/max_fps) is uneven and shows up as jitter on
        // steep diagonal slopes (e.g. the 2:1 tile-combat diagonal).
        tileMoveTween.SetProcessMode(Tween.TweenProcessMode.Physics);
        Vector2 prevPos = GlobalPosition;
        foreach (Vector2I cell in path.Skip(1)) // Skip the start cell — character is already there; applying its cost would cause a zero-distance wait.
        {
            Vector2 worldPosition = BattleManager.battleManager.combatTileMapLayer.CellToWorld(cell);
            // Set Velocity to the step direction so CharacterBody2DAnimator can read it for animations.
            Vector2 capturedPrev = prevPos;
            Vector2 capturedDest = worldPosition;
            Vector2I capturedCell = cell;
            float stepDuration = Mathf.Max(characterStats.tileMoveStepDuration * BattleManager.battleManager.combatTileMapLayer.GetMovementCost(cell), characterStats.tileMoveStepDuration * BattleManager.battleManager.combatTileMapLayer.GetMovementCost(BattleManager.battleManager.combatTileMapLayer.WorldToCell(capturedPrev)));
            tileMoveTween.TweenCallback(Callable.From(() => { UpdateVelocity((capturedDest - capturedPrev).Normalized()); UpdateTileCombatZIndex(capturedCell); }));
            tileMoveTween.TweenProperty(this, "global_position", worldPosition, stepDuration);
            totalPathDuration += stepDuration; // Total duration of the entire path
            prevPos = worldPosition;
        }
        // Clear Velocity and the moving flag when the path is complete so the animator returns to idle.
        tileMoveTween.TweenCallback(Callable.From(() => { UpdateVelocity(Vector2.Zero); isTileMoving = false; }));
    }

    public void OnTileSendPathInstant(Godot.Collections.Array<Vector2I> path)
    {
        tileMoveTween?.Kill();
        tileMoveTween = null;
        isTileMoving = false;
        totalPathDuration = 0f;

        if (path == null || path.Count == 0)
            return;

        Vector2 worldPosition = BattleManager.battleManager.combatTileMapLayer.CellToWorld(path[0]);
        GlobalPosition = worldPosition;
        UpdateTileCombatZIndex(path[0]);

        Vector2 prevPos = worldPosition;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2I cell = path[i];
            worldPosition = BattleManager.battleManager.combatTileMapLayer.CellToWorld(cell);
            UpdateVelocity((worldPosition - prevPos).Normalized());
            UpdateTileCombatZIndex(cell);
            GlobalPosition = worldPosition;
            prevPos = worldPosition;
        }

        UpdateVelocity(Vector2.Zero);
    }

    public void OnIsOnGround(bool isGrounded)
    {
        // This can be used to trigger landing animations or sound effects
        if (isGrounded)
        {
            this.CollisionMask = 1;
        }
        else
        {
            this.CollisionMask = 0;
        }
    }

    // Transitions all discovered movement nodes to a new state simultaneously.
    // Call this from BattleManager (or any system) to switch between world and combat movement.
    public void ChangeControllerState(ControllerState newState)
    {
        if (currentControllerState == newState) return;
        ControllerState oldState = currentControllerState;

        foreach (var node in _ControllerNodes)
            node.OnExitState(oldState);
        foreach (var node in _ControllerNodes)
            node.OnEnterState(newState);

        currentControllerState = newState;
    }

    public virtual void OnIsDead(bool isDeath)
    {
        if (isDeath == true)
        {
            var worldMov = GetController<WorldController>();
            if (worldMov != null)
                worldMov.SendVelocity -= OnWorldSendVelocity;
            isDead = true;
        }
    }

    protected virtual void connectSignals()
    {
        var worldMov = GetController<WorldController>();
        if (worldMov != null)
        {
            worldMov.SendVelocity += OnWorldSendVelocity;
            worldMov.IsOnGround += OnIsOnGround;
        }

        var tileCombat = GetController<TileCombatController>();
        if (tileCombat != null)
            tileCombat.SendPath += OnTileSendPath;

        characterStats.IsDead += OnIsDead;
    }

    public void ActivateCollision(bool enableCollision)
    {
        if (enableCollision)
            collisionShape.Disabled = false;
        else
            collisionShape.Disabled = true;
    }
}
