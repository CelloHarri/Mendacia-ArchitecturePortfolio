using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DialogueActionChoice : DialogueAction
{
    [Export] public Array<DialogueChoiceOption> choices = new();
}