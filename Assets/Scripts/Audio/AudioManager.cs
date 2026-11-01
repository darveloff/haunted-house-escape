using UnityEngine;

// 2D game audio: ambient drone, heartbeat that speeds up with danger, one-shot effects.
// Ghost moans are 3D and live on each ghost (GhostAI).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public float masterVolume = 1f;

    AudioSource ambient, sfx, steps;
    GhostAI[] ghosts;
    float nextBeat;
    float lastSpotted = -99f;

    void Awake()
    {
        Instance = this;
        ambient = NewSource(true);
        ambient.clip = AudioSynth.Drone();
        ambient.volume = 0.12f;
        ambient.Play();
        sfx = NewSource(false);
        steps = NewSource(false);

        // Warm up the synth so the first chase doesn't hitch.
        AudioSynth.Heartbeat(); AudioSynth.Moan(0); AudioSynth.Moan(1); AudioSynth.Shriek();
        AudioSynth.Spotted(); AudioSynth.KeyChime(); AudioSynth.RadarPing();
        for (int i = 0; i < 3; i++) AudioSynth.Footstep(i);
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    void Update()
    {
        AudioListener.volume = masterVolume;
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameManager.State.Playing) return;

        if (ghosts == null) ghosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        float danger = 0f;
        foreach (var g in ghosts)
            if (g != null) danger = Mathf.Max(danger, g.Alertness);

        // Heartbeat only when a ghost is actually onto you (searching or chasing).
        if (danger >= 0.6f && Time.time >= nextBeat)
        {
            sfx.PlayOneShot(AudioSynth.Heartbeat(), Mathf.Lerp(0.15f, 0.35f, danger));
            nextBeat = Time.time + Mathf.Lerp(0.95f, 0.6f, danger);
        }
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfx.PlayOneShot(clip, volume);
    }

    public void Footstep(bool sprinting)
    {
        steps.pitch = Random.Range(0.9f, 1.1f) * (sprinting ? 1.1f : 1f);
        steps.PlayOneShot(AudioSynth.Footstep(Random.Range(0, 3)), sprinting ? 0.22f : 0.12f);
    }

    public void Spotted()
    {
        if (Time.time - lastSpotted < 3f) return;
        lastSpotted = Time.time;
        Play(AudioSynth.Spotted(), 0.3f);
    }

    public void Click() => Play(AudioSynth.Click(), 0.25f);
}
