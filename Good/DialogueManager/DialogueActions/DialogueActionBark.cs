using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DialogueActionBark : DialogueAction
{
    [Export] public string characterName = "";
    [Export] public string text = "";
}