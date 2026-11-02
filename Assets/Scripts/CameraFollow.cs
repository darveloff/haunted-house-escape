using UnityEngine;

// High-angle follow camera. The kit has no ceilings, so looking down works well.
// Also carries the "ears" (AudioListener) at the player so 3D ghost sounds are heard
// from the player's position, panned to match the screen.
// Supports a screen shake and a short "look over there" focus (used for the exit door).
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Transform ears;
    public Vector3 offset = new Vector3(0f, 14f, -8f);
    public float smoothTime = 0.15f;

    Vector3 velocity;
    float orbit;

    float shakeAmount, shakeUntil, shakeDuration;
    Vector3 focusPoint;
    float focusUntil;

    void Start()
    {
        if (target != null) transform.position = target.position + offset;
    }

    public void Shake(float amount, float duration)
    {
        shakeAmount = amount;
        shakeDuration = duration;
        shakeUntil = Time.unscaledTime + duration;
    }

    // Temporarily look at a point (real-time seconds), then glide back to the player.
    public void FocusOn(Vector3 point, float duration)
    {
        focusPoint = point;
        focusUntil = Time.unscaledTime + duration;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Title screen: slow orbit high above the house as a live backdrop.
        var gm = GameManager.Instance;
        if (gm != null && gm.CurrentState == GameManager.State.Title)
        {
            orbit += Time.unscaledDeltaTime * 6f;
            transform.position = target.position + Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, 26f, -24f);
            transform.LookAt(target.position + Vector3.forward * 20f);
            velocity = Vector3.zero;
            return;
        }

        Vector3 lookAt = Time.unscaledTime < focusUntil ? focusPoint : target.position;
        Vector3 goal = lookAt + offset;
        // Unscaled so the exit-door pan still moves during the slow-motion moment.
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime * (Time.unscaledTime < focusUntil ? 3f : 1f),
            Mathf.Infinity, Time.unscaledDeltaTime);
        transform.LookAt(transform.position - offset + Vector3.up);

        if (Time.unscaledTime < shakeUntil)
        {
            float k = (shakeUntil - Time.unscaledTime) / shakeDuration;
            transform.position += Random.insideUnitSphere * shakeAmount * k;
        }

        if (ears != null)
        {
            ears.position = target.position + Vector3.up * 1.2f;
            ears.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }
    }
}
