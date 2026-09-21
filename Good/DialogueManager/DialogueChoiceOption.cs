using Godot;
using Godot.Collections;
using System;
using System.Linq;

[Tool]
[GlobalClass]
public partial class DialogueChoiceOption : Resource
{
    [Export] public string label = "";

    // Permanent save flag
    [Export] public bool setSaveFlag = false;
    [Export] public string SaveFlagSearchFilter { get; set; } = "";
    [ExportToolButton("Apply Save Flag Filter")]
    public Callable ApplySaveFlagFilterButton => new Callable(this, nameof(ApplySaveFlagFilter));
    private void ApplySaveFlagFilter() => NotifyPropertyListChanged();
    [Export] public SaveFlags saveFlag;
    [Export] public int saveFlagValue = 1;

    // Temporary session flag (cleared when dialogue ends or scene changes)
    [Export] public bool setTempFlag = false;
    [Export] public string tempFlagKey = "";
    [Export] public int tempFlagValue = 1;

    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);
        if (property["name"].AsStringName() == nameof(saveFlag))
            DialogueActionFlagHelper.ApplyFlagSearchHint(property, SaveFlagSearchFilter);
    }
}
