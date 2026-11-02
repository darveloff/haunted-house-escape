using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Visual personality for each ghost type, plus a "!" / "?" speech bubble for everyone.
//  Wisp      blue motes orbiting it, faster when chasing
//  Wraith    red afterimages streaming behind it while it sprints
//  Watcher   translucent eye scanning above its head + faint vision cone on the floor
//  Screamer  freezes, shakes and blasts sound-wave rings when it shrieks
[RequireComponent(typeof(GhostAI))]
public class GhostFX : MonoBehaviour
{
    public Material spriteMaterial;

    GhostAI ai;
    Camera cam;
    Color color;

    // bubble
    RectTransform bubble;
    Text bubbleText;
    float bubbleUntil;
    float bubbleScale;
    float bubbleTarget;

    // wisp
    readonly List<SpriteRenderer> motes = new List<SpriteRenderer>();
    float moteAngle;

    // wraith
    float nextAfterimage;

    // watcher
    SpriteRenderer eye;
    SpriteRenderer cone;

    // screamer
    Vector3 visualScale;

    struct Fading
    {
        public SpriteRenderer sr;
        public float born, life, startScale, endScale, startAlpha;
        public bool billboard;
    }
    readonly List<Fading> fading = new List<Fading>();

    void Start()
    {
        ai = GetComponent<GhostAI>();
        cam = Camera.main;
        color = ai.Profile.color;
        if (ai.visual != null) visualScale = ai.visual.localScale;

        BuildBubble();
        switch (ai.kind)
        {
            case GhostKind.Wisp:
                for (int i = 0; i < 3; i++)
                    motes.Add(NewSprite("Mote", UIStyle.GlowIcon, new Color(color.r, color.g, color.b, 0.9f), 0.3f, transform));
                break;
            case GhostKind.Watcher:
                eye = NewSprite("WatcherEye", UIStyle.EyeIcon, new Color(0.85f, 0.85f, 0.9f, 0.5f), 1.6f, transform);
                eye.transform.localPosition = new Vector3(0, 3.3f, 0);
                cone = NewSprite("VisionCone", UIStyle.ConeSprite(ai.Profile.viewAngle), new Color(color.r, color.g, color.b, 0.12f), 1f, transform);
                cone.transform.localPosition = new Vector3(0, 0.06f, 0);
                cone.transform.localRotation = Quaternion.Euler(90, 0, 0);
                cone.transform.localScale = Vector3.one * ai.Profile.sightRange;
                break;
        }

        ai.StateChanged += OnStateChanged;
        ai.Screamed += OnScreamed;
    }

    void OnDestroy()
    {
        if (ai == null) return;
        ai.StateChanged -= OnStateChanged;
        ai.Screamed -= OnScreamed;
    }

    // ---------------------------------------------------------------- events

    void OnStateChanged(GhostState state)
    {
        if (state == GhostState.Chase)
        {
            ShowBubble("!", UIStyle.Danger, 1.6f);
            if (ai.kind == GhostKind.Wraith) Ring(transform.position + Vector3.up * 0.08f, flat: true, 0.6f, 5f, 0.45f, 0.7f);
        }
        else if (state == GhostState.Search) ShowBubble("?", UIStyle.Gold, ai.searchTime);
        else bubbleUntil = 0f;
    }

    void OnScreamed()
    {
        ShowBubble("!!!", color, 1.4f);
        Vector3 head = transform.position + Vector3.up * 1.6f;
        for (int i = 0; i < 4; i++)
            Ring(head, flat: false, 0.6f, 7f, 0.7f + i * 0.12f, 0.8f, delay: i * 0.18f);
        // Floor ring shows how far the call reaches.
        Ring(transform.position + Vector3.up * 0.1f, flat: true, 1f, ai.screamAlertRadius * 2f, 1.3f, 0.5f);
    }

    // ---------------------------------------------------------------- per frame

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        float t = Time.time;

        UpdateBubble();

        switch (ai.kind)
        {
            case GhostKind.Wisp:
                moteAngle += Time.deltaTime * (ai.IsChasing ? 420f : 120f);
                for (int i = 0; i < motes.Count; i++)
                {
                    float a = (moteAngle + i * 120f) * Mathf.Deg2Rad;
                    motes[i].transform.position = transform.position + new Vector3(Mathf.Cos(a) * 0.8f, 1.1f + Mathf.Sin(t * 2.3f + i * 2f) * 0.35f, Mathf.Sin(a) * 0.8f);
                    Billboard(motes[i].transform);
                }
                break;

            case GhostKind.Wraith:
                if (ai.IsChasing && ai.Agent != null && ai.Agent.velocity.magnitude > 2.5f && t >= nextAfterimage)
                {
                    nextAfterimage = t + 0.05f;
                    var sr = NewSprite("Afterimage", UIStyle.GhostIconFor(ai.kind), new Color(color.r, color.g, color.b, 0.5f), 2.2f, null);
                    sr.transform.position = transform.position + Vector3.up * 1f;
                    fading.Add(new Fading { sr = sr, born = t, life = 0.35f, startScale = 2.2f, endScale = 1.6f, startAlpha = 0.5f, billboard = true });
                }
                break;

            case GhostKind.Watcher:
                bool alert = ai.CurrentState != GhostState.Patrol;
                var target = alert ? new Color(color.r, color.g, color.b, 0.95f) : new Color(0.85f, 0.85f, 0.9f, 0.45f);
                eye.color = Color.Lerp(eye.color, target, Time.deltaTime * 5f);
                // Slow look left/right, occasional blink.
                float look = alert ? 0f : Mathf.Sin(t * 0.5f) * 0.35f;
                float blink = Mathf.Repeat(t, 5.3f) < 0.12f ? 0.15f : 1f;
                eye.transform.position = transform.position + Vector3.up * (3.3f + Mathf.Sin(t * 1.2f) * 0.08f) + cam.transform.right * look;
                eye.transform.localScale = new Vector3(1f, blink, 1f) * (alert ? 2.1f : 1.6f);
                Billboard(eye.transform);
                cone.color = Color.Lerp(cone.color, new Color(color.r, color.g, color.b, alert ? 0.28f : 0.12f), Time.deltaTime * 4f);
                break;

            case GhostKind.Screamer:
                if (ai.visual != null)
                {
                    float shake = ai.IsScreaming ? 1f : 0f;
                    ai.visual.localScale = visualScale * (1f + shake * 0.12f * Mathf.Sin(t * 45f));
                    ai.visual.localRotation = Quaternion.Euler(0, 0, shake * 6f * Mathf.Sin(t * 60f));
                }
                break;
        }

        UpdateFading(t);
    }

    void UpdateFading(float t)
    {
        for (int i = fading.Count - 1; i >= 0; i--)
        {
            var f = fading[i];
            float age = t - f.born;
            if (age < 0f)
            {
                f.sr.enabled = false;
                continue;
            }
            f.sr.enabled = true;
            float k = age / f.life;
            if (k >= 1f)
            {
                Destroy(f.sr.gameObject);
                fading.RemoveAt(i);
                continue;
            }
            var c = f.sr.color;
            c.a = f.startAlpha * (1f - k) * (1f - k);
            f.sr.color = c;
            f.sr.transform.localScale = Vector3.one * Mathf.Lerp(f.startScale, f.endScale, 1f - (1f - k) * (1f - k));
            if (f.billboard) Billboard(f.sr.transform);
        }
    }

    // ---------------------------------------------------------------- helpers

    void Ring(Vector3 pos, bool flat, float from, float to, float life, float alpha, float delay = 0f)
    {
        var sr = NewSprite("Ring", UIStyle.RingIcon, new Color(color.r, color.g, color.b, alpha), from, null);
        sr.transform.position = pos;
        if (flat) sr.transform.rotation = Quaternion.Euler(90, 0, 0);
        // RingIcon is 0.64 units wide at scale 1; scale so `to` is the final diameter.
        float unit = 1f / 0.64f;
        fading.Add(new Fading { sr = sr, born = Time.time + delay, life = life, startScale = from * unit, endScale = to * unit, startAlpha = alpha, billboard = !flat });
    }

    SpriteRenderer NewSprite(string name, Sprite sprite, Color c, float scale, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.localScale = Vector3.one * scale;
        return sr;
    }

    void Billboard(Transform t)
    {
        if (cam != null) t.rotation = cam.transform.rotation;
    }

    // World-space speech bubble above the ghost, same look as the HUD.
    void BuildBubble()
    {
        var go = new GameObject("Bubble", typeof(Canvas));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, 3.6f, 0);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        var root = (RectTransform)go.transform;
        root.sizeDelta = new Vector2(150, 120);
        root.localScale = Vector3.one * 0.01f;

        var img = UIStyle.Bubble("Body", root, new Vector2(130, 100));
        img.rectTransform.anchoredPosition = new Vector2(0, 10);
        bubbleText = UIStyle.Label("Text", img.transform, 72, UIStyle.Ink);
        UIStyle.Stretch(bubbleText.rectTransform);
        bubble = img.rectTransform;
        bubble.localScale = Vector3.zero;
    }

    void ShowBubble(string text, Color textColor, float seconds)
    {
        bubbleText.text = text;
        bubbleText.color = Color.Lerp(textColor, UIStyle.Ink, 0.15f);
        bubbleUntil = Time.time + seconds;
        bubbleScale = 0f;
    }

    void UpdateBubble()
    {
        bubbleTarget = Time.time < bubbleUntil ? 1f : 0f;
        // Springy pop: overshoot on the way in.
        bubbleScale = Mathf.MoveTowards(bubbleScale, bubbleTarget, Time.deltaTime * 6f);
        float s = bubbleTarget > 0f ? 1f + 0.25f * Mathf.Sin(bubbleScale * Mathf.PI) : bubbleScale;
        bubble.localScale = Vector3.one * (bubbleTarget > 0f ? bubbleScale * s : bubbleScale);
        bubble.parent.rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
    }
}
