using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NanoCapsulePickup : MonoBehaviour
{
    private bool collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        NanoCapsuleUse inventory = player.GetComponent<NanoCapsuleUse>();
        if (inventory == null)
        {
            Debug.LogWarning("Player needs NanoCapsuleUse to collect a Nano Repair Capsule.", player);
            return;
        }

        if (!inventory.TryAddHealPotion())
        {
            return;
        }

        collected = true;
        Destroy(gameObject);
    }
}