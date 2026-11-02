using UnityEngine;

// First-run tips, shown once each at the moment they're useful. Added by GameManager
// until the player escapes for the first time (SaveData.TutorialDone).
public class Tutorial : MonoBehaviour
{
    const float GhostNoticeDistance = 14f;

    GameManager gm;
    PlayerController player;
    GhostAI[] ghosts;
    float startTime;
    bool mapTip, ghostTip, sprintTip, radarTip, coverTip;
    float busyUntil; // don't stack tips on top of each other

    void Start()
    {
        gm = GameManager.Instance;
        player = FindAnyObjectByType<PlayerController>();
        ghosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);
        startTime = Time.time;
        busyUntil = Time.time + 5.5f; // let the opening message show first
    }

    void Update()
    {
        if (gm == null || player == null || gm.CurrentState != GameManager.State.Playing) return;
        if (Time.time < busyUntil) return;

        if (!mapTip)
        {
            Tip(ref mapTip, "TIP: GOLD KEYS ARE MARKED\nON THE MAP (TOP LEFT)");
            return;
        }
        if (!radarTip && Time.time - startTime > 12f)
        {
            Tip(ref radarTip, "TIP: THE MAP REVEALS EVERY GHOST\nFOR 5 SECONDS, EVERY 5 SECONDS");
            return;
        }
        if (!ghostTip && NearestGhostDistance() < GhostNoticeDistance)
        {
            Tip(ref ghostTip, "TIP: THAT'S A GHOST!\nSTAY OUT OF ITS LINE OF SIGHT");
            return;
        }
        if (!sprintTip && player.IsSprinting)
        {
            Tip(ref sprintTip, "TIP: SPRINTING IS LOUD.\nTHE RING SHOWS WHO CAN HEAR YOU");
            return;
        }
        if (!coverTip && ghostTip && Time.time - startTime > 30f)
        {
            Tip(ref coverTip, "TIP: DUCK BEHIND WALLS AND PILLARS.\nGHOSTS CAN'T SEE THROUGH THEM");
            return;
        }
        if (mapTip && radarTip && ghostTip && sprintTip && coverTip) Destroy(this);
    }

    void Tip(ref bool shown, string text)
    {
        shown = true;
        gm.ShowMessage(text, 4.5f);
        busyUntil = Time.time + 6f;
    }

    float NearestGhostDistance()
    {
        float best = float.MaxValue;
        foreach (var g in ghosts)
            if (g != null) best = Mathf.Min(best, Vector3.Distance(g.transform.position, player.transform.position));
        return best;
    }
}
