using Godot;
using Godot.Collections;
using System;
using System.Linq;

public enum FlagComparison { Equal, NotEqual, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual }

[GlobalClass]
public abstract partial class DialogueAction : Resource { }

// Shared helper — lives outside the classes so all can use it
public static class DialogueActionFlagHelper
{
    public static void ApplyFlagSearchHint(Dictionary property, string searchFilter) // Used to Search save flags in the editor
    {
        string[] allFlags = Enum.GetNames(typeof(SaveFlags));
        string[] filteredFlags = string.IsNullOrWhiteSpace(searchFilter)
            ? allFlags
            : allFlags
                .Where(f => f.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (filteredFlags.Length == 0) // If Length is 0 after filtering, revert to all flags
            filteredFlags = allFlags;

        string hintString = string.Join(",", filteredFlags);

        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = hintString;
    }
}