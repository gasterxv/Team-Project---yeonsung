using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NanoCapsuleCheckpointRefill : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        NanoCapsuleUse inventory = player.GetComponent<NanoCapsuleUse>();
        if (inventory == null)
        {
            Debug.LogWarning("Player needs NanoCapsuleUse to refill at a checkpoint.", player);
            return;
        }

        inventory.RefillAtCheckpoint();
    }
}