using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Title, pause and end screens in the same chunky style as the HUD:
// tilted sticker headings, dark pills with keycaps, white speech bubbles.
public class MenuUI : MonoBehaviour
{
    static readonly string[] Tips =
    {
        "WRAITHS ARE NEARLY BLIND.\nWALK PAST THEM - DON'T RUN.",
        "THE WATCHER SEES ACROSS ROOMS.\nKEEP A WALL BETWEEN YOU.",
        "IF THE SCREAMER SPOTS YOU,\nEVERYONE COMES. RUN AND BREAK SIGHT.",
        "DUCK BEHIND WALLS AND PILLARS.\nGHOSTS CAN'T SEE THROUGH THEM.",
        "THE RADAR SHOWS GHOSTS FOR\n5 SECONDS. PLAN YOUR ROUTE.",
    };

    GameManager gm;
    RectTransform root;
    GameObject title, pause, end;
    Text pauseStatus, pauseTip;
    Image pauseTipGhost;

    // Title ghost lineup
    readonly List<(RectTransform slot, CanvasGroup group, PointerFlag hover, int kind)> slots = new List<(RectTransform, CanvasGroup, PointerFlag, int)>();
    RectTransform lineupBubble;
    Image lineupTail, lineupTailCover;
    Text bubbleName, bubbleTagline;
    RectTransform chipRow;
    int selected = -1;
    float nextCycle;

    public void Build(GameManager manager)
    {
        gm = manager;
        var canvasGo = new GameObject("Menus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        title = BuildTitle();
        pause = BuildPause();
        HideAll();
    }

    public void HideAll()
    {
        title.SetActive(false);
        pause.SetActive(false);
        if (end != null) Destroy(end);
    }

    public void ShowTitle()
    {
        HideAll();
        title.SetActive(true);
        Select(0);
        nextCycle = Time.unscaledTime + 3.5f;
    }

    public void ShowPause()
    {
        HideAll();
        int s = Mathf.CeilToInt(gm.TimeLeft);
        pauseStatus.text = $"KEYS  {gm.KeysCollected}/{gm.keysRequired}     {s / 60:00}:{s % 60:00} LEFT";
        int tip = Random.Range(0, Tips.Length);
        pauseTip.text = Tips[tip];
        pauseTipGhost.color = GhostAI.Profiles[Mathf.Min(tip, GhostAI.Profiles.Length - 1)].color;
        pause.SetActive(true);
    }

    public void ShowEscaped()
    {
        BuildEnd(new Color(0.02f, 0.12f, 0.06f, 0.55f), "YOU\nESCAPED!", "<color=#7CFF8E>FREE AT LAST</color>",
            UIStyle.KeyIcon, UIStyle.Gold, "FREEDOM!", celebrate: true);
    }

    public void ShowCaught(GhostKind kind)
    {
        var p = GhostAI.Profiles[(int)kind];
        BuildEnd(new Color(0.14f, 0.02f, 0.03f, 0.55f), "CAUGHT!", $"BY THE <color=#{ColorUtility.ToHtmlStringRGB(p.color)}>{p.name}</color>",
            UIStyle.GhostIcon, p.color, "BOO!", celebrate: false);
    }

    public void ShowTimeUp()
    {
        BuildEnd(new Color(0.08f, 0.04f, 0.12f, 0.55f), "TIME'S\nUP!", "THE HOUSE KEEPS YOU",
            UIStyle.HourglassIcon, UIStyle.Paper, "TICK...\nTOCK...", celebrate: false);
    }

    void Update()
    {
        if (!title.activeSelf) return;

        int hovered = slots.FindIndex(s => s.hover.Hovered);
        if (hovered >= 0 && hovered != selected) Select(hovered);
        else if (hovered < 0 && Time.unscaledTime >= nextCycle) Select((selected + 1) % slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            float target = i == selected ? 1.18f : 0.92f;
            var slot = slots[i];
            slot.slot.localScale = Vector3.one * Mathf.Lerp(slot.slot.localScale.x, target, Time.unscaledDeltaTime * 10f);
            slot.group.alpha = Mathf.Lerp(slot.group.alpha, i == selected ? 1f : 0.5f, Time.unscaledDeltaTime * 10f);
        }
    }

    // ---------------------------------------------------------------- shared pieces

    RectTransform Screen(string name, Color tint) => Backdrop(name, root, tint);

    static RectTransform Group(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = UIStyle.Rect(name, parent);
        UIStyle.Anchor(rt, anchor, pos, size);
        return rt;
    }

    public static Text MessageBox(RectTransform parent, string text, Vector2 pos, int size = 34)
    {
        var panel = UIStyle.Panel("MessageBox", parent);
        var rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 22, 22);
        layout.childControlWidth = layout.childControlHeight = true;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var label = UIStyle.Label("Text", rt, size, UIStyle.Paper, TextAnchor.MiddleLeft);
        label.text = text;
        return label;
    }

    static RectTransform ButtonColumn(RectTransform parent, Vector2 pos, bool horizontal = false)
    {
        var col = UIStyle.Rect("Buttons", parent);
        col.anchorMin = col.anchorMax = col.pivot = new Vector2(0, 1);
        col.anchoredPosition = pos;
        HorizontalOrVerticalLayoutGroup layout = horizontal
            ? col.gameObject.AddComponent<HorizontalLayoutGroup>()
            : (HorizontalOrVerticalLayoutGroup)col.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 22;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fit = col.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return col;
    }

    static void ControlsRow(RectTransform parent)
    {
        var row = UIStyle.Rect("Controls", parent);
        UIStyle.Anchor(row, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1400, 64));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 56;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        UIStyle.Prompt(row, "WASD", "MOVE", out _).sizeDelta = new Vector2(220, 60);
        UIStyle.Prompt(row, "SHIFT", "SPRINT", out _).sizeDelta = new Vector2(250, 60);
        UIStyle.Prompt(row, "ESC", "PAUSE", out _).sizeDelta = new Vector2(220, 60);
    }

    // ---------------------------------------------------------------- title

    GameObject BuildTitle()
    {
        var screen = Screen("Title", new Color(0.02f, 0.02f, 0.05f, 0.25f));

        BuildLogo(screen, animate: true);

        var goal = MessageBox(screen, $"FIND {gm.keysRequired} KEYS.\nDON'T GET CAUGHT.", new Vector2(120, -500));
        UIAnim.Add(goal.transform.parent, 0.35f);

        var buttons = ButtonColumn(screen, new Vector2(120, -680));
        UIStyle.PillButton(buttons, "SPACE", "PLAY", gm.StartGame);
        if (GameManager.CanQuit) UIStyle.PillButton(buttons, "Q", "QUIT", gm.Quit);
        UIAnim.Add(buttons, 0.45f);

        BuildLineup(screen);
        ControlsRow(screen);
        return screen.gameObject;
    }

    // Tilted HAUNTED HOUSE stickers with the red ESCAPE tag. Shared with the edit-mode preview,
    // which passes animate: false because UIAnim doesn't tick outside Play mode.
    public static void BuildLogo(RectTransform screen, bool animate)
    {
        var logo = Group("Logo", screen, new Vector2(0, 1), new Vector2(110, -60), new Vector2(900, 400));
        var haunted = UIStyle.Sticker("Haunted", logo, 150, UIStyle.Paper, -4f);
        Place(haunted.rectTransform, new Vector2(0, 0), new Vector2(900, 170), TextAnchor.MiddleLeft);
        haunted.text = "HAUNTED";
        var house = UIStyle.Sticker("House", logo, 176, UIStyle.Paper, -4f);
        Place(house.rectTransform, new Vector2(60, -150), new Vector2(900, 190), TextAnchor.MiddleLeft);
        house.text = "HOUSE";
        var tag = UIStyle.Rect("EscapeTag", logo).gameObject.AddComponent<Image>();
        tag.sprite = UIStyle.RoundedSprite(26, 6, UIStyle.Danger, UIStyle.Paper);
        tag.type = Image.Type.Sliced;
        tag.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -8);
        Place(tag.rectTransform, new Vector2(540, -326), new Vector2(330, 92), TextAnchor.MiddleCenter);
        tag.rectTransform.localEulerAngles = new Vector3(0, 0, 6);
        var tagText = UIStyle.Label("Text", tag.transform, 56, UIStyle.Paper);
        tagText.text = "ESCAPE";
        UIStyle.Stretch(tagText.rectTransform);
        if (animate)
        {
            UIAnim.Add(logo, 0f, bob: 6f, wobble: 0.8f);
            UIAnim.Add(tag.rectTransform, 0.3f, wobble: 3f, phase: 1f);
        }
    }

    public static RectTransform Backdrop(string name, RectTransform root, Color tint)
    {
        var rt = UIStyle.Rect(name, root);
        UIStyle.Stretch(rt);
        var flat = UIStyle.Rect("Tint", rt).gameObject.AddComponent<Image>();
        flat.color = tint;
        UIStyle.Stretch(flat.rectTransform);
        var vignette = UIStyle.Rect("Vignette", rt).gameObject.AddComponent<Image>();
        vignette.sprite = UIStyle.VignetteSprite;
        vignette.color = new Color(0, 0, 0, 0.92f);
        vignette.raycastTarget = false;
        UIStyle.Stretch(vignette.rectTransform);
        return rt;
    }

    void BuildLineup(RectTransform screen)
    {
        var header = UIStyle.Sticker("Header", screen, 54, UIStyle.Paper, 3f);
        UIStyle.Anchor(header.rectTransform, new Vector2(1, 1), new Vector2(-520, -90), new Vector2(800, 80));
        header.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var ghosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        header.text = $"TONIGHT'S HAUNTS  <color=#FFD24D>×{ghosts.Length}</color>";
        UIAnim.Add(header.rectTransform, 0.2f, wobble: 1.5f);

        // Speech bubble that describes the selected ghost.
        var bubble = UIStyle.Bubble("Bubble", screen, new Vector2(640, 250));
        lineupBubble = bubble.rectTransform;
        UIStyle.Anchor(lineupBubble, new Vector2(1, 1), new Vector2(-520, -305), new Vector2(640, 250));
        lineupBubble.pivot = new Vector2(0.5f, 0.5f);
        lineupTail = lineupBubble.Find("Tail").GetComponent<Image>();
        lineupTailCover = lineupBubble.Find("TailCover").GetComponent<Image>();
        bubbleName = UIStyle.Label("Name", lineupBubble, 46, UIStyle.Ink, TextAnchor.UpperLeft);
        bubbleName.supportRichText = true;
        UIStyle.Anchor(bubbleName.rectTransform, new Vector2(0, 1), new Vector2(34, -26), new Vector2(580, 56));
        bubbleName.rectTransform.pivot = new Vector2(0, 1);
        bubbleTagline = UIStyle.Label("Tagline", lineupBubble, 27, UIStyle.Ink, TextAnchor.UpperLeft);
        bubbleTagline.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIStyle.Anchor(bubbleTagline.rectTransform, new Vector2(0, 1), new Vector2(34, -88), new Vector2(572, 80));
        bubbleTagline.rectTransform.pivot = new Vector2(0, 1);
        chipRow = UIStyle.Rect("Chips", lineupBubble);
        chipRow.anchorMin = chipRow.anchorMax = chipRow.pivot = new Vector2(0, 0);
        chipRow.anchoredPosition = new Vector2(34, 26);
        var chipLayout = chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 12;
        chipLayout.childControlWidth = chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
        var chipFit = chipRow.gameObject.AddComponent<ContentSizeFitter>();
        chipFit.horizontalFit = chipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIAnim.Add(lineupBubble, 0.4f);

        // Ghost lineup: floating icons with name pills.
        var row = Group("Lineup", screen, new Vector2(1, 1), new Vector2(-90, -470), new Vector2(860, 330));
        row.pivot = new Vector2(1, 1);
        for (int i = 0; i < GhostAI.Profiles.Length; i++)
        {
            var p = GhostAI.Profiles[i];
            int count = ghosts.Count(g => (int)g.kind == i);

            var slot = UIStyle.Rect("Slot_" + p.name, row);
            slot.anchorMin = slot.anchorMax = new Vector2(0, 1);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(110 + i * 213, -140);
            slot.sizeDelta = new Vector2(200, 270);
            var group = slot.gameObject.AddComponent<CanvasGroup>();
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var flag = slot.gameObject.AddComponent<PointerFlag>();

            var floaty = UIStyle.Rect("Float", slot);
            UIStyle.Anchor(floaty, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(200, 200));
            floaty.pivot = new Vector2(0.5f, 0.5f);
            var glow = UIStyle.Icon("Glow", floaty, UIStyle.GlowIcon, new Color(p.color.r, p.color.g, p.color.b, 0.55f), 240);
            glow.rectTransform.sizeDelta = new Vector2(240, 240);
            var icon = UIStyle.Icon("Ghost", floaty, UIStyle.GhostIcon, p.color, 150);
            foreach (var d in new[] { new Vector2(4, -4), new Vector2(-4, 4) })
                icon.gameObject.AddComponent<Outline>().effectDistance = d;
            icon.GetComponents<Outline>().ToList().ForEach(o => o.effectColor = UIStyle.Ink);
            var badge = UIStyle.Icon("Badge", floaty, UIStyle.DotIcon, UIStyle.Paper, 56);
            badge.rectTransform.anchoredPosition = new Vector2(62, 58);
            badge.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
            var badgeText = UIStyle.Label("Count", badge.transform, 26, UIStyle.Ink);
            badgeText.text = "×" + count;
            UIStyle.Stretch(badgeText.rectTransform);
            UIAnim.Add(floaty, 0.5f + i * 0.08f, bob: 10f, wobble: 4f, phase: i * 1.3f);

            var pill = UIStyle.Panel("Name", slot, radius: 20, border: 4);
            UIStyle.Anchor(pill.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(196, 58));
            pill.rectTransform.pivot = new Vector2(0.5f, 0);
            var name = UIStyle.Label("Text", pill.transform, 30, p.color);
            name.text = p.name;
            UIStyle.Stretch(name.rectTransform);

            slots.Add((slot, group, flag, i));
        }
    }

    void Select(int index)
    {
        if (slots.Count == 0) return;
        selected = index;
        nextCycle = Time.unscaledTime + 3.5f;
        var p = GhostAI.Profiles[slots[index].kind];
        int count = FindObjectsByType<GhostAI>(FindObjectsSortMode.None).Count(g => (int)g.kind == slots[index].kind);
        bubbleName.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(p.color, UIStyle.Ink, 0.25f))}>{p.name}</color>  ×{count}";
        bubbleTagline.text = p.tagline.ToUpperInvariant();

        foreach (Transform c in chipRow) Destroy(c.gameObject);
        foreach (var trait in p.traits)
        {
            var chip = UIStyle.Rect("Chip", chipRow).gameObject.AddComponent<Image>();
            chip.sprite = UIStyle.RoundedSprite(16, 0, p.color, p.color);
            chip.type = Image.Type.Sliced;
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            var text = UIStyle.Label("Text", chip.transform, 22, UIStyle.Ink);
            text.text = trait;
        }

        // Point the bubble's tail at the selected ghost.
        float slotX = -90f - 860f + 110f + index * 213f;   // slot centre in the screen's top-right space
        float bubbleX = -520f;
        float tailX = Mathf.Clamp(slotX - bubbleX, -270f, 270f);
        lineupTail.rectTransform.anchoredPosition = new Vector2(tailX, -4);
        lineupTailCover.rectTransform.anchoredPosition = new Vector2(tailX, 11);
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size, TextAnchor align)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = rt.GetComponent<Text>();
        if (text != null) text.alignment = align;
    }

    // ---------------------------------------------------------------- pause

    GameObject BuildPause()
    {
        var screen = Screen("Pause", new Color(0.02f, 0.02f, 0.05f, 0.55f));

        var heading = UIStyle.Sticker("Paused", screen, 160, UIStyle.Paper, -4f);
        Place(heading.rectTransform, new Vector2(110, -80), new Vector2(900, 200), TextAnchor.MiddleLeft);
        UIAnim.Add(heading.rectTransform, 0f, wobble: 1f);

        pauseStatus = MessageBox(screen, "", new Vector2(120, -310));
        UIAnim.Add(pauseStatus.transform.parent, 0.1f);

        var buttons = ButtonColumn(screen, new Vector2(120, -450));
        UIStyle.PillButton(buttons, "ESC", "RESUME", gm.Resume);
        UIStyle.PillButton(buttons, "R", "RESTART", gm.Restart);
        UIStyle.PillButton(buttons, "M", "MAIN MENU", gm.MainMenu);
        if (GameManager.CanQuit) UIStyle.PillButton(buttons, "Q", "QUIT", gm.Quit);
        UIAnim.Add(buttons, 0.15f);

        // A ghost with a tip in a speech bubble.
        var tipGroup = Group("Tip", screen, new Vector2(1, 0), new Vector2(-140, 150), new Vector2(700, 420));
        tipGroup.pivot = new Vector2(1, 0);
        var bubble = UIStyle.Bubble("Bubble", tipGroup, new Vector2(620, 170), tailX: 180f);
        UIStyle.Anchor(bubble.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(620, 170));
        bubble.rectTransform.pivot = new Vector2(0.5f, 1);
        var tipLabel = UIStyle.Label("TipLabel", bubble.transform, 24, new Color(0.45f, 0.45f, 0.5f), TextAnchor.UpperLeft);
        UIStyle.Anchor(tipLabel.rectTransform, new Vector2(0, 1), new Vector2(30, -22), new Vector2(200, 30));
        tipLabel.rectTransform.pivot = new Vector2(0, 1);
        tipLabel.text = "GHOST TIP";
        pauseTip = UIStyle.Label("Tip", bubble.transform, 28, UIStyle.Ink, TextAnchor.UpperLeft);
        UIStyle.Anchor(pauseTip.rectTransform, new Vector2(0, 1), new Vector2(30, -58), new Vector2(570, 90));
        pauseTip.rectTransform.pivot = new Vector2(0, 1);
        pauseTipGhost = UIStyle.Icon("Ghost", tipGroup, UIStyle.GhostIcon, UIStyle.Paper, 170);
        UIStyle.Anchor(pauseTipGhost.rectTransform, new Vector2(0.5f, 0), new Vector2(180, 20), new Vector2(170, 170));
        pauseTipGhost.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
        UIAnim.Add(pauseTipGhost.rectTransform, 0.25f, bob: 10f, wobble: 5f);
        UIAnim.Add(bubble.rectTransform, 0.35f);
        return screen.gameObject;
    }

    // ---------------------------------------------------------------- end screens

    void BuildEnd(Color tint, string heading, string sub, Sprite heroSprite, Color heroColor, string shout, bool celebrate)
    {
        HideAll();
        var screen = Screen("End", tint);
        end = screen.gameObject;

        var head = UIStyle.Sticker("Heading", screen, 170, UIStyle.Paper, -4f);
        head.lineSpacing = 0.85f;
        Place(head.rectTransform, new Vector2(110, -60), new Vector2(1000, 380), TextAnchor.MiddleLeft);
        UIAnim.Add(head.rectTransform, 0f, wobble: 1.2f);

        var subText = UIStyle.Sticker("Sub", screen, 58, UIStyle.Paper, -2f);
        Place(subText.rectTransform, new Vector2(130, -440), new Vector2(1000, 80), TextAnchor.MiddleLeft);
        subText.text = sub;
        head.text = heading;
        UIAnim.Add(subText.rectTransform, 0.2f);

        // Stat pills.
        var stats = UIStyle.Rect("Stats", screen);
        stats.anchorMin = stats.anchorMax = stats.pivot = new Vector2(0, 1);
        stats.anchoredPosition = new Vector2(120, -560);
        var statLayout = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
        statLayout.spacing = 20;
        statLayout.childControlWidth = statLayout.childControlHeight = false;
        statLayout.childForceExpandWidth = false;
        int used = Mathf.CeilToInt(gm.TimeUsed);
        Stat(stats, UIStyle.HourglassIcon, $"{used / 60:00}:{used % 60:00}", "TIME", 0.35f);
        Stat(stats, UIStyle.KeyIcon, $"{gm.KeysCollected}/{gm.keysRequired}", "KEYS", 0.42f);
        Stat(stats, UIStyle.EyeIcon, gm.TimesSpotted.ToString(), "SPOTTED", 0.49f);

        var buttons = ButtonColumn(screen, new Vector2(120, -760), horizontal: true);
        UIStyle.PillButton(buttons, "R", "PLAY AGAIN", gm.Restart, 440);
        UIStyle.PillButton(buttons, "M", "MAIN MENU", gm.MainMenu, 420);
        UIAnim.Add(buttons, 0.65f);

        // Hero on the right: the ghost that got you, the key, or the hourglass.
        var hero = Group("Hero", screen, new Vector2(1, 0.5f), new Vector2(-300, -60), new Vector2(520, 520));
        hero.pivot = new Vector2(0.5f, 0.5f);
        var glow = UIStyle.Icon("Glow", hero, UIStyle.GlowIcon, new Color(heroColor.r, heroColor.g, heroColor.b, 0.6f), 620);
        glow.rectTransform.sizeDelta = new Vector2(620, 620);
        var icon = UIStyle.Icon("Icon", hero, heroSprite, heroColor, 380);
        foreach (var d in new[] { new Vector2(7, -7), new Vector2(-7, 7) })
        {
            var o = icon.gameObject.AddComponent<Outline>();
            o.effectColor = UIStyle.Ink;
            o.effectDistance = d;
        }
        UIAnim.Add(hero, 0.25f, bob: 16f, wobble: celebrate ? 8f : 5f);

        var bubble = UIStyle.Bubble("Shout", screen, new Vector2(340, 170), tailX: 70f);
        UIStyle.Anchor(bubble.rectTransform, new Vector2(1, 0.5f), new Vector2(-560, 290), new Vector2(340, 170));
        bubble.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bubble.rectTransform.localEulerAngles = new Vector3(0, 0, 5);
        var shoutText = UIStyle.Label("Text", bubble.transform, shout.Contains("\n") ? 46 : 70, celebrate ? new Color(0.15f, 0.55f, 0.25f) : UIStyle.Ink);
        shoutText.text = shout;
        UIStyle.Stretch(shoutText.rectTransform);
        UIAnim.Add(bubble.rectTransform, 0.55f, wobble: 3f);

        if (celebrate)
        {
            // Little keys floating around the big one.
            for (int i = 0; i < 7; i++)
            {
                var k = UIStyle.Icon("Spark", hero, UIStyle.KeyIcon, UIStyle.Gold, 60);
                float a = i / 7f * Mathf.PI * 2f;
                k.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 290f;
                k.rectTransform.localEulerAngles = new Vector3(0, 0, i * 50f);
                k.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
                UIAnim.Add(k.rectTransform, 0.5f + i * 0.06f, bob: 14f, wobble: 20f, phase: i);
            }
        }
    }

    static void Stat(RectTransform parent, Sprite icon, string value, string label, float delay)
    {
        var pill = UIStyle.Panel("Stat_" + label, parent, radius: 24, border: 5);
        pill.rectTransform.sizeDelta = new Vector2(196, 170);
        var img = UIStyle.Icon("Icon", pill.transform, icon, UIStyle.Paper, 48);
        UIStyle.Anchor(img.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(48, 48));
        img.rectTransform.pivot = new Vector2(0.5f, 1);
        var v = UIStyle.Label("Value", pill.transform, 48, UIStyle.Paper);
        UIStyle.Anchor(v.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(190, 56));
        v.text = value;
        var l = UIStyle.Label("Label", pill.transform, 22, new Color(1, 1, 1, 0.6f));
        UIStyle.Anchor(l.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(190, 30));
        l.text = label;
        UIAnim.Add(pill.rectTransform, delay);
    }
}
