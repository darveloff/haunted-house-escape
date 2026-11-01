using UnityEngine;

// Slides the gate's door up once every key is collected.
public class ExitDoor : MonoBehaviour
{
    public Transform door;
    public Light statusLight;
    public float openHeight = 4.4f;
    public float openTime = 2f;

    Vector3 closedPos;
    float progress;
    bool opening;

    void Start()
    {
        if (door == null) door = transform.Find("door");
        closedPos = door.position;
        if (GameManager.Instance != null) GameManager.Instance.AllKeysCollected += Open;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.AllKeysCollected -= Open;
    }

    public void Open()
    {
        opening = true;
        AudioManager.Instance?.Play(AudioSynth.DoorRumble(), 0.4f);
        if (statusLight != null) statusLight.color = new Color(0.3f, 1f, 0.4f);
    }

    void Update()
    {
        if (!opening || progress >= 1f) return;
        progress = Mathf.Min(1f, progress + Time.deltaTime / openTime);
        door.position = closedPos + Vector3.up * openHeight * Mathf.SmoothStep(0f, 1f, progress);
    }
}
