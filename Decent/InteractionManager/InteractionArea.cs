using Godot;

[Tool]
[GlobalClass]
public partial class InteractionArea : Area2D
{
    [Export] public string interactionName = "Interact";
    [Export] public Shape2D shape2D;

    public InteractionArea()
    {
        // Set the collision layer and mask to 0 to avoid collisions
        CollisionLayer = 0;
        CollisionMask = 2; // Only detect collisions with layer 2 (Hopefully nothing else is on layer 2)
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        Connect("body_entered", new Callable(this, "OnBodyEntered"));
        Connect("body_exited", new Callable(this, "OnBodyExited"));
    }

    public override void _Process(double delta)
    {
    }

    public System.Action interact = () =>
    {
        GD.Print("Interacted");
    };

    public void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            InteractionManager.instance.RegisterInteractionArea(this);
        }
    }

    public void OnBodyExited(Node2D body)
    {
        if (body is Player player)
        {
            InteractionManager.instance.UnregisterInteractionArea(this);
        }
    }
}