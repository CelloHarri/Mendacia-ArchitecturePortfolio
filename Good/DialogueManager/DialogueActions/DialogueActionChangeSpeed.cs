using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DialogueActionChangeSpeed : DialogueAction
{
    [Export] public float charactersPerSecond = 20f;
}