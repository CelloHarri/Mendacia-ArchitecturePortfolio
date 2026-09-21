using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DialogueActionSetSpeaker : DialogueAction
{
    [Export] public string characterName = "";
}
