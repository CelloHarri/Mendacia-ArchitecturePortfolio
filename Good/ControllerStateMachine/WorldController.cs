using Godot;
using System;
using System.Formats.Asn1;

public enum WorldMovementType
{
    HalfIsometric,
    TopDown
}

public partial class WorldController : ControllerBaseClass
{
    [Signal] public delegate void SendVelocityEventHandler(Vector2 velocity, float zVel);
    [Signal] public delegate void IsOnGroundEventHandler(bool isOnGround);

    protected Character character;

    protected Faction characterFaction;

    [ExportGroup("Movement Settings")]
    [Export] public WorldMovementType movementType { get; set; } = WorldMovementType.TopDown;

    protected Vector2 velocity = Vector2.Zero;

    protected Vector2 characterMovementVector; // XY movement on the ground plane
    protected float zVelocity = 0f; // Velocity on Z-axis (height/jumping)
    protected float zPosition = 0f; // Current height above ground (0 = on ground)
    protected bool isOnStairs = false;
    protected bool isOverGround = false;

    [ExportGroup("Area Detection")]
    [Export] public Area2D stairsDetectorCollisionMaskFive;  // Area2D for detecting stairs (collision mask: layer 5)
    [Export] public Area2D groundDetectorCollisionMaskSix;  // Area2D for detecting ground (collision mask: layer 6)

    [ExportGroup("Jumping")]
    [Export] protected bool allowJumping = false; //If the game will or will not have jumping
    [Export] protected float jumpStrength = 400;
    [Export] protected int maxJumps = 1; //# of times player can jump before touching the ground again
    [Export] protected float gravity = 980f; //Gravity for Z-axis (height)

    protected bool isGrounded = true;
    protected int jumpCount = 0;

    protected bool isActive = true;

    //TODO -> Make Override for Basically everything so It can be used in a state machine for the player and enemies. (This will allow for more complex movement and jumping mechanics, as well as better control over the player's movement in different states.)

    public override void _Ready()
    {
        character = GetParent<Character>();
        characterFaction = character.characterStats.faction;
        connectSignals();
    }

    public override void _Process(double delta)
    {

    }

    public override void _PhysicsProcess(double delta)
    {
        // Update Area2D positions to follow the player
        stairsDetectorCollisionMaskFive.GlobalPosition = character.GlobalPosition;
        groundDetectorCollisionMaskSix.GlobalPosition = character.GlobalPosition;

        EmitSendVelocitySignal(characterMovementVector, zVelocity);
    }


    // === Stairs Detection ===
    protected virtual void OnStairsEntered(Node2D body)
    {
        if (body is TileMapLayer)
        {
            isOnStairs = true;
        }
    }

    protected virtual void OnStairsExited(Node2D body)
    {
        if (body is TileMapLayer)
        {
            isOnStairs = false;
        }
    }

    // === Ground Detection ===
    protected virtual void OnGroundEntered(Node2D body)
    {
        if (body is TileMapLayer)
        {
            isOverGround = true;

            // Only set grounded if at ground level (zPosition <= 0)
            if (zPosition <= 0)
            {
                isGrounded = true;
                jumpCount = 0;
                zPosition = 0f;
                zVelocity = 0f;
            }
        }
    }

    protected virtual void OnGroundExited(Node2D body)
    {
        if (body is TileMapLayer)
        {
            isOverGround = false;
            isGrounded = false;
        }
    }

    public virtual void EmitSendVelocitySignal(Vector2 velocity, float zVel)
    {
        EmitSignal(SignalName.SendVelocity, velocity, zVel);
    }

    public virtual void EmitIsOnGroundSignal(bool isOnGround)
    {
        EmitSignal(SignalName.IsOnGround, isOnGround);
    }

    protected virtual void connectSignals()
    {
        stairsDetectorCollisionMaskFive.BodyEntered += OnStairsEntered;
        stairsDetectorCollisionMaskFive.BodyExited += OnStairsExited;

        // Connect ground detector signals
        groundDetectorCollisionMaskSix.BodyEntered += OnGroundEntered;
        groundDetectorCollisionMaskSix.BodyExited += OnGroundExited;
    }


    // === State Machine Hooks ===
    // These are called by the Character class when the Controller state changes.
    public override void OnEnterState(ControllerState newState)
    {
        base.OnEnterState(newState);
        isActive = newState == ControllerState.World;
        if (!isActive)
        {
            characterMovementVector = Vector2.Zero;
            zVelocity = 0f;
        }
    }

    public override void OnExitState(ControllerState oldState)
    {
        if (oldState == ControllerState.World)
        {
            isActive = false;
            characterMovementVector = Vector2.Zero;
            zVelocity = 0f;
            EmitSendVelocitySignal(Vector2.Zero, 0f);
        }
    }
}