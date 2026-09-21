using Godot;
using Godot.Collections;
using System;
using System.Linq;

[Tool]
[GlobalClass]
public partial class DialogueActionSetFlag : DialogueAction
{
    [ExportToolButton("Add New Save Flag")]
    public Callable AddNewFlagButton => new Callable(this, nameof(AddNewFlag));
    [Export] public string NewFlagName { get; set; } = "";

    // Click to apply SearchFilter to the dropdown
    [ExportToolButton("Apply Filter")]
    public Callable ApplyFilterButton => new Callable(this, nameof(ApplyFilter));
    [Export] public string SearchFilter { get; set; } = "";

    private void ApplyFilter() => NotifyPropertyListChanged();

    [Export] public SaveFlags flagName;
    [Export] public int setValue = 1;



    private void AddNewFlag() => GameSaveData.CreateNewSaveFlagEnum(NewFlagName);

    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);
        if (property["name"].AsStringName() == nameof(flagName))
            DialogueActionFlagHelper.ApplyFlagSearchHint(property, SearchFilter);
    }
}