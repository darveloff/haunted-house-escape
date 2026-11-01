using UnityEngine;
using UnityEngine.UI;

// Builds and updates the in-game HUD:
//  top-left   hourglass + timer, status message box
//  top-right  eye + danger meter, key count + key icons
//  bottom     stamina bar and keycap prompts
//  corner     minimap
public class GameHUD : MonoBehaviour
{
    GameManager gm;
    PlayerController player;
    GhostAI[] ghosts;

    Text timerText;
    Image messagePanel;
    Text messageText;
    float messageUntil;

    Image dangerFill;
    Text keysText;
    Image[] keyIcons;

    RectTransform staminaFill;
    Image staminaFillImage;
    GameObject hidePrompt;
    Text hideActionText;

    Image banner;
    Text bannerText;

    bool wasChased;

    public void Build(GameManager manager)
    {
        gm = manager;

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;

        BuildTopLeft(root);
        BuildTopRight(root);
        BuildBottom(root);
        BuildBanner(root);
        canvasGo.AddComponent<Minimap>().Build(root);
    }

    CanvasGroup group;

    public void SetVisible(bool visible)
    {
        if (group != null) group.alpha = visible ? 1f : 0f;
    }

    void BuildTopLeft(RectTransform root)
    {
        var hourglass = UIStyle.Icon("Hourglass", root, UIStyle.HourglassIcon, UIStyle.Paper, 46);
        UIStyle.Anchor(hourglass.rectTransform, new Vector2(0, 1), new Vector2(468, -36), new Vector2(46, 46));
        hourglass.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;

        timerText = UIStyle.Label("Timer", root, 44, UIStyle.Paper, TextAnchor.MiddleLeft, outline: true);
        UIStyle.Anchor(timerText.rectTransform, new Vector2(0, 1), new Vector2(530, -32), new Vector2(300, 56));

        messagePanel = UIStyle.Panel("Message", root);
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

    void BuildTopRight(RectTransform root)
    {
        var eye = UIStyle.Icon("Eye", root, UIStyle.EyeIcon, UIStyle.Paper, 54);
        UIStyle.Anchor(eye.rectTransform, new Vector2(1, 1), new Vector2(-858, -36), new Vector2(54, 54));
        eye.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;

        var bar = UIStyle.Panel("DangerBar", root, light: true, radius: 18, border: 5);
        UIStyle.Anchor(bar.rectTransform, new Vector2(1, 1), new Vector2(-460, -38), new Vector2(380, 44));
        dangerFill = UIStyle.Rect("Fill", bar.transform).gameObject.AddComponent<Image>();
        dangerFill.sprite = UIStyle.RoundedSprite(12, 0, Color.white, Color.white);
        dangerFill.type = Image.Type.Sliced;
        dangerFill.color = UIStyle.Ink;
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
    }

    void BuildBottom(RectTransform root)
    {
        // Stamina
        var back = UIStyle.Panel("Stamina", root, radius: 12, border: 4);
        UIStyle.Anchor(back.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 118), new Vector2(420, 26));
        staminaFillImage = UIStyle.Rect("Fill", back.transform).gameObject.AddComponent<Image>();
        staminaFillImage.sprite = UIStyle.RoundedSprite(8, 0, Color.white, Color.white);
        staminaFillImage.type = Image.Type.Sliced;
        staminaFill = staminaFillImage.rectTransform;
        staminaFill.anchorMin = Vector2.zero;
        staminaFill.anchorMax = Vector2.one;
        staminaFill.offsetMin = new Vector2(6, 6);
        staminaFill.offsetMax = new Vector2(-6, -6);

        var prompts = UIStyle.Rect("Prompts", root);
        UIStyle.Anchor(prompts, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1200, 64));
        var layout = prompts.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 60;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = false;

        UIStyle.Prompt(prompts, "WASD", "MOVE", out _).sizeDelta = new Vector2(230, 60);
        UIStyle.Prompt(prompts, "SHIFT", "SPRINT", out _).sizeDelta = new Vector2(260, 60);
        var hide = UIStyle.Prompt(prompts, "E", "HIDE", out hideActionText);
        hide.sizeDelta = new Vector2(200, 60);
        hidePrompt = hide.gameObject;
    }

    void BuildBanner(RectTransform root)
    {
        banner = UIStyle.Panel("Banner", root, radius: 30, border: 6);
        var rt = banner.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 60);
        var layout = banner.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(70, 70, 44, 44);
        layout.childControlWidth = layout.childControlHeight = true;
        var fitter = banner.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bannerText = UIStyle.Label("Text", banner.transform, 56, UIStyle.Paper);
        banner.gameObject.SetActive(false);
    }

    public void ShowMessage(string text, float seconds)
    {
        messageText.text = text.ToUpperInvariant();
        messagePanel.gameObject.SetActive(true);
        messageUntil = Time.time + seconds;
    }

    public void ShowBanner(string text)
    {
        bannerText.text = text.ToUpperInvariant();
        banner.gameObject.SetActive(true);
        messagePanel.gameObject.SetActive(false);
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

        if (!gm.IsOver && chased != wasChased)
        {
            ShowMessage(chased ? "A GHOST SPOTTED YOU!\nBREAK LINE OF SIGHT" : "YOU LOST THEM...\nSTAY OUT OF SIGHT", 3f);
            wasChased = chased;
        }

        if (player != null)
        {
            staminaFill.anchorMax = new Vector2(player.Stamina01, 1f);
            staminaFillImage.color = player.Stamina01 < 0.3f ? UIStyle.Danger : new Color(0.55f, 0.85f, 1f);
            bool canHide = player.IsHidden || HideSpot.Current != null;
            hidePrompt.SetActive(canHide && !gm.IsOver);
            hideActionText.text = player.IsHidden ? "LEAVE" : "HIDE";
        }
    }
}
