using Godot;

public partial class InputManager : Node
{
    public static InputManager Instance { get; private set; }

    private bool _suppressNextControllerInput = false;

    // Call this from your swap button before changing the profile
    public void SuppressNextControllerInput() => _suppressNextControllerInput = true;

    public override void _Ready()
    {
        Instance = this;
        ApplyCurrentInputProfile();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ApplyCurrentInputProfile()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.ApplyCurrentInputProfileInternal();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventJoypadButton ||
            (@event is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > 0.2f))
        {
            if (_suppressNextControllerInput) { _suppressNextControllerInput = false; return; }
            if (GameManager.currentInputType != InputType.Controller)
            {
                GameManager.currentInputType = InputType.Controller;
                ApplyCurrentInputProfile();
            }
        }

        else if (@event is InputEventKey && GameManager.mostRecentKeyboardInputType == InputType.KeyboardOnly)
        {
            if (GameManager.currentInputType != InputType.KeyboardAndMouse && GameManager.currentInputType != InputType.KeyboardOnly)
            {
                GameManager.currentInputType = InputType.KeyboardOnly;
                ApplyCurrentInputProfile();
            }
        }
        else if ((@event is InputEventMouseMotion or InputEventKey) && GameManager.mostRecentKeyboardInputType == InputType.KeyboardAndMouse)
        {
            if (GameManager.currentInputType != InputType.KeyboardAndMouse)
            {
                GameManager.currentInputType = InputType.KeyboardAndMouse;
                ApplyCurrentInputProfile();
            }
        }
    }

    private void ApplyCurrentInputProfileInternal()
    {
        switch (GameManager.currentInputType)
        {
            case InputType.Controller:
                ApplyControllerProfile();
                break;
            case InputType.KeyboardOnly:
                ApplyKeyboardOnlyProfile();
                break;
            default:
                ApplyKeyboardAndMouseProfile();
                break;
        }
    }

    private void ApplyKeyboardOnlyProfile()
    {
        // RebindAction("ui_left", CreateKey(Key.Left));
        // RebindAction("ui_right", CreateKey(Key.Right));
        // RebindAction("ui_up", CreateKey(Key.Up));
        // RebindAction("ui_down", CreateKey(Key.Down));
        RemoveActionBindings("ui_left");
        RemoveActionBindings("ui_right");
        RemoveActionBindings("ui_up");
        RemoveActionBindings("ui_down");
        RebindAction("move_left", CreateKey(Key.Left));
        RebindAction("move_right", CreateKey(Key.Right));
        RebindAction("move_up", CreateKey(Key.Up));
        RebindAction("move_down", CreateKey(Key.Down));
        RebindAction("focus_up", CreateKey(Key.Up));
        RebindAction("focus_down", CreateKey(Key.Down));
        RebindAction("focus_left", CreateKey(Key.Left));
        RebindAction("focus_right", CreateKey(Key.Right));
        RebindAction("ui_accept", CreateKey(Key.Enter), CreateKey(Key.Space), CreateKey(Key.Z));
        RebindAction("ui_cancel", CreateKey(Key.Escape), CreateKey(Key.X));
        RebindAction("advance_dialogue", CreateKey(Key.Enter), CreateKey(Key.Space), CreateKey(Key.Z));
        RebindAction("skip_dialogue_scrolling", CreateKey(Key.Shift), CreateKey(Key.X));
        RebindAction("advance_and_skip_dialogue_scrolling", CreateKey(Key.Ctrl), CreateKey(Key.C));
        RebindAction("jump", CreateKey(Key.Space), CreateKey(Key.Z));
        RebindAction("slow_gem", CreateKey(Key.Shift), CreateKey(Key.X));
        RebindAction("run", CreateKey(Key.Shift), CreateKey(Key.X));
        RebindAction("combat_redo", CreateKey(Key.R)); //Temporary moves, in game this will change
        RebindAction("combat_confirm", CreateKey(Key.Z), CreateKey(Key.Space), CreateKey(Key.Enter));
        RebindAction("interact", CreateKey(Key.Z), CreateKey(Key.Space), CreateKey(Key.Enter));
        RebindAction("flip_attack_pattern", CreateKey(Key.F));
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        GetViewport().GuiDisableInput = false;
        SetUiMouseFilter(Control.MouseFilterEnum.Ignore);
        GameManager.mostRecentKeyboardInputType = InputType.KeyboardOnly;
        GD.Print("Input Type: Keyboard Only");
    }

    private void ApplyKeyboardAndMouseProfile()
    {
        // RebindAction("ui_left", CreateKey(Key.A));
        // RebindAction("ui_right", CreateKey(Key.D));
        // RebindAction("ui_up", CreateKey(Key.W));
        // RebindAction("ui_down", CreateKey(Key.S));
        RemoveActionBindings("ui_left");
        RemoveActionBindings("ui_right");
        RemoveActionBindings("ui_up");
        RemoveActionBindings("ui_down");
        RebindAction("move_left", CreateKey(Key.A));
        RebindAction("move_right", CreateKey(Key.D));
        RebindAction("move_up", CreateKey(Key.W));
        RebindAction("move_down", CreateKey(Key.S));
        RebindAction("focus_up", CreateKey(Key.W));
        RebindAction("focus_down", CreateKey(Key.S));
        RebindAction("focus_left", CreateKey(Key.A));
        RebindAction("focus_right", CreateKey(Key.D));
        RebindAction("ui_accept", CreateKey(Key.Enter), CreateKey(Key.Space), CreateMouseButton(MouseButton.Left), CreateKey(Key.E));
        RebindAction("ui_cancel", CreateKey(Key.Escape));
        RebindAction("advance_dialogue", CreateKey(Key.Enter), CreateKey(Key.Space), CreateMouseButton(MouseButton.Left), CreateKey(Key.E));
        RebindAction("skip_dialogue_scrolling", CreateKey(Key.Shift), CreateMouseButton(MouseButton.Right), CreateKey(Key.Q));
        RebindAction("advance_and_skip_dialogue_scrolling", CreateKey(Key.Ctrl), CreateMouseButton(MouseButton.Middle), CreateKey(Key.F));
        RebindAction("jump", CreateKey(Key.Space));
        RebindAction("slow_gem", CreateKey(Key.Shift));
        RebindAction("run", CreateKey(Key.Shift));
        RebindAction("combat_redo", CreateKey(Key.R)); //Temporary moves, in game this will change
        RebindAction("combat_confirm", CreateKey(Key.Space), CreateKey(Key.Enter), CreateMouseButton(MouseButton.Left));
        RebindAction("interact", CreateKey(Key.E));
        RebindAction("flip_attack_pattern", CreateKey(Key.F));
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetViewport().GuiDisableInput = false;
        SetUiMouseFilter(Control.MouseFilterEnum.Stop);
        GameManager.mostRecentKeyboardInputType = InputType.KeyboardAndMouse;
        GD.Print("Input Type: Keyboard and Mouse");
    }

    private void ApplyControllerProfile()
    {
        RebindAction("ui_left", CreateJoyButton(JoyButton.DpadLeft), CreateJoyMotion(JoyAxis.LeftX, -1f));
        RebindAction("ui_right", CreateJoyButton(JoyButton.DpadRight), CreateJoyMotion(JoyAxis.LeftX, 1f));
        RebindAction("ui_up", CreateJoyButton(JoyButton.DpadUp), CreateJoyMotion(JoyAxis.LeftY, -1f));
        RebindAction("ui_down", CreateJoyButton(JoyButton.DpadDown), CreateJoyMotion(JoyAxis.LeftY, 1f));
        RebindAction("move_left", CreateJoyButton(JoyButton.DpadLeft));
        RebindAction("move_right", CreateJoyButton(JoyButton.DpadRight));
        RebindAction("move_up", CreateJoyButton(JoyButton.DpadUp));
        RebindAction("move_down", CreateJoyButton(JoyButton.DpadDown));
        RebindAction("focus_up", CreateJoyButton(JoyButton.DpadUp), CreateJoyMotion(JoyAxis.LeftY, -1f));
        RebindAction("focus_down", CreateJoyButton(JoyButton.DpadDown), CreateJoyMotion(JoyAxis.LeftY, 1f));
        RebindAction("focus_left", CreateJoyButton(JoyButton.DpadLeft), CreateJoyMotion(JoyAxis.LeftX, -1f));
        RebindAction("focus_right", CreateJoyButton(JoyButton.DpadRight), CreateJoyMotion(JoyAxis.LeftX, 1f));
        RebindAction("stick_left", CreateJoyMotion(JoyAxis.LeftX, -1f));
        RebindAction("stick_right", CreateJoyMotion(JoyAxis.LeftX, 1f));
        RebindAction("stick_up", CreateJoyMotion(JoyAxis.LeftY, -1f));
        RebindAction("stick_down", CreateJoyMotion(JoyAxis.LeftY, 1f));
        RebindAction("ui_accept", CreateJoyButton(JoyButton.A));
        RebindAction("ui_cancel", CreateJoyButton(JoyButton.B));
        RebindAction("advance_dialogue", CreateJoyButton(JoyButton.A));
        RebindAction("skip_dialogue_scrolling", CreateJoyButton(JoyButton.B));
        RebindAction("advance_and_skip_dialogue_scrolling", CreateJoyButton(JoyButton.Y));
        RebindAction("jump", CreateJoyButton(JoyButton.A));
        RebindAction("slow_gem", CreateJoyButton(JoyButton.B));
        RebindAction("run", CreateJoyButton(JoyButton.B));
        RebindAction("combat_redo", CreateJoyButton(JoyButton.LeftShoulder)); //Temporary moves, in game this will change
        RebindAction("combat_confirm", CreateJoyButton(JoyButton.A));
        RebindAction("interact", CreateJoyButton(JoyButton.A));
        RebindAction("flip_attack_pattern", CreateJoyButton(JoyButton.Y));
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        GetViewport().GuiDisableInput = false;
        SetUiMouseFilter(Control.MouseFilterEnum.Ignore);
        GD.Print("Input Type: Controller");
    }

    private void RebindAction(string actionName, params InputEvent[] events)
    {
        InputMap.ActionEraseEvents(actionName);

        foreach (InputEvent inputEvent in events)
        {
            InputMap.ActionAddEvent(actionName, inputEvent);
        }
    }

    private void RemoveActionBindings(string actionName)
    {
        InputMap.ActionEraseEvents(actionName);
    }

    private void SetUiMouseFilter(Control.MouseFilterEnum filter)
    {
        var root = GetTree().CurrentScene;
        if (root == null)
        {
            return;
        }

        ApplyMouseFilterRecursive(root, filter);
    }

    private void ApplyMouseFilterRecursive(Node node, Control.MouseFilterEnum filter)
    {
        if (node is Control control)
        {
            control.MouseFilter = filter;
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyMouseFilterRecursive(child, filter);
        }
    }

    private InputEventKey CreateKey(Key key)
    {
        return new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key
        };
    }

    private InputEventMouseButton CreateMouseButton(MouseButton button)
    {
        return new InputEventMouseButton
        {
            ButtonIndex = button
        };
    }

    private InputEventJoypadButton CreateJoyButton(JoyButton button)
    {
        return new InputEventJoypadButton
        {
            ButtonIndex = button
        };
    }

    private InputEventJoypadMotion CreateJoyMotion(JoyAxis axis, float value)
    {
        return new InputEventJoypadMotion
        {
            Axis = axis,
            AxisValue = value
        };
    }
}
