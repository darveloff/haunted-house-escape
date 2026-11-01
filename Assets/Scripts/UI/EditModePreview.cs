using UnityEngine;
using UnityEngine.UI;

// Shows the title look in the Game view while you're editing (outside Play mode):
// logo, goal box and a "press Play" prompt. Everything it creates is hidden and never saved,
// so nothing ends up in the scene file, and it removes itself as soon as Play starts.
[ExecuteAlways]
public class EditModePreview : MonoBehaviour
{
    GameObject canvas;

    void OnEnable()
    {
        if (!Application.isPlaying) Build();
    }

    void OnDisable() => Clear();

    void Update()
    {
        // Covers Play mode entered without a scene reload.
        if (Application.isPlaying) Clear();
        else if (canvas == null) Build();
    }

    void Build()
    {
        Clear();
        canvas = new GameObject("EditModePreviewCanvas", typeof(Canvas), typeof(CanvasScaler));
        var c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 20;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvas.transform;

        var screen = MenuUI.Backdrop("Preview", root, new Color(0.02f, 0.02f, 0.05f, 0.25f));
        MenuUI.BuildLogo(screen, animate: false);
        MenuUI.MessageBox(screen, "FIND 5 KEYS.\nDON'T GET CAUGHT.", new Vector2(120, -500));

        // "Press Play" bubble, pointing up towards the editor's Play button.
        var bubble = UIStyle.Bubble("PressPlay", screen, new Vector2(660, 180));
        var rt = bubble.rectTransform;
        UIStyle.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(380, -60), new Vector2(660, 180));
        rt.localEulerAngles = new Vector3(0, 0, -3);
        var title = UIStyle.Label("Title", rt, 46, UIStyle.Ink);
        UIStyle.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(600, 56));
        title.text = "PRESS  PLAY  TO  START";
        var sub = UIStyle.Label("Sub", rt, 24, new Color(0.4f, 0.4f, 0.45f));
        UIStyle.Anchor(sub.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(620, 30));
        sub.text = "EDITOR PREVIEW  •  TOOLS > HAUNTED HOUSE > BUILD MAP";

        var ghost = UIStyle.Icon("Ghost", screen, UIStyle.GhostIcon, GhostAI.Profiles[(int)GhostKind.Wraith].color, 160);
        UIStyle.Anchor(ghost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720, -230), new Vector2(160, 160));
        ghost.rectTransform.localEulerAngles = new Vector3(0, 0, 8);
        foreach (var d in new[] { new Vector2(5, -5), new Vector2(-5, 5) })
        {
            var o = ghost.gameObject.AddComponent<Outline>();
            o.effectColor = UIStyle.Ink;
            o.effectDistance = d;
        }

        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
    }

    void Clear()
    {
        if (canvas == null) return;
        if (Application.isPlaying) Destroy(canvas);
        else DestroyImmediate(canvas);
        canvas = null;
    }
}
