using Godot;
using System;
using System.Reflection.Metadata;

public enum GameState
{
    World,
    Battle
}

public enum InputType
{
    Controller, //Controller Input (Gamepad)
    KeyboardOnly, //KeyBoard Only Input (ZXCV + Arrow Keys [Default Controls]) 
    KeyboardAndMouse //KeyBoard and Mouse Input (WASD + Mouse [Default Controls])
}

public enum HoverDirectionOnUpInput
{
    NorthEast,
    NorthWest,
}

public partial class GameManager : Node
{
    public static string gameVersion { get; } = "0.0.0 - No Build Available"; //Game Version

    public static bool pauseAllowed { get; set; } //Is Pause Allowed

    public static AudioSettings audioSettings { get; set; } = new AudioSettings();

    public static GameState currentGameState { get; set; } = GameState.World;
    public static InputType currentInputType { get; set; } = InputType.KeyboardAndMouse; //Default Input Type with WASD and Mouse
    public static InputType mostRecentKeyboardInputType { get; set; } = InputType.KeyboardAndMouse; //Default Input Type with WASD and Mouse
    public static HoverDirectionOnUpInput currentHoverDirectionOnUpInput { get; set; } = HoverDirectionOnUpInput.NorthEast; //Default Hover Direction on Up Input

    public static GameSaveData currentSaveData
    { get; set; } = new GameSaveData();
    public static GameSaveData saveSlot1 { get; set; } = new GameSaveData();
    public static GameSaveData saveSlot2 { get; set; } = new GameSaveData();
    public static GameSaveData saveSlot3 { get; set; } = new GameSaveData();

    public override void _Ready()
    {
        PortableCompressedTexture2D.SetKeepAllCompressedBuffers(true);

        GD.Print("Game Manager Ready");
        GD.Print("Game Version: " + gameVersion);
        GD.Print("Input Type: " + currentInputType.ToString());

        if (!FileAccess.FileExists("user://config.json"))
        {
            GameConfig.SaveConfig(new GameConfigData());
        }

        audioSettings = GameConfig.LoadConfig().audioSettings;

        LoadGameData();
        InputManager.ApplyCurrentInputProfile();
    }

    public static void SaveGameData()
    {
        //Placeholder for saving game data logic
        //Saves currentSaveData to appropriate save slot
        GD.Print("Saving Game Data...");
    }

    public static void LoadGameData()
    {
        //Placeholder for loading game data logic
        //Loads All Data when Game Starts
        GD.Print("Loading Game Data...");
    }


}