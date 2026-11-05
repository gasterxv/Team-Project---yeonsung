using UnityEngine;

public class BossHurtbox : MonoBehaviour
{
    [SerializeField] private BossController owner;
    [SerializeField] private string playerAttackTag = "PlayerAttack";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null)
            return;

        if (!other.CompareTag(playerAttackTag))
            return;

        if (other.TryGetComponent<PlayerHitbox>(out var hitbox))
        {
            int damageValue = hitbox.GiveDamage();

            owner.NotifyPlayerAttack(damageValue);
        }
    }
}