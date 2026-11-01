using UnityEngine;

// Spooky flicker with the occasional short blackout.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public float flickerAmount = 0.35f;
    public float flickerSpeed = 3f;
    public float blackoutChancePerSecond = 0.04f;

    Light lightSource;
    float baseIntensity;
    float seed;
    float blackoutUntil;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (Time.time < blackoutUntil)
        {
            lightSource.intensity = baseIntensity * 0.05f;
            return;
        }
        if (Random.value < blackoutChancePerSecond * Time.deltaTime)
            blackoutUntil = Time.time + Random.Range(0.08f, 0.4f);

        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
        lightSource.intensity = baseIntensity * (1f - flickerAmount + noise * flickerAmount * 2f);
    }
}
