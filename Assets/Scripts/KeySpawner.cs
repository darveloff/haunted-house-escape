using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The builder places a key at every candidate spot. Each run this keeps a random,
// spread-out handful and switches the rest off.
public class KeySpawner : MonoBehaviour
{
    public float minDistanceBetweenKeys = 24f;
    public float minDistanceFromPlayer = 20f;

    void Awake()
    {
        var gm = FindAnyObjectByType<GameManager>();
        int count = gm != null ? gm.keysRequired : 5;
        var player = FindAnyObjectByType<PlayerController>();
        Vector3 start = player != null ? player.transform.position : Vector3.zero;

        var candidates = GetComponentsInChildren<KeyPickup>(true).OrderBy(_ => Random.value).ToList();
        var chosen = new List<KeyPickup>();

        // Relax the spacing until we have enough keys.
        for (float spread = minDistanceBetweenKeys; chosen.Count < count && spread >= 0f; spread -= 6f)
        {
            foreach (var key in candidates)
            {
                if (chosen.Count >= count) break;
                if (chosen.Contains(key)) continue;
                Vector3 p = key.transform.position;
                if (Vector3.Distance(p, start) < minDistanceFromPlayer && spread > 0f) continue;
                if (chosen.Any(c => Vector3.Distance(c.transform.position, p) < spread)) continue;
                chosen.Add(key);
            }
        }

        foreach (var key in candidates)
            key.gameObject.SetActive(chosen.Contains(key));
    }
}
