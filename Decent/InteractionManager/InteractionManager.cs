using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class InteractionManager : Node
{
    public static InteractionManager instance;
    public Player player;
    public Label label;
    public string labelText = "Press [Z] to ";

    public List<InteractionArea> interactionAreas = new List<InteractionArea>();
    public bool canInteract = true;

    public override void _Ready()
    {
        instance = this;


        label = new Label();
        AddChild(label);
        label.ZIndex = 20;
        label.TopLevel = true;
    }

    public override void _Process(double delta)
    {
        if (player == null)
        {
            return;
        }
        if (interactionAreas.Count > 0 && canInteract)
        {
            interactionAreas.Sort((a, b) =>
            {
                float distanceA = player.GlobalPosition.DistanceTo(a.GlobalPosition);
                float distanceB = player.GlobalPosition.DistanceTo(b.GlobalPosition);
                return distanceA.CompareTo(distanceB);
            });
            label.Text = labelText + interactionAreas[0].interactionName;
            label.GlobalPosition = interactionAreas[0].GlobalPosition + new Vector2(label.Size.X / 2, -50);
            label.Visible = true;
        }
        else
        {
            label.Visible = false;
        }
        getInput();
    }

    public void getInput()
    {
        if (Input.IsActionJustPressed("interact") && interactionAreas.Count > 0 && canInteract)
        {
            canInteract = false;
            label.Visible = false;
            interactionAreas[0].interact.Invoke();
        }
    }

    public void RegisterInteractionArea(InteractionArea area)
    {
        if (!interactionAreas.Contains(area))
        {
            interactionAreas.Add(area);
        }
    }

    public void UnregisterInteractionArea(InteractionArea area)
    {
        int index = interactionAreas.IndexOf(area);
        if (index != -1)
        {
            interactionAreas.RemoveAt(index);
        }
    }

    public void OnPlayerReady(Player playerInstance)
    {
        player = playerInstance;
    }
}
