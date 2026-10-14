using System.Collections.Generic;
using UnityEngine;

public class BossSensor : MonoBehaviour
{
    public enum SensorType
    {
        Pattern1Range,
        Body
    }

    [SerializeField] private BossController owner;
    [SerializeField] private SensorType sensorType;

    [Header("태그")]
    [SerializeField] private string playerBodyTag = "Player";
    [SerializeField] private string playerAttackTag = "PlayerAttack";
    [SerializeField] private string dashFloorTag = "BoseRoomDashFloor";

    private readonly HashSet<Collider2D> players =
        new HashSet<Collider2D>();

    private readonly HashSet<Collider2D> dashFloors =
        new HashSet<Collider2D>();

    public bool HasPlayer
    {
        get
        {
            RemoveInvalid(players);
            return players.Count > 0;
        }
    }

    public bool IsTouchingDashFloor
    {
        get
        {
            RemoveInvalid(dashFloors);
            return dashFloors.Count > 0;
        }
    }

    private void RemoveInvalid(HashSet<Collider2D> colliders)
    {
        colliders.RemoveWhere(collider =>
            collider == null ||
            !collider.enabled ||
            !collider.gameObject.activeInHierarchy
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandleExit(other);
    }

    // 몸과 바닥이 일반 충돌을 사용하는 경우도 지원
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleEnter(collision.collider);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HandleExit(collision.collider);
    }

    private void HandleEnter(Collider2D other)
    {
        if (sensorType == SensorType.Pattern1Range)
        {
            if (other.CompareTag(playerBodyTag))
            {
                players.Add(other);
            }

            return;
        }

        if (other.CompareTag(dashFloorTag))
        {
            dashFloors.Add(other);
        }

        if (other.CompareTag(playerAttackTag) &&
            other.TryGetComponent<PlayerHitbox>(out var hitbox))
        {
            owner.Damaged(hitbox.GiveDamage());
        }
    }

    private void HandleExit(Collider2D other)
    {
        players.Remove(other);
        dashFloors.Remove(other);
    }

    private void OnDisable()
    {
        players.Clear();
        dashFloors.Clear();
    }
}