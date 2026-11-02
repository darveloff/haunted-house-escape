using System;
using System.Collections;
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
    public Material fxMaterial; // unlit sprite material for world effects (set by the builder)

    [Header("Difficulty")]
    public float threatPerKey = 0.07f;   // ghosts get 7% faster/sharper per key
    public float huntThreatBonus = 0.15f; // and a bit more once the hunt starts

    public State CurrentState { get; private set; } = State.Title;
    public int KeysCollected { get; private set; }
    public float TimeLeft { get; private set; }
    public bool IsOver => CurrentState == State.Over;
    public bool HasAllKeys => KeysCollected >= keysRequired;

    // Run stats for the end screen.
    public int TimesSpotted { get; private set; }
    public float TimeUsed => timeLimit - TimeLeft;

    // 1 at the start, rising with every key; ghosts scale speed and senses by it.
    public float Threat => 1f + KeysCollected * threatPerKey + (HuntActive ? huntThreatBonus : 0f);
    // Final phase after the last key: the house wakes up and the ghosts converge on the exit.
    public bool HuntActive { get; private set; }
    public Vector3 ExitPosition { get; private set; }

    public event Action AllKeysCollected;
    public event Action HuntStarted;

    PlayerController player;
    GameHUD hud;
    MenuUI menu;

    void Awake()
    {
        Instance = this;
        TimeLeft = timeLimit;
        WorldFX.material = fxMaterial;
        LightFlicker.HuntMode = false;
        gameObject.AddComponent<AudioManager>();
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        var exit = FindAnyObjectByType<ExitZone>();
        if (exit != null) ExitPosition = exit.transform.position;

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
        FindAnyObjectByType<CameraFollow>()?.Shake(0.4f, 0.6f);
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
        ShowMessage($"FIND {keysRequired} KEYS AND ESCAPE\nDON'T LET THE GHOSTS SEE YOU", 5f);
        if (!SaveData.TutorialDone) gameObject.AddComponent<Tutorial>();
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

    public void CollectKey()
    {
        if (IsOver) return;
        KeysCollected++;
        if (HasAllKeys)
        {
            AllKeysCollected?.Invoke();
            StartHunt();
        }
        else
        {
            ShowMessage($"KEY FOUND!\n{keysRequired - KeysCollected} MORE TO GO", 2.5f);
            if (KeysCollected == keysRequired - 1) ShowMessage("ONE KEY LEFT...\nTHE GHOSTS ARE GETTING RESTLESS", 3f);
        }
    }

    // Last key: lights turn red, ghosts speed up and head for the exit, camera shows the open door.
    void StartHunt()
    {
        HuntActive = true;
        LightFlicker.HuntMode = true;
        ShowMessage("THE HOUSE IS AWAKE!\nRUN TO THE EXIT IN THE FOYER", 5f);
        hud.Flash(new Color(1f, 0.15f, 0.1f, 0.45f), 0.6f);
        AudioManager.Instance?.Play(AudioSynth.Spotted(), 0.45f);
        HuntStarted?.Invoke();
        StartCoroutine(ShowExitDoor());
    }

    // Brief slow-motion camera pan to the exit door as it opens.
    IEnumerator ShowExitDoor()
    {
        var cam = FindAnyObjectByType<CameraFollow>();
        if (cam == null) yield break;
        cam.FocusOn(ExitPosition, 1.8f);
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(1.8f);
        if (CurrentState == State.Playing) Time.timeScale = 1f;
    }

    public void Win()
    {
        if (IsOver) return;
        CurrentState = State.Over;
        Time.timeScale = 1f;
        AudioManager.Instance?.Play(AudioSynth.WinJingle(), 0.45f);
        SaveData.TutorialDone = true;
        int stars = SaveData.StarsFor(TimeUsed, TimesSpotted);
        bool newBest = SaveData.RecordWin(TimeUsed, stars);
        hud.SetVisible(false);
        menu.ShowEscaped(stars, newBest);
    }

    public void LoseCaught(GhostKind catcher)
    {
        if (IsOver) return;
        CurrentState = State.Over;
        Time.timeScale = 1f;
        AudioManager.Instance?.Play(AudioSynth.Caught(), 0.5f);
        FindAnyObjectByType<CameraFollow>()?.Shake(0.7f, 0.5f);
        hud.Flash(new Color(1f, 0.1f, 0.08f, 0.7f), 0.5f);
        StartCoroutine(ShowCaughtAfterFlash(catcher));
    }

    // Let the shake and red flash land before the end screen covers the game.
    IEnumerator ShowCaughtAfterFlash(GhostKind catcher)
    {
        yield return new WaitForSecondsRealtime(0.45f);
        hud.SetVisible(false);
        menu.ShowCaught(catcher);
    }
}
