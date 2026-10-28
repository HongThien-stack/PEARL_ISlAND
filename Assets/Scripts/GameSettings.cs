using UnityEngine;

/// <summary>Persistent audio and display settings shared by the menu and game.</summary>
public static class GameSettings
{
    private const string Prefix = "PearlIsland.Settings.";

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.7f;
    public static float EffectsVolume { get; private set; } = 0.8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() => Load();

    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Master", 1f));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Music", 0.7f));
        EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Effects", 0.8f));
        AudioListener.volume = MasterVolume;
        if (PlayerPrefs.HasKey(Prefix + "Fullscreen"))
            ApplyDisplay(PlayerPrefs.GetInt(Prefix + "Fullscreen") == 1,
                PlayerPrefs.GetInt(Prefix + "Width", 1280), PlayerPrefs.GetInt(Prefix + "Height", 720));
    }

    public static void SetVolumes(float master, float music, float effects)
    {
        MasterVolume = Mathf.Clamp01(master);
        MusicVolume = Mathf.Clamp01(music);
        EffectsVolume = Mathf.Clamp01(effects);
        AudioListener.volume = MasterVolume;
        BackgroundMusic.SetVolume(MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "Master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "Effects", EffectsVolume);
        PlayerPrefs.Save();
    }

    public static void ApplyDisplay(bool fullscreen, int width, int height)
    {
        width = Mathf.Max(640, width);
        height = Mathf.Max(480, height);
        Screen.SetResolution(width, height,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "Width", width);
        PlayerPrefs.SetInt(Prefix + "Height", height);
        PlayerPrefs.Save();
    }
}
