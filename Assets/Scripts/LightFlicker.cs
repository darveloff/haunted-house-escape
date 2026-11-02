using UnityEngine;

// Spooky flicker with the occasional short blackout.
// During the hunt (last key collected) every light turns red and flickers harder.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public static bool HuntMode;

    public float flickerAmount = 0.35f;
    public float flickerSpeed = 3f;
    public float blackoutChancePerSecond = 0.04f;

    static readonly Color HuntColor = new Color(1f, 0.12f, 0.08f);

    Light lightSource;
    float baseIntensity;
    Color baseColor;
    float seed;
    float blackoutUntil;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        baseColor = lightSource.color;
        seed = Random.value * 100f;
    }

    void Update()
    {
        lightSource.color = Color.Lerp(lightSource.color, HuntMode ? HuntColor : baseColor, Time.deltaTime * 2f);

        if (Time.time < blackoutUntil)
        {
            lightSource.intensity = baseIntensity * 0.05f;
            return;
        }
        float blackoutChance = blackoutChancePerSecond * (HuntMode ? 6f : 1f);
        if (Random.value < blackoutChance * Time.deltaTime)
            blackoutUntil = Time.time + Random.Range(0.08f, 0.4f);

        float amount = HuntMode ? flickerAmount * 1.6f : flickerAmount;
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed * (HuntMode ? 2.5f : 1f));
        lightSource.intensity = baseIntensity * (1f - amount + noise * amount * 2f);
    }
}
