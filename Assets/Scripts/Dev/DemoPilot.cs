using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

// Dev tool for the README GIF: steers the player through a short scripted run
// (sprint to the nearest key, then creep up on a ghost and flee once spotted) and saves
// one screenshot per frame. Time.captureFramerate keeps the recording smooth even when the
// editor is busy. Started from Tools > Haunted House > Debug > Record Gameplay GIF Frames.
public class DemoPilot : MonoBehaviour
{
    public int frameCount = 110;
    public int framesPerSecond = 10;
    public string outputDir = "Logs/GifFrames";

    enum Phase { ToKey, ToGhost, Flee }

    PlayerController player;
    NavMeshPath path;
    Phase phase = Phase.ToKey;
    int keysAtStart;

    IEnumerator Start()
    {
        player = GetComponent<PlayerController>();
        path = new NavMeshPath();
        keysAtStart = GameManager.Instance != null ? GameManager.Instance.KeysCollected : 0;
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        Directory.CreateDirectory(outputDir);
        Time.captureFramerate = framesPerSecond;

        for (int i = 0; i < frameCount; i++)
        {
            Steer();
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(outputDir, $"frame_{i:000}.jpg"), tex.EncodeToJPG(88));
            Destroy(tex);
        }

        VirtualInput.Move = Vector2.zero;
        VirtualInput.Sprint = false;
        Time.captureFramerate = 0;
        Debug.Log($"HauntedHouse: GIF frames done ({frameCount} in {outputDir})");
        Destroy(this);
    }

    void Steer()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsOver) { VirtualInput.Move = Vector2.zero; return; }

        Vector3 pos = player.transform.position;
        GhostAI nearestGhost = Nearest(FindObjectsByType<GhostAI>(FindObjectsSortMode.None), pos);

        if (phase == Phase.ToKey && gm.KeysCollected > keysAtStart) phase = Phase.ToGhost;
        if (phase == Phase.ToGhost && nearestGhost != null && nearestGhost.IsChasing) phase = Phase.Flee;

        Vector3 target;
        switch (phase)
        {
            case Phase.ToKey:
                var key = Nearest(FindObjectsByType<KeyPickup>(FindObjectsSortMode.None), pos);
                if (key == null) { phase = Phase.ToGhost; return; }
                target = key.transform.position;
                VirtualInput.Sprint = true; // shows the noise ring
                break;
            case Phase.ToGhost:
                if (nearestGhost == null) return;
                target = nearestGhost.transform.position;
                VirtualInput.Sprint = false;
                break;
            default:
                // Run straight away from the ghost that spotted us.
                if (nearestGhost == null) return;
                target = pos + (pos - nearestGhost.transform.position).normalized * 10f;
                VirtualInput.Sprint = true;
                break;
        }

        Vector3 next = target;
        if (NavMesh.CalculatePath(pos, target, NavMesh.AllAreas, path) && path.corners.Length > 1)
            next = path.corners[1];
        Vector3 dir = next - pos;
        dir.y = 0f;
        // The camera looks along +Z, so screen-up is world +Z and screen-right is world +X.
        VirtualInput.Move = dir.sqrMagnitude > 0.01f ? new Vector2(dir.x, dir.z).normalized : Vector2.zero;
    }

    static T Nearest<T>(T[] items, Vector3 pos) where T : Component
    {
        T best = null;
        float bestDist = float.MaxValue;
        foreach (var item in items)
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(item.transform.position, pos);
            if (d < bestDist) { bestDist = d; best = item; }
        }
        return best;
    }
}
