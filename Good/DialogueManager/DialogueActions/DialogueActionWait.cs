using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DialogueActionWait : DialogueAction
{
    [Export] public float duration = 1f;
}