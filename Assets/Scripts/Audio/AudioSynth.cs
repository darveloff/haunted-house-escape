using System.Collections.Generic;
using UnityEngine;

// Every sound in the game is synthesised here at startup, so the project needs no audio files.
public static class AudioSynth
{
    const int Rate = 44100;
    static readonly System.Random rng = new System.Random(1337);
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

    static AudioClip Make(string name, float[] data)
    {
        // Normalise to a safe peak.
        float peak = 0.0001f;
        foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.6f / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;

        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        cache[name] = clip;
        return clip;
    }

    static bool TryGet(string name, out AudioClip clip) => cache.TryGetValue(name, out clip) && clip != null;

    public static AudioClip Footstep(int variant)
    {
        string name = "footstep" + variant;
        if (TryGet(name, out var c)) return c;
        int n = (int)(Rate * 0.16f);
        var d = new float[n];
        float lp = 0f, cutoff = 0.08f + variant * 0.04f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            lp += cutoff * (Noise() - lp);
            float thump = Mathf.Sin(2f * Mathf.PI * (85f + variant * 12f) * t) * Mathf.Exp(-t * 55f);
            d[i] = (lp * 1.6f + thump * 0.7f) * Mathf.Exp(-t * 32f);
        }
        return Make(name, d);
    }

    public static AudioClip Heartbeat()
    {
        if (TryGet("heartbeat", out var c)) return c;
        int n = (int)(Rate * 0.6f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            d[i] = Beat(t, 1f) + Beat(t - 0.24f, 0.7f);
        }
        return Make("heartbeat", d);

        static float Beat(float t, float amp)
        {
            if (t < 0f) return 0f;
            float f = Mathf.Lerp(72f, 42f, Mathf.Clamp01(t * 8f));
            return amp * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 16f) * Smooth(0f, 0.005f, t);
        }
    }

    public static AudioClip Moan(int variant)
    {
        string name = "moan" + variant;
        if (TryGet(name, out var c)) return c;
        float dur = 2.6f + variant * 0.5f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float baseF = variant == 0 ? 150f : 115f;
        float phase = 0f, breath = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f0 = baseF * (1f - 0.25f * t / dur)
                       + 30f * Mathf.Sin(2f * Mathf.PI * 0.4f * t)
                       + 6f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
            phase += 2f * Mathf.PI * f0 / Rate;
            float s = 0f;
            for (int k = 1; k <= 7; k++)
            {
                float fk = k * f0;
                float formant = Mathf.Exp(-Mathf.Pow((fk - 520f) / 320f, 2f)) + 0.25f * Mathf.Exp(-Mathf.Pow((fk - 1100f) / 400f, 2f));
                s += Mathf.Sin(phase * k) * formant / k;
            }
            breath += 0.05f * (Noise() - breath);
            float env = Smooth(0f, 0.7f, t) * (1f - Smooth(dur - 1.1f, dur, t));
            d[i] = (s + breath * 1.5f) * env;
        }
        return Make(name, d);
    }

    public static AudioClip Shriek()
    {
        if (TryGet("shriek", out var c)) return c;
        int n = (int)(Rate * 1.2f);
        var d = new float[n];
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = 1400f + 500f * Mathf.Sin(2f * Mathf.PI * 9f * t) - 600f * t;
            phase += 2f * Mathf.PI * f / Rate;
            lp += 0.35f * (Noise() - lp);
            float env = Smooth(0f, 0.03f, t) * Mathf.Exp(-t * 2.2f);
            d[i] = (Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 1.51f) * 0.3f + lp * 0.5f) * env;
        }
        return Make("shriek", d);
    }

    public static AudioClip Drone()
    {
        if (TryGet("drone", out var c)) return c;
        // Frequencies with whole cycles in 8s so the loop is seamless.
        const float dur = 8f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float[] freqs = { 55f, 55.5f, 82.5f, 110.25f };
        float[] amps = { 1f, 0.8f, 0.45f, 0.2f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int k = 0; k < freqs.Length; k++) s += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * amps[k];
            float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / dur);
            d[i] = s * swell;
        }
        return Make("drone", d);
    }

    public static AudioClip KeyChime()
    {
        if (TryGet("chime", out var c)) return c;
        return Make("chime", Arpeggio(new[] { 880f, 1108.7f, 1318.5f, 1760f }, 0.07f, 1.1f));
    }

    public static AudioClip WinJingle()
    {
        if (TryGet("win", out var c)) return c;
        return Make("win", Arpeggio(new[] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 0.14f, 2f));
    }

    static float[] Arpeggio(float[] notes, float gap, float dur)
    {
        int n = (int)(Rate * dur);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float tk = t - k * gap;
                if (tk < 0f) continue;
                float e = Mathf.Exp(-tk * 4.5f) * Smooth(0f, 0.004f, tk);
                s += (Mathf.Sin(2f * Mathf.PI * notes[k] * tk) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[k] * tk)) * e;
            }
            d[i] = s;
        }
        return d;
    }

    public static AudioClip Spotted()
    {
        if (TryGet("spotted", out var c)) return c;
        int n = (int)(Rate * 1.1f);
        var d = new float[n];
        float[] f = { 233f, 246.9f, 311f, 466f };
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            foreach (var fk in f)
            {
                float p = Mathf.Repeat(fk * t, 1f);
                s += (p * 2f - 1f) * 0.4f + Mathf.Sin(2f * Mathf.PI * fk * t) * 0.6f; // saw + sine
            }
            lp += 0.2f * (Noise() - lp);
            float env = Smooth(0f, 0.01f, t) * Mathf.Exp(-t * 3.2f);
            d[i] = (s * 0.5f + lp * Mathf.Exp(-t * 20f) * 2f) * env;
        }
        return Make("spotted", d);
    }

    public static AudioClip Caught()
    {
        if (TryGet("caught", out var c)) return c;
        int n = (int)(Rate * 1.8f);
        var d = new float[n];
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(950f, 160f, t / 1.8f) + 60f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            phase += 2f * Mathf.PI * f / Rate;
            lp += 0.3f * (Noise() - lp);
            float env = Smooth(0f, 0.02f, t) * (1f - Smooth(1.2f, 1.8f, t));
            d[i] = (Mathf.Sin(phase) * 0.6f + Mathf.Sign(Mathf.Sin(phase * 0.5f)) * 0.15f + lp * 0.6f) * env;
        }
        return Make("caught", d);
    }

    public static AudioClip DoorRumble()
    {
        if (TryGet("door", out var c)) return c;
        int n = (int)(Rate * 2.2f);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            lp += 0.02f * (Noise() - lp);
            float env = Smooth(0f, 0.3f, t) * (1f - Smooth(1.6f, 2.2f, t));
            d[i] = (lp * 4f + Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.4f) * env;
        }
        return Make("door", d);
    }

    public static AudioClip RadarPing()
    {
        if (TryGet("ping", out var c)) return c;
        int n = (int)(Rate * 0.6f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 1250f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 1875f * t)) * Mathf.Exp(-t * 7f) * Smooth(0f, 0.003f, t);
        }
        return Make("ping", d);
    }

    public static AudioClip Click()
    {
        if (TryGet("click", out var c)) return c;
        int n = (int)(Rate * 0.06f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 70f);
        }
        return Make("click", d);
    }
}
