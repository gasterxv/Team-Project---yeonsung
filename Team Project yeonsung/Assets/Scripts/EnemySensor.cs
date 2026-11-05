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
        Debug.Log($"[{name}] 접촉: {other.name}, 태그: {other.tag}, 센서: {sensorType}",this);

        if (sensorType == SensorType.PlayerRange)
        {
            if (other.CompareTag("Player"))
            {
                playerColliders.Add(other);

                Debug.Log($"[{name}] 플레이어 등록 성공", this);
            }

            return;
        }

        if (other.CompareTag(playerAttackTag) &&
            other.TryGetComponent<PlayerHitbox>(out var hitbox))
        {
            owner.NotifyPlayerAttack(hitbox.GiveDamage());
        }
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