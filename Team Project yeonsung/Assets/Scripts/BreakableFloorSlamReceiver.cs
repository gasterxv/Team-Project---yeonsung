using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BreakableFloorSlamReceiver : MonoBehaviour
{
    [SerializeField] private int slamDamage = 3;

    private BreakableFloor breakableFloor;

    private static readonly FieldInfo IsDivingField =
        typeof(PlayerController).GetField(
            "isDiving",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private void Awake()
    {
        breakableFloor = GetComponent<BreakableFloor>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();

        if (player == null && collision.rigidbody != null)
        {
            player = collision.rigidbody.GetComponent<PlayerController>();
        }

        if (player == null || IsDivingField == null ||
            !(bool)IsDivingField.GetValue(player))
        {
            return;
        }

        if (breakableFloor == null)
        {
            breakableFloor = GetComponent<BreakableFloor>();
        }

        if (breakableFloor != null)
        {
            breakableFloor.TakeDamage(slamDamage);
        }
    }
}