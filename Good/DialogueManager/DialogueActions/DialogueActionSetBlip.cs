using Godot;

public partial class DialogueActionSetBlip : DialogueAction
{
    [Export]
    public AudioStream BlipSound { get; set; }
}