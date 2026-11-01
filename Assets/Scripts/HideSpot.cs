using UnityEngine;

// A nook the player can crouch in with E. Hidden players can't be seen or caught.
[RequireComponent(typeof(BoxCollider))]
public class HideSpot : MonoBehaviour
{
    public static HideSpot Current { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) Current = this;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && Current == this) Current = null;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
    }
}
