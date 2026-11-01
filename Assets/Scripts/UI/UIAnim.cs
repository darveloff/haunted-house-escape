using UnityEngine;
using UnityEngine.EventSystems;

// Small procedural UI motion: pop-in with overshoot, idle bob and wobble.
// Runs on unscaled time so it also animates on the title and pause screens.
public class UIAnim : MonoBehaviour
{
    public float popDelay;
    public float bobAmount;
    public float bobSpeed = 2f;
    public float wobbleAmount;
    public float wobbleSpeed = 1.5f;
    public float phase;

    RectTransform rt;
    Vector2 basePos;
    float baseRotation;
    float t;
    bool captured;

    void OnEnable()
    {
        rt = (RectTransform)transform;
        if (!captured)
        {
            basePos = rt.anchoredPosition;
            baseRotation = rt.localEulerAngles.z;
            captured = true;
        }
        t = 0f;
        rt.localScale = Vector3.zero;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01((t - popDelay) / 0.4f);
        rt.localScale = Vector3.one * EaseOutBack(p);
        // Only touch position/rotation when asked to, so layout groups stay in charge otherwise.
        float time = t + phase;
        if (bobAmount != 0f) rt.anchoredPosition = basePos + Vector2.up * Mathf.Sin(time * bobSpeed) * bobAmount;
        if (wobbleAmount != 0f) rt.localEulerAngles = new Vector3(0, 0, baseRotation + Mathf.Sin(time * wobbleSpeed) * wobbleAmount);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // Jump every running animation past its pop-in (used by the editor screenshot tool).
    public static void FinishAll()
    {
        foreach (var a in FindObjectsByType<UIAnim>(FindObjectsSortMode.None)) a.t = Mathf.Max(a.t, a.popDelay + 1f);
    }

    public static UIAnim Add(Component target, float delay, float bob = 0f, float wobble = 0f, float phase = 0f)
    {
        var a = target.gameObject.AddComponent<UIAnim>();
        a.popDelay = delay;
        a.bobAmount = bob;
        a.wobbleAmount = wobble;
        a.phase = phase;
        return a;
    }
}

// Just reports whether the pointer is over this element.
public class PointerFlag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool Hovered { get; private set; }
    public void OnPointerEnter(PointerEventData e) => Hovered = true;
    public void OnPointerExit(PointerEventData e) => Hovered = false;
    void OnDisable() => Hovered = false;
}

// Hover nudge for pill buttons: grows a little and tilts.
public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool Hovered { get; private set; }
    float amount;

    public void OnPointerEnter(PointerEventData e) => Hovered = true;
    public void OnPointerExit(PointerEventData e) => Hovered = false;
    void OnDisable() { Hovered = false; amount = 0f; transform.localScale = Vector3.one; transform.localEulerAngles = Vector3.zero; }

    void Update()
    {
        amount = Mathf.MoveTowards(amount, Hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        transform.localScale = Vector3.one * (1f + 0.06f * amount);
        transform.localEulerAngles = new Vector3(0, 0, -1.5f * amount);
    }
}
