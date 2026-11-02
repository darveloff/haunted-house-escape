using UnityEngine;

// Movement written by the on-screen phone controls (TouchControls) and read by PlayerController.
public static class VirtualInput
{
    public static Vector2 Move;
    public static bool Sprint;

    // True on phones/tablets, where the on-screen controls are shown.
    public static bool IsTouchDevice =>
#if UNITY_EDITOR
        // Editor-only preview toggle (Tools > Haunted House > Debug > Preview Phone Controls).
        PlayerPrefs.GetInt("hh_preview_touch", 0) == 1 ||
#endif
        Application.isMobilePlatform || (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Mouse.current == null);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Move = Vector2.zero;
        Sprint = false;
    }
}
