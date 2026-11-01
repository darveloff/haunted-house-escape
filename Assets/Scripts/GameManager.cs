using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Owns the round: title -> playing <-> paused -> over. Tracks keys and the timer.
// GameHUD draws the in-game HUD, MenuUI the title/pause/game-over screens.
public class GameManager : MonoBehaviour
{
    public enum State { Title, Playing, Paused, Over }

#if UNITY_WEBGL && !UNITY_EDITOR
    public static bool CanQuit => false; // browsers can't close the page
#else
    public static bool CanQuit => true;
#endif

    public static GameManager Instance { get; private set; }
    static bool skipTitle; // set by Restart so "play again" goes straight back in

    // Statics survive between Play sessions when domain reload is off; reset them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => skipTitle = false;

    public int keysRequired = 5;
    public float timeLimit = 420f;

    public State CurrentState { get; private set; } = State.Title;
    public int KeysCollected { get; private set; }
    public float TimeLeft { get; private set; }
    public bool IsOver => CurrentState == State.Over;
    public bool HasAllKeys => KeysCollected >= keysRequired;

    // Run stats for the end screen.
    public int TimesSpotted { get; private set; }
    public int TimesHidden { get; private set; }
    public float TimeUsed => timeLimit - TimeLeft;

    public event Action AllKeysCollected;

    PlayerController player;
    GameHUD hud;
    MenuUI menu;

    void Awake()
    {
        Instance = this;
        TimeLeft = timeLimit;
        gameObject.AddComponent<AudioManager>();
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        hud = gameObject.AddComponent<GameHUD>();
        hud.Build(this);
        menu = gameObject.AddComponent<MenuUI>();
        menu.Build(this);

        if (skipTitle) StartGame();
        else ShowTitle();
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        switch (CurrentState)
        {
            case State.Title:
                if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) StartGame();
                else if (k.qKey.wasPressedThisFrame && CanQuit) Quit();
                break;
            case State.Playing:
                if (k.escapeKey.wasPressedThisFrame) Pause();
                TickTimer();
                break;
            case State.Paused:
                if (k.escapeKey.wasPressedThisFrame) Resume();
                else if (k.rKey.wasPressedThisFrame) Restart();
                else if (k.mKey.wasPressedThisFrame) MainMenu();
                else if (k.qKey.wasPressedThisFrame && CanQuit) Quit();
                break;
            case State.Over:
                if (k.rKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) Restart();
                else if (k.mKey.wasPressedThisFrame) MainMenu();
                break;
        }
    }

    void TickTimer()
    {
        TimeLeft -= Time.deltaTime;
        if (TimeLeft > 0f) return;
        TimeLeft = 0f;
        if (player != null) player.Die();
        if (IsOver) return;
        CurrentState = State.Over;
        AudioManager.Instance?.Play(AudioSynth.Spotted(), 0.4f);
        hud.SetVisible(false);
        menu.ShowTimeUp();
    }

    // ---------------------------------------------------------------- flow

    void ShowTitle()
    {
        CurrentState = State.Title;
        Time.timeScale = 0f;
        hud.SetVisible(false);
        menu.ShowTitle();
    }

    public void StartGame()
    {
        CurrentState = State.Playing;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        menu.HideAll();
        hud.SetVisible(true);
        ShowMessage($"FIND {keysRequired} KEYS AND ESCAPE\nHIDE FROM THE GHOSTS", 5f);
    }

    public void Pause()
    {
        if (CurrentState != State.Playing) return;
        CurrentState = State.Paused;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        menu.ShowPause();
    }

    public void Resume()
    {
        if (CurrentState != State.Paused) return;
        CurrentState = State.Playing;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        menu.HideAll();
    }

    public void Restart()
    {
        skipTitle = true;
        Reload();
    }

    public void MainMenu()
    {
        skipTitle = false;
        Reload();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Reload()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    // ---------------------------------------------------------------- gameplay events

    public void ShowMessage(string text, float seconds)
    {
        if (hud != null) hud.ShowMessage(text, seconds);
    }

    public void CountSpotted() => TimesSpotted++;
    public void CountHidden() => TimesHidden++;

    public void CollectKey()
    {
        if (IsOver) return;
        KeysCollected++;
        if (HasAllKeys)
        {
            ShowMessage("ALL KEYS FOUND!\nRUN TO THE EXIT IN THE FOYER", 5f);
            AllKeysCollected?.Invoke();
        }
        else
        {
            ShowMessage($"KEY FOUND!\n{keysRequired - KeysCollected} MORE TO GO", 2.5f);
        }
    }

    public void Win()
    {
        if (IsOver) return;
        CurrentState = State.Over;
        AudioManager.Instance?.Play(AudioSynth.WinJingle(), 0.45f);
        hud.SetVisible(false);
        menu.ShowEscaped();
    }

    public void LoseCaught(GhostKind catcher)
    {
        if (IsOver) return;
        CurrentState = State.Over;
        AudioManager.Instance?.Play(AudioSynth.Caught(), 0.5f);
        hud.SetVisible(false);
        menu.ShowCaught(catcher);
    }
}
