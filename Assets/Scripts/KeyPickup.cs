using UnityEngine;

// Spinning key. Needs a trigger collider; the player's CharacterController fires OnTriggerEnter.
public class KeyPickup : MonoBehaviour
{
    public float spinSpeed = 120f;
    public float bobHeight = 0.2f;
    public float bobSpeed = 2.5f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (GameManager.Instance != null) GameManager.Instance.CollectKey();
        AudioManager.Instance?.Play(AudioSynth.KeyChime(), 0.35f);
        WorldFX.Burst(transform.position, new Color(1f, 0.85f, 0.35f, 1f), 14, 3.5f, 0.7f);
        WorldFX.Ring(transform.position, new Color(1f, 0.85f, 0.35f, 0.9f), 0.5f, 3.5f, 0.45f, flat: false);
        Destroy(gameObject);
    }
}
