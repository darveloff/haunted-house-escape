using UnityEngine;

// Trigger behind the exit door. Reaching it with every key wins the game.
public class ExitZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var gm = GameManager.Instance;
        if (gm != null && gm.HasAllKeys) gm.Win();
    }
}
