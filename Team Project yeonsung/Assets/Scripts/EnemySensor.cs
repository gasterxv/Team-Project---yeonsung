using System.Collections.Generic;
using UnityEngine;

public class EnemySensor : MonoBehaviour
{
    public enum SensorType
    {
        PlayerRange,
        Hurtbox
    }

    [SerializeField] private EnemyBase owner;
    [SerializeField] private SensorType sensorType;
    [SerializeField] private string playerAttackTag = "PlayerAttack";

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    public Collider2D PlayerCollider
    {
        get
        {
            playerColliders.RemoveWhere( collider =>collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy );

            foreach (Collider2D collider in playerColliders)
            {
                return collider;
            }

            return null;
        }
    }

    public bool HasPlayer => PlayerCollider != null;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(
            $"[{name}] 접촉: {other.name}, " +
            $"태그: {other.tag}, 센서: {sensorType}",
            this
        );

        if (sensorType == SensorType.PlayerRange)
        {
            if (other.CompareTag("Player"))
                playerColliders.Add(other);

            return;
        }

        if (!other.CompareTag(playerAttackTag))
        {
            Debug.Log($"[{name}] 공격 태그 불일치: {other.tag}", this);
            return;
        }

        if (!other.TryGetComponent<PlayerHitbox>(out var hitbox))
        {
            Debug.LogWarning(
                $"[{name}] {other.name}에 PlayerHitbox가 없습니다. " +
                "공격 Collider와 같은 오브젝트에 있는지 확인하세요.",
                other
            );
            return;
        }

        if (owner == null)
        {
            Debug.LogError($"[{name}] Hurtbox의 Owner가 비어 있습니다.", this);
            return;
        }

        int damage = hitbox.GiveDamage();

        Debug.Log(
            $"[{name}] 데미지 전달 → {owner.name}, " +
            $"데미지: {damage}, 상태: {owner.CurrentState}",
            this
        );

        owner.NotifyPlayerAttack(damage);
    }


    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
    }
}