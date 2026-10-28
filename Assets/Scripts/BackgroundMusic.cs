using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Plays menu music and switches to game music after the game scene loads.</summary>
public sealed class BackgroundMusic : MonoBehaviour
{
    private static BackgroundMusic instance;

    private AudioSource source;
    private AudioClip gameClip;
    private bool waitingForGame;

    public static BackgroundMusic GetOrCreate()
    {
        if (instance != null)
            return instance;

        GameObject musicObject = new GameObject("Background Music");
        instance = musicObject.AddComponent<BackgroundMusic>();
        DontDestroyOnLoad(musicObject);
        return instance;
    }

    public static void SetVolume(float volume)
    {
        if (instance != null && instance.source != null)
            instance.source.volume = volume;
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = GameSettings.MusicVolume;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    public void PlayMenu(AudioClip menuClip, AudioClip inGameClip)
    {
        waitingForGame = false;
        gameClip = inGameClip;
        Play(menuClip);
    }

    public void StartGameWhenLoaded()
    {
        waitingForGame = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!waitingForGame || scene.name != "PixelLabMap")
            return;

        waitingForGame = false;
        Play(gameClip);
    }

    private void Play(AudioClip clip)
    {
        source.Stop();
        source.clip = clip;
        if (clip != null)
            source.Play();
    }
}
