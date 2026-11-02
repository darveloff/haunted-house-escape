using System.Collections.Generic;
using UnityEngine;

// Tiny world-space effects (expanding rings, sparkle bursts) built from sprites.
// Used for the sprint noise ring and key pickups. GameManager supplies the material.
public class WorldFX : MonoBehaviour
{
    public static Material material;
    static WorldFX instance;

    struct Item
    {
        public SpriteRenderer sr;
        public float born, life, startScale, endScale, startAlpha;
        public Vector3 velocity;
        public bool billboard;
    }

    readonly List<Item> items = new List<Item>();

    static WorldFX Get()
    {
        if (instance == null) instance = new GameObject("WorldFX").AddComponent<WorldFX>();
        return instance;
    }

    // Ring that grows from one diameter to another while fading. Flat rings lie on the floor.
    public static void Ring(Vector3 pos, Color color, float fromDiameter, float toDiameter, float life, bool flat = true)
    {
        const float unit = 1f / 0.64f; // RingIcon is 0.64 units wide at scale 1
        var sr = Get().Spawn("Ring", UIStyle.RingIcon, color, pos);
        if (flat) sr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Get().items.Add(new Item { sr = sr, born = Time.time, life = life, startScale = fromDiameter * unit, endScale = toDiameter * unit, startAlpha = color.a, billboard = !flat });
    }

    // Glowing sparks flying outward and upward.
    public static void Burst(Vector3 pos, Color color, int count, float speed, float life)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Quaternion.Euler(0f, i * 360f / count + Random.Range(-15f, 15f), 0f) * Vector3.forward;
            var sr = Get().Spawn("Spark", UIStyle.GlowIcon, color, pos);
            Get().items.Add(new Item
            {
                sr = sr, born = Time.time, life = life * Random.Range(0.8f, 1.2f),
                startScale = 0.35f, endScale = 0.05f, startAlpha = color.a,
                velocity = dir * speed * Random.Range(0.7f, 1.3f) + Vector3.up * speed * Random.Range(0.4f, 1f),
                billboard = true,
            });
        }
    }

    SpriteRenderer Spawn(string name, Sprite sprite, Color color, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        if (material != null) sr.sharedMaterial = material;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return sr;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        float t = Time.time;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            float k = (t - it.born) / it.life;
            if (k >= 1f || it.sr == null)
            {
                if (it.sr != null) Destroy(it.sr.gameObject);
                items.RemoveAt(i);
                continue;
            }
            float ease = 1f - (1f - k) * (1f - k);
            it.sr.transform.localScale = Vector3.one * Mathf.Lerp(it.startScale, it.endScale, ease);
            if (it.velocity != Vector3.zero)
            {
                it.velocity += Physics.gravity * 0.25f * Time.deltaTime;
                it.sr.transform.position += it.velocity * Time.deltaTime;
            }
            var c = it.sr.color;
            c.a = it.startAlpha * (1f - k);
            it.sr.color = c;
            if (it.billboard && cam != null) it.sr.transform.rotation = cam.transform.rotation;
            items[i] = it;
        }
    }
}
