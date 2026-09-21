using Godot;
using Godot.Collections;

public enum ButtonContainerType
{
    Zero = 0,
    Two = 2,
    Three = 3,
    Four = 4
}

public partial class DialogueBox : Control
{
    [Export] public NinePatchRect dialogueBoxBackground;
    [Export] public RichTextLabel textLabel;
    [Export] public Control TwoButtonContainer;
    [Export] public Control ThreeButtonContainer;
    [Export] public Control FourButtonContainer;
    [Export] public UIFocusManager uiFocusManager;

    private Vector2 hiddenPosition = new Vector2(-200, 25);
    private Vector2 visiblePosition = new Vector2(-200, -125);

    public override void _Ready()
    {
        if (textLabel != null)
            textLabel.BbcodeEnabled = true;
    }

    public Tween ShowDialogueBox()
    {
        Tween tween = CreateTween();
        tween.TweenProperty(dialogueBoxBackground, "position", visiblePosition, 0.5);
        tween.Play();
        return tween;
    }

    public Tween HideDialogueBox()
    {
        Tween tween = CreateTween();
        tween.TweenProperty(dialogueBoxBackground, "position", hiddenPosition, 0.5);
        tween.Play();
        return tween;
    }

    public void SetText(string text)
    {
        textLabel.Visible = true;
        textLabel.Text = text;
    }

    public void SetAmountOfVisibleCharacters(int amount)
    {
        textLabel.VisibleCharacters = amount;
    }

    public int GetTotalCharacterCount() => textLabel.GetTotalCharacterCount();

    public void ClearText()
    {
        textLabel.Visible = false;
        textLabel.Text = "";
    }

    public void SetButtonContainerVisibility(ButtonContainerType buttonCount)
    {
        if (buttonCount == ButtonContainerType.Zero)
        {
            TwoButtonContainer.Visible = false;
            ThreeButtonContainer.Visible = false;
            FourButtonContainer.Visible = false;
            return;
        }
        if (buttonCount == ButtonContainerType.Two)
        {
            TwoButtonContainer.Visible = true;
            ThreeButtonContainer.Visible = false;
            FourButtonContainer.Visible = false;
            return;
        }
        if (buttonCount == ButtonContainerType.Three)
        {
            TwoButtonContainer.Visible = false;
            ThreeButtonContainer.Visible = true;
            FourButtonContainer.Visible = false;
            return;
        }
        if (buttonCount == ButtonContainerType.Four)
        {
            TwoButtonContainer.Visible = false;
            ThreeButtonContainer.Visible = false;
            FourButtonContainer.Visible = true;
            return;
        }

        GD.PrintErr("Invalid button count: " + (int)buttonCount);
        TwoButtonContainer.Visible = false;
        ThreeButtonContainer.Visible = false;
        FourButtonContainer.Visible = false;
    }

    public Array<Button> GetVisibleButtons()
    {
        Array<Button> visibleButtons = new Array<Button>();
        if (TwoButtonContainer.Visible)
        {
            foreach (Button button in TwoButtonContainer.GetChildren())
                visibleButtons.Add(button);
        }
        if (ThreeButtonContainer.Visible)
        {
            foreach (Button button in ThreeButtonContainer.GetChildren())
                visibleButtons.Add(button);
        }
        if (FourButtonContainer.Visible)
        {
            foreach (Button button in FourButtonContainer.GetChildren())
                visibleButtons.Add(button);
        }
        return visibleButtons;
    }
}
