using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Floor plan drawn from the baked NavMesh, with player, keys and exit always shown.
// Ghosts blink onto the radar: hidden for revealInterval seconds, then shown for revealDuration.
public class Minimap : MonoBehaviour
{
    public float revealInterval = 5f;
    public float revealDuration = 5f;
    public int resolution = 384;

    static readonly Color FloorColor = new Color(0.36f, 0.38f, 0.48f, 1f);
    static readonly Color WallColor = new Color(0.92f, 0.92f, 0.96f, 1f);

    RectTransform map;
    Vector2 worldMin;
    float worldSize;

    PlayerController player;
    RectTransform playerMarker;
    readonly List<(Transform target, RectTransform marker)> ghosts = new List<(Transform, RectTransform)>();
    readonly List<(Transform target, RectTransform marker)> keys = new List<(Transform, RectTransform)>();
    Image exitMarker;
    Transform exit;
    Text radarText;
    Image radarIcon;
    bool wasVisible;

    public void Build(RectTransform parent)
    {
        player = FindAnyObjectByType<PlayerController>();

        var frame = UIStyle.Panel("Minimap", parent, radius: 26, border: 5);
        var frameRt = frame.rectTransform;
        UIStyle.Anchor(frameRt, new Vector2(0, 1), new Vector2(32, -32), new Vector2(400, 450));

        map = UIStyle.Rect("Map", frameRt);
        UIStyle.Anchor(map, new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(360, 360));
        map.gameObject.AddComponent<RawImage>().texture = BuildTexture();

        var radarRow = UIStyle.Rect("Radar", frameRt);
        UIStyle.Anchor(radarRow, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(360, 40));
        radarIcon = UIStyle.Icon("Icon", radarRow, UIStyle.GhostIcon, UIStyle.Paper, 34);
        radarIcon.rectTransform.anchorMin = radarIcon.rectTransform.anchorMax = new Vector2(0, 0.5f);
        radarIcon.rectTransform.anchoredPosition = new Vector2(24, 0);
        radarText = UIStyle.Label("Text", radarRow, 24, UIStyle.Paper, TextAnchor.MiddleLeft);
        radarText.rectTransform.anchorMin = new Vector2(0, 0);
        radarText.rectTransform.anchorMax = new Vector2(1, 1);
        radarText.rectTransform.offsetMin = new Vector2(52, 0);
        radarText.rectTransform.offsetMax = Vector2.zero;

        var exitZone = FindAnyObjectByType<ExitZone>();
        if (exitZone != null)
        {
            exit = exitZone.transform;
            exitMarker = UIStyle.Icon("Exit", map, UIStyle.RoundedSprite(6, 0, Color.white, Color.white), UIStyle.Danger, 18);
            exitMarker.type = Image.Type.Sliced;
        }
        foreach (var spot in FindObjectsByType<HideSpot>(FindObjectsSortMode.None))
            Place(UIStyle.Icon("Hide", map, UIStyle.DotIcon, new Color(0.65f, 0.85f, 1f, 0.8f), 11).rectTransform, spot.transform.position);
        foreach (var key in FindObjectsByType<KeyPickup>(FindObjectsSortMode.None))
            keys.Add((key.transform, UIStyle.Icon("Key", map, UIStyle.KeyIcon, UIStyle.Gold, 26).rectTransform));
        foreach (var ghost in FindObjectsByType<GhostAI>(FindObjectsSortMode.None))
        {
            var icon = UIStyle.Icon("Ghost", map, UIStyle.GhostIcon, ghost.Profile.color, 30);
            icon.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
            ghosts.Add((ghost.transform, icon.rectTransform));
        }
        playerMarker = UIStyle.Icon("Player", map, UIStyle.ArrowIcon, Color.white, 26).rectTransform;
        playerMarker.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
    }

    void Update()
    {
        if (map == null) return;

        if (player != null)
        {
            Place(playerMarker, player.transform.position);
            playerMarker.localEulerAngles = new Vector3(0, 0, -player.transform.eulerAngles.y);
        }

        for (int i = keys.Count - 1; i >= 0; i--)
        {
            if (keys[i].target == null)
            {
                Destroy(keys[i].marker.gameObject);
                keys.RemoveAt(i);
            }
            else Place(keys[i].marker, keys[i].target.position);
        }

        if (exit != null)
        {
            Place(exitMarker.rectTransform, exit.position);
            bool open = GameManager.Instance != null && GameManager.Instance.HasAllKeys;
            exitMarker.color = open ? new Color(0.35f, 1f, 0.45f) : UIStyle.Danger;
        }

        // Radar cycle: hidden, then visible.
        float cycle = revealInterval + revealDuration;
        float t = Time.timeSinceLevelLoad % cycle;
        bool visible = t >= revealInterval;
        float remaining = visible ? cycle - t : revealInterval - t;
        float pop = visible ? 1f + 0.35f * Mathf.Clamp01(1f - (t - revealInterval) * 3f) : 1f;
        if (visible && !wasVisible && GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.State.Playing)
            AudioManager.Instance?.Play(AudioSynth.RadarPing(), 0.12f);
        wasVisible = visible;
        foreach (var (target, marker) in ghosts)
        {
            marker.gameObject.SetActive(visible && target != null);
            if (visible && target != null)
            {
                Place(marker, target.position);
                marker.localScale = Vector3.one * pop;
            }
        }
        radarText.text = visible ? $"GHOSTS VISIBLE  {Mathf.CeilToInt(remaining)}" : $"RADAR IN  {Mathf.CeilToInt(remaining)}";
        radarIcon.color = visible ? UIStyle.Danger : UIStyle.Paper;
    }

    void Place(RectTransform marker, Vector3 world)
    {
        Vector2 n = (new Vector2(world.x, world.z) - worldMin) / worldSize;
        marker.anchorMin = marker.anchorMax = n;
        marker.anchoredPosition = Vector2.zero;
    }

    // Rasterise the floor-level NavMesh, keep only the part connected to the player,
    // then outline it so it reads like walls.
    Texture2D BuildTexture()
    {
        var tri = NavMesh.CalculateTriangulation();
        var verts = tri.vertices;
        int res = resolution;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var v in verts)
        {
            if (v.y > 1.5f) continue;
            min = Vector2.Min(min, new Vector2(v.x, v.z));
            max = Vector2.Max(max, new Vector2(v.x, v.z));
        }
        worldSize = Mathf.Max(max.x - min.x, max.y - min.y) + 8f;
        worldMin = (min + max) * 0.5f - Vector2.one * worldSize * 0.5f;

        var walkable = new bool[res * res];
        for (int i = 0; i < tri.indices.Length; i += 3)
        {
            Vector3 a = verts[tri.indices[i]], b = verts[tri.indices[i + 1]], c = verts[tri.indices[i + 2]];
            if (a.y > 1.5f || b.y > 1.5f || c.y > 1.5f) continue;
            Vector2 pa = ToPixel(a, res), pb = ToPixel(b, res), pc = ToPixel(c, res);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
            int y1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (InTriangle(new Vector2(x + 0.5f, y + 0.5f), pa, pb, pc)) walkable[y * res + x] = true;
        }

        // Flood fill from the player so islands (wall tops, outside nooks) are dropped.
        if (player != null)
        {
            Vector2 start = ToPixel(player.transform.position, res);
            var keep = new bool[res * res];
            var stack = new Stack<int>();
            int s = Mathf.Clamp((int)start.y, 0, res - 1) * res + Mathf.Clamp((int)start.x, 0, res - 1);
            if (walkable[s]) { stack.Push(s); keep[s] = true; }
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                int px = p % res, py = p / res;
                for (int d = 0; d < 4; d++)
                {
                    int nx = px + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = py + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= res || ny >= res) continue;
                    int n = ny * res + nx;
                    if (walkable[n] && !keep[n]) { keep[n] = true; stack.Push(n); }
                }
            }
            if (stack.Count == 0 && keep[s]) walkable = keep;
        }

        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[res * res];
        const int wall = 3;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int i = y * res + x;
            if (walkable[i]) { pixels[i] = FloorColor; continue; }
            bool nearFloor = false;
            for (int dy = -wall; dy <= wall && !nearFloor; dy++)
            for (int dx = -wall; dx <= wall && !nearFloor; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < res && ny < res && walkable[ny * res + nx]) nearFloor = true;
            }
            pixels[i] = nearFloor ? WallColor : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    Vector2 ToPixel(Vector3 world, int res) => (new Vector2(world.x, world.z) - worldMin) / worldSize * res;

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
}
