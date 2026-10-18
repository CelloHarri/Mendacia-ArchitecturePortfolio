using Godot;
using System;
using System.Collections.Generic;

public partial class SoundManager : Node
{
    public static SoundManager instance;

    private AudioStreamPlayer musicPlayer;
    private AudioStreamPlayer sfxPlayer;
    private AudioStreamPolyphonic sfxPolyphonic;
    private AudioStreamPlaybackPolyphonic sfxPlayback;

    private bool _fading = false;

    private Dictionary<string, AudioStream> sfxDict = new();
    private Dictionary<string, AudioStream> musicDict = new();

    public override void _Ready()
    {
        instance = this;

        musicPlayer = new AudioStreamPlayer();
        musicPlayer.Bus = "Music";
        AddChild(musicPlayer);

        sfxPolyphonic = new AudioStreamPolyphonic();
        sfxPlayer = new AudioStreamPlayer();
        sfxPlayer.Stream = sfxPolyphonic;
        sfxPlayer.Bus = "SoundEffects";
        AddChild(sfxPlayer);
        sfxPlayer.Play();
        sfxPlayback = (AudioStreamPlaybackPolyphonic)sfxPlayer.GetStreamPlayback();

        LoadSounds("res://Sounds/SFX/", sfxDict);
        LoadSounds("res://Sounds/Music/", musicDict);
        foreach (var stream in musicDict.Values)
            SetLoopEnabled(stream, true);

        ApplyAudioSettings(GameManager.audioSettings);
    }

    public void ApplyAudioSettings(AudioSettings s)
    {
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(s.masterVolume));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Music"), Mathf.LinearToDb(s.musicVolume));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("SoundEffects"), Mathf.LinearToDb(s.soundEffectsVolume));
    }

    private void LoadSounds(string path, Dictionary<string, AudioStream> dict)
    {
        using var dir = DirAccess.Open(path);
        if (dir == null) return;

        dir.ListDirBegin();
        string fileName = dir.GetNext();

        while (fileName != "")
        {
            if (fileName != "." && fileName != ".." && !dir.CurrentIsDir())
            {
                string fullPath = path + fileName;
                // strip import sidecar files
                if (!fileName.EndsWith(".import"))
                {
                    var stream = ResourceLoader.Load<AudioStream>(fullPath);
                    if (stream != null)
                    {
                        string key = System.IO.Path.GetFileNameWithoutExtension(fileName);
                        dict[key] = stream;
                    }
                }
            }
            fileName = dir.GetNext();
        }
    }

    /// <summary>Plays an already-loaded AudioStream directly (used for character voice blips).</summary>
    public void PlaySFXStream(AudioStream stream, float volumeDb = 0f)
    {
        sfxPlayback.PlayStream(stream, 0f, volumeDb);
    }

    public void PlaySFX(string name, float volumeDb = 0f)
    {
        if (!sfxDict.TryGetValue(name, out var stream))
        {
            GD.PrintErr($"SoundManager: SFX '{name}' not found.");
            return;
        }
        sfxPlayback.PlayStream(stream, 0f, volumeDb);
    }

    public void PlayMusic(string name, bool loop = true, float volumeDb = 0f)
    {
        if (!musicDict.TryGetValue(name, out var stream))
        {
            GD.PrintErr($"SoundManager: Music '{name}' not found.");
            return;
        }

        SetLoopEnabled(stream, loop);

        musicPlayer.VolumeDb = volumeDb;
        musicPlayer.Stream = stream;
        musicPlayer.Play();
    }

    private static void SetLoopEnabled(AudioStream stream, bool loop)
    {
        if (stream is AudioStreamOggVorbis ogg)
            ogg.Loop = loop;
        else if (stream is AudioStreamMP3 mp3)
            mp3.Loop = loop;
        else if (stream is AudioStreamWav wav)
            wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
    }

    public void StopMusic()
    {
        _fading = false;
        musicPlayer.Stop();
    }

    public async void FadeMusic(float duration)
    {
        _fading = true;
        float start = musicPlayer.VolumeDb;
        double elapsed = 0;
        while (elapsed < duration && _fading)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
            musicPlayer.VolumeDb = Mathf.Lerp(start, -80f, (float)(elapsed / duration));
        }
        if (_fading) { musicPlayer.Stop(); musicPlayer.VolumeDb = start; }
        _fading = false;
    }
}
