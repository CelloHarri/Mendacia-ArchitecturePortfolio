using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class DialogueManager : Node
{
    public static DialogueManager instance;

    // BBCode alias tables — add entries here to expand shorthand tags
    private static readonly System.Collections.Generic.Dictionary<string, string> ColorAliases = new()
    {
        { "sierra", "#a23e8c" },
        { "Sierra", "#a23e8c" }
    };

    private static readonly System.Collections.Generic.Dictionary<string, string> FontAliases = new()
    {
        // { "header", "res://Fonts/Header.ttf" },
    };

    private static readonly System.Collections.Generic.Dictionary<string, string> SoundAliases = new()
    {
        // { "default", "res://Sounds/SFX/DefaultBlip.wav" },
    };

    // State
    private DialogueSequence activeSequence;
    private int actionIndex;
    private bool isActive;
    private bool isScrolling; // True While Text is animating in
    private bool awaitingAdvance; // True After Scroll finishes, waiting for confirm

    // Text Scroll
    private float dialogueTimer = 0f;
    private float charactersPerSecond = 30f;
    private string targetText;
    private int revealedCharacters; // Drive RichTextLabel.VisibleCharacters in _Process

    // Voice Blips
    private float blipTimer = 0f;
    private AudioStream characterVoiceBlip;
    private float voiceBlipsPerSecond; // Triggered in _Process while isScrolling, keyed by characterName

    // Current Speaker
    private string currentSpeakingCharacter = "";

    private DialogueBox dialogueBox;

    //Button Variables
    private int totalButtons; // Total number of buttons in the dialogue box -> Max 4
    private Array<Button> visibleButtons; // Stores the currently visible buttons in the dialogue box
    private System.Collections.Generic.Dictionary<Button, Action> connectedChoiceHandlers = new(); // Tracks each button's current Pressed handler so it can be replaced instead of stacked

    private bool scheduleNextActionOnPhysicsTick;

    // Temporary flags — cleared each time a new sequence starts
    public System.Collections.Generic.Dictionary<string, int> TempFlags { get; } = new();

    // Return stack for CheckFlag branches — each entry is (sequence, resumeIndex)
    private System.Collections.Generic.Stack<(DialogueSequence seq, int idx)> sequenceStack = new();

    public override void _EnterTree()
    {
        instance = this;
    }

    public override void _Ready()
    {
        var canvasLayer = new CanvasLayer();
        AddChild(canvasLayer);

        var dialogueBoxScene = GD.Load<PackedScene>("res://Scenes/DialogueBoxScene/DialogueBox.tscn");
        dialogueBox = dialogueBoxScene.Instantiate<DialogueBox>();
        canvasLayer.AddChild(dialogueBox);
    }

    public override void _Process(double delta)
    {
        HandleDialogueInput();

        if (!isScrolling)
            return;

        dialogueTimer += (float)delta;
        blipTimer += (float)delta;

        if (dialogueTimer >= 1f / charactersPerSecond)
        {
            revealedCharacters++;
            dialogueTimer = 0f;
            dialogueBox.SetAmountOfVisibleCharacters(revealedCharacters);
        }
        if (blipTimer >= 1f / voiceBlipsPerSecond)
        {
            if (characterVoiceBlip != null)
                SoundManager.instance.PlaySFXStream(characterVoiceBlip);
            blipTimer = 0f;
        }

        if (revealedCharacters >= dialogueBox.GetTotalCharacterCount())
        {
            isScrolling = false;
            awaitingAdvance = true;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!scheduleNextActionOnPhysicsTick)
            return;

        scheduleNextActionOnPhysicsTick = false;
        if (!isScrolling && awaitingAdvance)
        {
            NextAction();
        }
    }

    // Public API
    public async void PlaySequence(DialogueSequence sequence)
    {
        if (sequence == null)
            return;

        activeSequence = sequence;
        actionIndex = 0;
        isActive = true;
        isScrolling = false;
        awaitingAdvance = false;
        characterVoiceBlip = null; // Reset to default blip for the new sequence
        TempFlags.Clear();
        sequenceStack.Clear();

        Tween showTween = dialogueBox.ShowDialogueBox();
        await ToSignal(showTween, Tween.SignalName.Finished);

        ProcessCurrentAction();
    }



    /// <summary>
    /// Advances the dialogue. If currently scrolling, it will finish the scroll immediately.
    /// If not scrolling and awaiting advance, it will proceed to the next action.
    /// Used for code, not input handling
    /// </summary>
    public void Advance()
    {
        if (isScrolling)
        {
            FinishScrollImmediately(); // after this, isScrolling = false and awaitingAdvance = true
        }
        else if (!isScrolling && awaitingAdvance)
        {
            NextAction();
        }
        else
        {
            GD.PrintRich("[color=red]Advance called when not scrolling and not awaiting advance[/color]");
        }
    }

    public async void Close()
    {
        isActive = false;
        isScrolling = false;
        awaitingAdvance = false;
        Tween hideTween = dialogueBox.HideDialogueBox();
        dialogueBox.SetButtonContainerVisibility(ButtonContainerType.Zero);
        dialogueBox.ClearText();
        await ToSignal(hideTween, Tween.SignalName.Finished);
    }

    public void SetChoices(string[] choices, Action<int> onChoiceSelected)
    {
        int count = Mathf.Clamp(choices.Length, 0, 4);
        var containerType = (ButtonContainerType)count;
        dialogueBox.SetButtonContainerVisibility(containerType);

        visibleButtons = dialogueBox.GetVisibleButtons();
        for (int i = 0; i < visibleButtons.Count; i++)
        {
            visibleButtons[i].Text = choices[i];
            int capturedIndex = i;
            ConnectChoiceButton(visibleButtons[i], () =>
            {
                dialogueBox.SetButtonContainerVisibility(ButtonContainerType.Zero);
                onChoiceSelected(capturedIndex);
            });
        }

        // Refresh focus manager with the active container's buttons
        var activeContainer = containerType switch
        {
            ButtonContainerType.Two => dialogueBox.TwoButtonContainer,
            ButtonContainerType.Three => dialogueBox.ThreeButtonContainer,
            ButtonContainerType.Four => dialogueBox.FourButtonContainer,
            _ => null
        };
        if (activeContainer != null)
            dialogueBox.uiFocusManager.SetFocusablesFromContainer(activeContainer);

        awaitingAdvance = false;
        isScrolling = false;
    }

    public string GetCurrentSpeakingCharacter() => currentSpeakingCharacter;

    // Private Helpers
    private void ProcessCurrentAction()
    {
        if (actionIndex >= activeSequence.Actions.Count)
        {
            // Return to caller if this was a branch, otherwise close
            if (sequenceStack.Count > 0)
            {
                var (seq, idx) = sequenceStack.Pop();
                activeSequence = seq;
                actionIndex = idx;
                ProcessCurrentAction();
            }
            else
            {
                Close();
            }
            return;
        }
        var action = activeSequence.Actions[actionIndex];

        if (action is DialogueActionText text)
        {
            targetText = PreprocessDialogue(text.text);
            dialogueBox.SetText(targetText);
            dialogueBox.SetAmountOfVisibleCharacters(0);
            revealedCharacters = 0;
            dialogueTimer = 0f;
            blipTimer = 0f;
            isScrolling = true;
            awaitingAdvance = false;
        }
        else if (action is DialogueActionSound sound)
        {
            SoundManager.instance.PlaySFX(sound.soundKey);
            NextAction();
        }
        else if (action is DialogueActionSetFlag setFlag)
        {
            GameManager.currentSaveData.WriteToFlag(setFlag.flagName, setFlag.setValue);
            NextAction();
        }
        else if (action is DialogueActionCheckFlag checkFlag)
        {
            int flagValue = checkFlag.flagSource == FlagSource.SaveFlag
                ? GameManager.currentSaveData.ReadFromFlag(checkFlag.flagName)
                : (TempFlags.TryGetValue(checkFlag.tempFlagKey, out int tmp) ? tmp : 0);

            bool result = checkFlag.comparison switch
            {
                FlagComparison.Equal => flagValue == checkFlag.compareValue,
                FlagComparison.NotEqual => flagValue != checkFlag.compareValue,
                FlagComparison.GreaterThan => flagValue > checkFlag.compareValue,
                FlagComparison.LessThan => flagValue < checkFlag.compareValue,
                FlagComparison.GreaterOrEqual => flagValue >= checkFlag.compareValue,
                FlagComparison.LessOrEqual => flagValue <= checkFlag.compareValue,
                _ => false
            };

            var branch = result ? checkFlag.sequenceIfTrue : checkFlag.sequenceIfFalse;
            if (branch != null)
                BranchToSequence(branch, actionIndex + 1);
            else
                NextAction();
        }
        else if (action is DialogueActionChoice choice)
        {
            int count = Mathf.Clamp(choice.choices.Count, 0, 4);
            var containerType = (ButtonContainerType)count;
            dialogueBox.SetButtonContainerVisibility(containerType);

            visibleButtons = dialogueBox.GetVisibleButtons();
            for (int i = 0; i < visibleButtons.Count; i++)
            {
                var option = choice.choices[i];
                visibleButtons[i].Text = option.label;
                int capturedIndex = i;
                ConnectChoiceButton(visibleButtons[i], () => OnChoiceSelected(choice, capturedIndex));
            }

            // Refresh focus manager with the active container's buttons
            var activeContainer = containerType switch
            {
                ButtonContainerType.Two => dialogueBox.TwoButtonContainer,
                ButtonContainerType.Three => dialogueBox.ThreeButtonContainer,
                ButtonContainerType.Four => dialogueBox.FourButtonContainer,
                _ => null
            };
            if (activeContainer != null)
                dialogueBox.uiFocusManager.SetFocusablesFromContainer(activeContainer);

            awaitingAdvance = false;
            isScrolling = false;
            // Execution pauses here; OnChoiceSelected will call NextAction
        }
        else if (action is DialogueActionWait wait)
        {
            GetTree().CreateTimer(wait.duration).Timeout += NextAction;
        }
        else if (action is DialogueActionClearText)
        {
            dialogueBox.ClearText();
            NextAction();
        }
        else if (action is DialogueActionClose)
        {
            Close();
        }
        else if (action is DialogueActionBark bark)
        {
            // Complex System needs to be implemented, may not be needed for this, may be a seperate system
        }
        else if (action is DialogueActionChangeSpeed changeSpeed)
        {
            charactersPerSecond = changeSpeed.charactersPerSecond;
            NextAction();
        }
        else if (action is DialogueActionSetBlip changeBlip)
        {
            if (SoundAliases.TryGetValue(changeBlip.BlipKey, out string blipPath))
                characterVoiceBlip = GD.Load<AudioStream>(blipPath);
            else
                GD.PrintErr($"DialogueManager: No SoundAliases entry for blip key '{changeBlip.BlipKey}'.");
            NextAction();
        }
        else if (action is DialogueActionSetSpeaker setSpeaker)
        {
            currentSpeakingCharacter = setSpeaker.characterName;
            NextAction();
        }
    }
    private void NextAction() { actionIndex++; awaitingAdvance = false; ProcessCurrentAction(); }

    // Branches into a sub-sequence; resumes the caller at resumeIndex when the branch ends
    private void BranchToSequence(DialogueSequence branch, int resumeIndex)
    {
        sequenceStack.Push((activeSequence, resumeIndex));
        activeSequence = branch;
        actionIndex = 0;
        isScrolling = false;
        awaitingAdvance = false;
        ProcessCurrentAction();
    }

    // Disconnects a button's previous choice handler (if any) before wiring the new one
    private void ConnectChoiceButton(Button button, Action onPressed)
    {
        if (connectedChoiceHandlers.TryGetValue(button, out Action previous))
            button.Pressed -= previous;

        connectedChoiceHandlers[button] = onPressed;
        button.Pressed += onPressed;
    }

    private void OnChoiceSelected(DialogueActionChoice choiceAction, int index)
    {
        var option = choiceAction.choices[index];

        if (option.setSaveFlag)
            GameManager.currentSaveData.WriteToFlag(option.saveFlag, option.saveFlagValue);

        if (option.setTempFlag && !string.IsNullOrEmpty(option.tempFlagKey))
            TempFlags[option.tempFlagKey] = option.tempFlagValue;

        dialogueBox.SetButtonContainerVisibility(ButtonContainerType.Zero);
        NextAction();
    }

    private void HandleDialogueInput()
    {
        if (!isActive) return;

        // skip_dialogue_scrolling: only completes scroll, nothing else
        if (isScrolling && Input.IsActionJustPressed("skip_dialogue_scrolling"))
        {
            FinishScrollImmediately();
            return;
        }

        // advance_dialogue: only advances, never skips scroll
        if (!isScrolling && awaitingAdvance && Input.IsActionJustPressed("advance_dialogue"))
        {
            NextAction();
            return;
        }

        // advance_and_skip_dialogue_scrolling: does both in one press
        if (Input.IsActionJustPressed("advance_and_skip_dialogue_scrolling"))
        {
            if (isScrolling)
            {
                FinishScrollImmediately(); // after this, isScrolling = false and awaitingAdvance = true
                scheduleNextActionOnPhysicsTick = true;
            }
            else if (!isScrolling && awaitingAdvance)
            {
                NextAction();
            }
        }
    }

    private void FinishScrollImmediately()
    {
        dialogueBox.SetAmountOfVisibleCharacters(-1);
        isScrolling = false;
        awaitingAdvance = true;
    }

    /// <summary>Resolves color/font name aliases and validates BBCode before display.</summary>
    private string PreprocessDialogue(string raw)
    {
        foreach (System.Collections.Generic.KeyValuePair<string, string> entry in ColorAliases)
            raw = raw.Replace($"[color={entry.Key}]", $"[color={entry.Value}]");

        foreach (System.Collections.Generic.KeyValuePair<string, string> entry in FontAliases)
            raw = raw.Replace($"[font={entry.Key}]", $"[font={entry.Value}]");

        raw = AutoColorCharacterNames(raw);
        raw = ApplySpeakerFont(raw);

        return raw;
    }

    // Wraps the whole line in the current speaker's font when their name matches a FontAliases key
    private string ApplySpeakerFont(string text)
    {
        if (string.IsNullOrEmpty(currentSpeakingCharacter) || !FontAliases.TryGetValue(currentSpeakingCharacter, out string fontPath))
            return text;

        return $"[font={fontPath}]{text}[/font]";
    }

    // Wraps bare occurrences of known character names in their alias color, skipping ones already inside a [color] tag
    private string AutoColorCharacterNames(string text)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Collections.Generic.KeyValuePair<string, string> entry in ColorAliases)
        {
            if (!seenNames.Add(entry.Key)) // Skip duplicate casing entries (e.g. "sierra" / "Sierra")
                continue;

            string hex = entry.Value;
            string wordPattern = $@"\b{Regex.Escape(entry.Key)}\b";

            text = Regex.Replace(text, wordPattern, match =>
            {
                int lastOpen = text.LastIndexOf("[color=", match.Index, StringComparison.OrdinalIgnoreCase);
                int lastClose = text.LastIndexOf("[/color]", match.Index, StringComparison.OrdinalIgnoreCase);
                bool alreadyColored = lastOpen > lastClose;
                return alreadyColored ? match.Value : $"[color={hex}]{match.Value}[/color]";
            }, RegexOptions.IgnoreCase);
        }

        return text;
    }
}