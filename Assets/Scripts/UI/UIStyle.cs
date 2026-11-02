using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Chunky rounded HUD look: dark pills with white outlines, white keycaps, bold rounded font.
// Sprites and icons are generated in code so the HUD needs no imported art.
public static class UIStyle
{
    public static readonly Color PanelFill = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    public static readonly Color Ink = new Color(0.1f, 0.1f, 0.12f, 1f);
    public static readonly Color Paper = new Color(0.97f, 0.97f, 0.98f, 1f);
    public static readonly Color Danger = new Color(1f, 0.32f, 0.28f, 1f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);

    static Font font;
    public static Font Font
    {
        get
        {
            if (font == null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browsers can't read system fonts; use Unity's built-in font.
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Rounded MT Bold", "Avenir Next", "Helvetica Neue", "Arial" }, 32);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
            }
            return font;
        }
    }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------- widgets

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static Image Panel(string name, Transform parent, bool light = false, int radius = 22, int border = 5)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = light ? RoundedSprite(radius, border, Paper, Ink) : RoundedSprite(radius, border, PanelFill, Paper);
        img.type = Image.Type.Sliced;
        var shadow = img.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.45f);
        shadow.effectDistance = new Vector2(0, -6);
        return img;
    }

    public static Text Label(string name, Transform parent, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool outline = false)
    {
        var text = Rect(name, parent).gameObject.AddComponent<Text>();
        text.font = Font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = align;
        text.lineSpacing = 1.1f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        if (outline)
        {
            var o = text.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(3, -3);
        }
        return text;
    }

    public static Image Icon(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    // White keycap followed by a label, e.g. [SPACE] ASK.
    public static RectTransform Prompt(Transform parent, string key, string action, out Text actionText)
    {
        var row = Rect("Prompt_" + key, parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var cap = Panel("Key", row, light: true, radius: 14, border: 4);
        var capLayout = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
        capLayout.padding = new RectOffset(16, 16, 8, 8);
        capLayout.childControlWidth = capLayout.childControlHeight = true;
        Label("Text", cap.transform, 26, Ink).text = key;

        actionText = Label("Action", row, 34, Paper, TextAnchor.MiddleLeft, outline: true);
        actionText.text = action;
        return row;
    }

    // Big bold text with a thick dark outline, like a sticker.
    public static Text Sticker(string name, Transform parent, int size, Color color, float rotation = 0f)
    {
        var text = Label(name, parent, size, color);
        text.supportRichText = true;
        // Each Outline multiplies the vertex count by 5, so two diagonal ones keep it thick but cheap.
        foreach (var d in new[] { new Vector2(5, -5), new Vector2(-5, 5) })
        {
            var o = text.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = d;
        }
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(0, -12);
        text.rectTransform.localEulerAngles = new Vector3(0, 0, rotation);
        return text;
    }

    // Dark pill with a white keycap on the left: [SPACE] PLAY. Grows on hover.
    public static RectTransform PillButton(Transform parent, string key, string label, Action onClick, float width = 460f)
    {
        var img = Panel("Pill_" + label, parent, radius: 26, border: 5);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(width, 92);
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 92;

        var button = img.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.Click();
            onClick();
        });
        img.gameObject.AddComponent<UIHover>();

        var cap = Panel("Key", rt, light: true, radius: 14, border: 4);
        var capRt = cap.rectTransform;
        capRt.anchorMin = capRt.anchorMax = capRt.pivot = new Vector2(0, 0.5f);
        capRt.anchoredPosition = new Vector2(20, 0);
        var capText = Label("Text", capRt, 26, Ink);
        capText.text = key;
        capRt.sizeDelta = new Vector2(Mathf.Max(64, capText.preferredWidth + 32), 58);
        Stretch(capText.rectTransform);

        var text = Label("Label", rt, 40, Paper, TextAnchor.MiddleLeft);
        text.text = label;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(capRt.sizeDelta.x + 44, 0);
        text.rectTransform.offsetMax = new Vector2(-20, 0);
        return rt;
    }

    // White speech bubble with a dark outline and a little tail.
    public static Image Bubble(string name, Transform parent, Vector2 size, float tailX = 0f)
    {
        var bubble = Panel(name, parent, light: true, radius: 30, border: 5);
        bubble.rectTransform.sizeDelta = size;
        var tail = Rect("Tail", bubble.rectTransform).gameObject.AddComponent<Image>();
        tail.sprite = RoundedSprite(6, 5, Paper, Ink);
        tail.type = Image.Type.Sliced;
        var tr = tail.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
        tr.sizeDelta = new Vector2(34, 34);
        tr.anchoredPosition = new Vector2(tailX, -4);
        tr.localEulerAngles = new Vector3(0, 0, 45);
        // Paper patch hides the bubble's border where the tail joins, so they read as one shape.
        var cover = Rect("TailCover", bubble.rectTransform).gameObject.AddComponent<Image>();
        cover.sprite = RoundedSprite(4, 0, Paper, Paper);
        cover.type = Image.Type.Sliced;
        var cr = cover.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(38, 22);
        cr.anchoredPosition = new Vector2(tailX, 11);
        return bubble;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // White keycap-style button.
    public static Button MakeButton(Transform parent, string label, Action onClick, float width)
    {
        var img = Panel("Button_" + label, parent, light: true, radius: 18, border: 5);
        img.rectTransform.sizeDelta = new Vector2(width, 80);
        var button = img.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.5f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.Click();
            onClick();
        });

        var text = Label("Text", img.transform, 36, Ink);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        text.text = label;
        return button;
    }

    // ---------------------------------------------------------------- generated sprites

    public static Sprite RoundedSprite(int radius, int border, Color fill, Color edge)
    {
        string key = $"round{radius}_{border}_{fill}_{edge}";
        if (cache.TryGetValue(key, out var s) && s != null && s.texture != null) return s;

        int size = radius * 2 + 4;
        var tex = NewTexture(size);
        var c = new Vector2(size / 2f, size / 2f);
        var half = new Vector2(size / 2f - 1f, size / 2f - 1f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundBox(new Vector2(x + 0.5f, y + 0.5f) - c, half, radius);
            float alpha = Mathf.Clamp01(0.5f - d);
            float t = Mathf.Clamp01(d + border + 0.5f);
            Color col = Color.Lerp(fill, edge, t);
            col.a *= alpha;
            tex.SetPixel(x, y, col);
        }
        tex.Apply();
        float b = radius + 2;
        s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        cache[key] = s;
        return s;
    }

    static float RoundBox(Vector2 p, Vector2 b, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - b + new Vector2(r, r);
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    public static Sprite HourglassIcon => ShapeIcon("hourglass", (x, y) =>
    {
        float ay = Mathf.Abs(y);
        if (ay > 0.78f && ay < 0.95f && Mathf.Abs(x) < 0.72f) return true;
        return ay <= 0.78f && Mathf.Abs(x) < 0.08f + 0.55f * ay / 0.78f;
    });

    public static Sprite KeyIcon => ShapeIcon("key", (x, y) =>
    {
        float ring = new Vector2(x + 0.45f, y).magnitude;
        if (ring < 0.4f && ring > 0.18f) return true;
        if (x > -0.1f && x < 0.9f && Mathf.Abs(y) < 0.11f) return true;
        if (y < 0 && y > -0.38f && ((x > 0.5f && x < 0.63f) || (x > 0.74f && x < 0.87f))) return true;
        return false;
    });

    public static Sprite EyeIcon => ShapeIcon("eye", (x, y) =>
    {
        if (Mathf.Abs(x) >= 1f) return false;
        bool outer = Mathf.Abs(y) < 0.58f * (1f - x * x);
        float xi = x / 0.8f;
        bool inner = Mathf.Abs(xi) < 1f && Mathf.Abs(y) < 0.38f * (1f - xi * xi);
        bool pupil = new Vector2(x, y).magnitude < 0.27f;
        return (outer && !inner) || pupil;
    });

    public static Sprite GhostIcon => ShapeIcon("ghost", (x, y) =>
    {
        bool head = new Vector2(x, y - 0.1f).magnitude < 0.68f && y >= 0.1f;
        float bottom = -0.72f + 0.14f * Mathf.Cos(x * Mathf.PI * 3.2f);
        bool body = Mathf.Abs(x) < 0.68f && y < 0.1f && y > bottom;
        bool eye = new Vector2(Mathf.Abs(x) - 0.26f, y - 0.15f).magnitude < 0.13f;
        return (head || body) && !eye;
    });

    // Each ghost type gets its own silhouette detail so they're readable without colour:
    // Wisp = plain, Wraith = horns + angry eyes, Watcher = one big eye, Screamer = open mouth.
    public static Sprite GhostIconFor(GhostKind kind)
    {
        switch (kind)
        {
            case GhostKind.Wraith:
                return ShapeIcon("ghost_wraith", (x, y) =>
                {
                    bool body = GhostBody(x, y);
                    // Horns: two triangles rising from the top of the head.
                    float hx = Mathf.Abs(x) - 0.42f;
                    bool horn = y > 0.45f && y < 0.98f && Mathf.Abs(hx + (y - 0.45f) * 0.25f) < (0.98f - y) * 0.32f;
                    // Angry eyes: slanted cut across the top of each eye.
                    float ex = Mathf.Abs(x) - 0.26f, ey = y - 0.12f;
                    bool eye = new Vector2(ex, ey).magnitude < 0.14f && ey < 0.07f + ex * 0.55f;
                    return (body || horn) && !eye;
                });
            case GhostKind.Watcher:
                return ShapeIcon("ghost_watcher", (x, y) =>
                {
                    float r = new Vector2(x, y - 0.15f).magnitude;
                    bool hole = r < 0.27f && r > 0.11f; // big single eye with a pupil
                    return GhostBody(x, y) && !hole;
                });
            case GhostKind.Screamer:
                return ShapeIcon("ghost_screamer", (x, y) =>
                {
                    bool eye = new Vector2(Mathf.Abs(x) - 0.26f, y - 0.25f).magnitude < 0.1f;
                    bool mouth = new Vector2(x / 0.16f, (y + 0.15f) / 0.24f).magnitude < 1f; // wide-open "O"
                    return GhostBody(x, y) && !eye && !mouth;
                });
            default:
                return GhostIcon;
        }
    }

    static bool GhostBody(float x, float y)
    {
        bool head = new Vector2(x, y - 0.1f).magnitude < 0.68f && y >= 0.1f;
        float bottom = -0.72f + 0.14f * Mathf.Cos(x * Mathf.PI * 3.2f);
        bool body = Mathf.Abs(x) < 0.68f && y < 0.1f && y > bottom;
        return head || body;
    }

    public static Sprite ArrowIcon => ShapeIcon("arrow", (x, y) =>
    {
        // Upward-pointing chevron.
        if (y < -0.75f || y > 0.9f) return false;
        float w = (0.9f - y) * 0.48f;
        bool tri = Mathf.Abs(x) < w;
        bool notch = y < -0.25f && Mathf.Abs(x) < (-0.25f - y) * 0.9f;
        return tri && !notch;
    });

    public static Sprite DotIcon => ShapeIcon("dot", (x, y) => new Vector2(x, y).magnitude < 0.9f);

    // Five-point star (10-vertex polygon, point-in-polygon test).
    public static Sprite StarIcon => ShapeIcon("star", (x, y) =>
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float r = i % 2 == 0 ? 0.95f : 0.42f;
            pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.05f);
        }
        bool inside = false;
        for (int i = 0, j = 9; i < 10; j = i++)
        {
            if ((pts[i].y > y) != (pts[j].y > y) &&
                x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                inside = !inside;
        }
        return inside;
    });

    public static Sprite RingIcon => ShapeIcon("ring", (x, y) =>
    {
        float r = new Vector2(x, y).magnitude;
        return r < 0.97f && r > 0.86f;
    });

    // Soft round glow.
    public static Sprite GlowIcon => GradientIcon("glow", 64, new Vector2(0.5f, 0.5f), (x, y) =>
    {
        float r = new Vector2(x, y).magnitude;
        return Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
    });

    // Full-screen vignette: clear in the middle, dark at the edges.
    public static Sprite VignetteSprite => GradientIcon("vignette", 128, new Vector2(0.5f, 0.5f), (x, y) =>
    {
        float r = new Vector2(x, y).magnitude / 1.41f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, r));
    });

    // Flat vision cone, apex at the bottom centre, pointing up. 512x256 px.
    public static Sprite ConeSprite(float angle)
    {
        string key = "cone" + angle;
        if (cache.TryGetValue(key, out var s) && s != null && s.texture != null) return s;
        const int w = 512, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float half = angle * 0.5f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2((x + 0.5f - w / 2f) / h, (y + 0.5f) / h);
            float r = p.magnitude;
            float a = Vector2.Angle(Vector2.up, p);
            float edge = Mathf.Clamp01((half - a) / 6f);
            float alpha = r > 1f ? 0f : edge * (0.35f + 0.65f * (1f - r)) * Mathf.Clamp01((1f - r) * 8f);
            tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        tex.Apply();
        s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
        cache[key] = s;
        return s;
    }

    static Sprite GradientIcon(string key, int size, Vector2 pivot, Func<float, float, float> alpha)
    {
        if (cache.TryGetValue(key, out var s) && s != null && s.texture != null) return s;
        var tex = NewTexture(size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
            tex.SetPixel(x, y, new Color(1, 1, 1, alpha(u, v)));
        }
        tex.Apply();
        s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), pivot, size / 2f);
        cache[key] = s;
        return s;
    }

    static Sprite ShapeIcon(string key, Func<float, float, bool> inside)
    {
        if (cache.TryGetValue(key, out var s) && s != null && s.texture != null) return s;
        const int size = 64, ss = 4;
        var tex = NewTexture(size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int hits = 0;
            for (int sy = 0; sy < ss; sy++)
            for (int sx = 0; sx < ss; sx++)
            {
                float u = (x + (sx + 0.5f) / ss) / size * 2f - 1f;
                float v = (y + (sy + 0.5f) / ss) / size * 2f - 1f;
                if (inside(u, v)) hits++;
            }
            tex.SetPixel(x, y, new Color(1, 1, 1, hits / (float)(ss * ss)));
        }
        tex.Apply();
        s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        cache[key] = s;
        return s;
    }

    static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
    }
}
