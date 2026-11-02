using UnityEngine;
using UnityEngine.UI;

// Builds and updates the in-game HUD:
//  top-left   hourglass + timer, status message box
//  top-right  eye + danger meter, key count + key icons (+ pause button on phones)
//  bottom     stamina bar and keycap prompts (or on-screen controls on phones)
//  corner     minimap
//  full-screen red chase vignette and flash effects
public class GameHUD : MonoBehaviour
{
    GameManager gm;
    PlayerController player;
    GhostAI[] ghosts;

    CanvasGroup group;
    Image chaseVignette;
    Image flash;
    float flashStart, flashDuration;
    Color flashColor;

    Text timerText;
    Image messagePanel;
    Text messageText;
    float messageUntil;

    Image eyeIcon;
    Image dangerFill;
    Text keysText;
    Image[] keyIcons;

    RectTransform staminaFill;
    Image staminaFillImage;

    bool wasChased;

    public void Build(GameManager manager)
    {
        gm = manager;

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;
        group = canvasGo.AddComponent<CanvasGroup>();

        chaseVignette = FullScreen("ChaseVignette", root, UIStyle.VignetteSprite);
        BuildTopLeft(root);
        BuildTopRight(root, VirtualInput.IsTouchDevice);
        BuildBottom(root, VirtualInput.IsTouchDevice);
        canvasGo.AddComponent<Minimap>().Build(root);
        if (VirtualInput.IsTouchDevice) canvasGo.AddComponent<TouchControls>().Build(root);
        flash = FullScreen("Flash", root, null);
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = group.blocksRaycasts = visible;
    }

    // Full-screen colour flash that fades out (real time, so it works during slow motion).
    public void Flash(Color color, float duration)
    {
        flashColor = color;
        flashStart = Time.unscaledTime;
        flashDuration = duration;
    }

    static Image FullScreen(string name, RectTransform root, Sprite sprite)
    {
        var img = UIStyle.Rect(name, root).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.clear;
        img.raycastTarget = false;
        UIStyle.Stretch(img.rectTransform);
        return img;
    }

    void BuildTopLeft(RectTransform root)
    {
        var hourglass = UIStyle.Icon("Hourglass", root, UIStyle.HourglassIcon, UIStyle.Paper, 46);
        UIStyle.Anchor(hourglass.rectTransform, new Vector2(0, 1), new Vector2(468, -36), new Vector2(46, 46));
        hourglass.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;

        timerText = UIStyle.Label("Timer", root, 44, UIStyle.Paper, TextAnchor.MiddleLeft, outline: true);
        UIStyle.Anchor(timerText.rectTransform, new Vector2(0, 1), new Vector2(530, -32), new Vector2(300, 56));

        messagePanel = UIStyle.Panel("Message", root);
        messagePanel.raycastTarget = false;
        var rt = messagePanel.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(460, -104);
        var layout = messagePanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 20, 20);
        layout.childControlWidth = layout.childControlHeight = true;
        var fitter = messagePanel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        messageText = UIStyle.Label("Text", messagePanel.transform, 30, UIStyle.Paper);
    }

    void BuildTopRight(RectTransform root, bool touch)
    {
        eyeIcon = UIStyle.Icon("Eye", root, UIStyle.EyeIcon, UIStyle.Paper, 54);
        UIStyle.Anchor(eyeIcon.rectTransform, new Vector2(1, 1), new Vector2(-858, -36), new Vector2(54, 54));
        eyeIcon.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;

        var bar = UIStyle.Panel("DangerBar", root, light: true, radius: 18, border: 5);
        bar.raycastTarget = false;
        UIStyle.Anchor(bar.rectTransform, new Vector2(1, 1), new Vector2(-460, -38), new Vector2(380, 44));
        dangerFill = UIStyle.Rect("Fill", bar.transform).gameObject.AddComponent<Image>();
        dangerFill.sprite = UIStyle.RoundedSprite(12, 0, Color.white, Color.white);
        dangerFill.type = Image.Type.Sliced;
        dangerFill.color = UIStyle.Ink;
        dangerFill.raycastTarget = false;
        var fr = dangerFill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(0.05f, 1);
        fr.offsetMin = new Vector2(9, 9);
        fr.offsetMax = new Vector2(-9, -9);

        keysText = UIStyle.Label("Keys", root, 42, UIStyle.Paper, TextAnchor.MiddleCenter, outline: true);
        UIStyle.Anchor(keysText.rectTransform, new Vector2(1, 1), new Vector2(-300, -32), new Vector2(140, 56));

        keyIcons = new Image[gm.keysRequired];
        for (int i = 0; i < keyIcons.Length; i++)
        {
            keyIcons[i] = UIStyle.Icon("KeyIcon", root, UIStyle.KeyIcon, UIStyle.Paper, 44);
            UIStyle.Anchor(keyIcons[i].rectTransform, new Vector2(1, 1), new Vector2(-40 - (keyIcons.Length - 1 - i) * 52, -38), new Vector2(44, 44));
            keyIcons[i].gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
        }

        // Phones have no Esc key, so give them a pause button.
        if (touch)
        {
            var pause = UIStyle.Panel("Pause", root, radius: 18, border: 4);
            UIStyle.Anchor(pause.rectTransform, new Vector2(1, 1), new Vector2(-40, -110), new Vector2(96, 76));
            pause.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                AudioManager.Instance?.Click();
                gm.Pause();
            });
            var label = UIStyle.Label("Text", pause.transform, 34, UIStyle.Paper);
            label.text = "II";
            UIStyle.Stretch(label.rectTransform);
        }
    }

    void BuildBottom(RectTransform root, bool touch)
    {
        var back = UIStyle.Panel("Stamina", root, radius: 12, border: 4);
        back.raycastTarget = false;
        UIStyle.Anchor(back.rectTransform, new Vector2(0.5f, 0), new Vector2(0, touch ? 50 : 118), new Vector2(420, 26));
        staminaFillImage = UIStyle.Rect("Fill", back.transform).gameObject.AddComponent<Image>();
        staminaFillImage.sprite = UIStyle.RoundedSprite(8, 0, Color.white, Color.white);
        staminaFillImage.type = Image.Type.Sliced;
        staminaFillImage.raycastTarget = false;
        staminaFill = staminaFillImage.rectTransform;
        staminaFill.anchorMin = Vector2.zero;
        staminaFill.anchorMax = Vector2.one;
        staminaFill.offsetMin = new Vector2(6, 6);
        staminaFill.offsetMax = new Vector2(-6, -6);

        if (touch) return; // on-screen controls replace the keyboard prompts

        var prompts = UIStyle.Rect("Prompts", root);
        UIStyle.Anchor(prompts, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1200, 64));
        var layout = prompts.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 60;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = false;

        UIStyle.Prompt(prompts, "WASD", "MOVE", out _).sizeDelta = new Vector2(230, 60);
        UIStyle.Prompt(prompts, "SHIFT", "SPRINT", out _).sizeDelta = new Vector2(260, 60);
    }

    public void ShowMessage(string text, float seconds)
    {
        messageText.text = text.ToUpperInvariant();
        messagePanel.gameObject.SetActive(true);
        messageUntil = Time.time + seconds;
    }

    void Update()
    {
        if (gm == null) return;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (ghosts == null) ghosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);

        int s = Mathf.CeilToInt(gm.TimeLeft);
        timerText.text = $"{s / 60:00}:{s % 60:00}";
        timerText.color = gm.TimeLeft < 60f && Mathf.Repeat(Time.time, 1f) < 0.5f ? UIStyle.Danger : UIStyle.Paper;

        if (Time.time > messageUntil && !gm.IsOver) messagePanel.gameObject.SetActive(false);

        keysText.text = $"{gm.KeysCollected} / {gm.keysRequired}";
        for (int i = 0; i < keyIcons.Length; i++)
            keyIcons[i].color = i < gm.KeysCollected ? UIStyle.Gold : new Color(1, 1, 1, 0.35f);

        float danger = 0f;
        bool chased = false;
        foreach (var g in ghosts)
        {
            if (g == null) continue;
            danger = Mathf.Max(danger, g.Alertness);
            chased |= g.IsChasing;
        }
        var fr = dangerFill.rectTransform;
        fr.anchorMax = new Vector2(Mathf.Lerp(fr.anchorMax.x, Mathf.Max(0.05f, danger), Time.deltaTime * 6f), 1);
        dangerFill.color = danger >= 0.99f ? UIStyle.Danger : UIStyle.Ink;
        eyeIcon.color = gm.HuntActive ? UIStyle.Danger : UIStyle.Paper;

        if (!gm.IsOver && chased != wasChased)
        {
            ShowMessage(chased ? "A GHOST SPOTTED YOU!\nBREAK LINE OF SIGHT" : "YOU LOST THEM...\nSTAY OUT OF SIGHT", 3f);
            wasChased = chased;
        }

        // Red edges pulse while chased; a steady red glow during the hunt.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
        float vignette = chased ? Mathf.Lerp(0.35f, 0.75f, pulse) : gm.HuntActive ? 0.3f : 0f;
        if (gm.IsOver) vignette = 0f;
        var vc = chaseVignette.color;
        float a = Mathf.MoveTowards(vc.a, vignette, Time.unscaledDeltaTime * 2.5f);
        chaseVignette.color = new Color(0.85f, 0.05f, 0.03f, a);

        float fk = flashDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - flashStart) / flashDuration) : 1f;
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * (1f - fk));

        if (player != null)
        {
            staminaFill.anchorMax = new Vector2(player.Stamina01, 1f);
            staminaFillImage.color = player.Stamina01 < 0.3f ? UIStyle.Danger : new Color(0.55f, 0.85f, 1f);
        }
    }
}
