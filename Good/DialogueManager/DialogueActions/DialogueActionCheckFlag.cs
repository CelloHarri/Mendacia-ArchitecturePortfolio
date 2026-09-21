using Godot;
using Godot.Collections;
using System;
using System.Linq;

public enum FlagSource { SaveFlag, TempFlag }

[Tool]
[GlobalClass]
public partial class DialogueActionCheckFlag : DialogueAction
{
    [Export] public FlagSource flagSource = FlagSource.SaveFlag;

    // Save flag fields
    [Export] public string SearchFilter { get; set; } = "";
    [ExportToolButton("Apply Filter")]
    public Callable ApplyFilterButton => new Callable(this, nameof(ApplyFilter));
    private void ApplyFilter() => NotifyPropertyListChanged();
    [Export] public SaveFlags flagName;

    // Temp flag fields
    [Export] public string tempFlagKey = "";

    [Export] public FlagComparison comparison = FlagComparison.GreaterOrEqual;
    [Export] public int compareValue = 1;
    [Export] public DialogueSequence sequenceIfTrue;
    [Export] public DialogueSequence sequenceIfFalse;

    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);
        if (property["name"].AsStringName() == nameof(flagName))
            DialogueActionFlagHelper.ApplyFlagSearchHint(property, SearchFilter);
    }
}
