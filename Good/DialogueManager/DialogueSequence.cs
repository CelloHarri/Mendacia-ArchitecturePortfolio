using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DialogueSequence : Resource
{
    [Export] public string sequenceName = "";
    [Export] public Array<DialogueAction> Actions = new Array<DialogueAction>();
}