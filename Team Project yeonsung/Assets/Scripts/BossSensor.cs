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

    private PlayerController currentPlayer;
    private Rigidbody2D playerBody;
    private Rigidbody2D bossBody;

    private Collider2D[] bossColliders;
    private Collider2D[] playerColliders;

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

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<BossController>();
        }

        if (owner != null)
        {
            bossBody = owner.GetComponent<Rigidbody2D>();

            bossColliders =
                owner.GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void FixedUpdate()
    {
        // Body 센서에서만 몸끼리 밀리지 않도록 처리합니다.
        if (sensorType != SensorType.Body ||
            owner == null ||
            bossBody == null)
        {
            return;
        }

        FindCurrentPlayer();
        IgnoreBodyCollisions();
    }

    private void FindCurrentPlayer()
    {
        if (currentPlayer != null &&
            currentPlayer.gameObject.activeInHierarchy &&
            playerBody != null)
        {
            return;
        }

        currentPlayer = FindFirstObjectByType<PlayerController>();

        playerBody = null;
        playerColliders = null;

        if (currentPlayer == null) return;

        playerBody = currentPlayer.GetComponent<Rigidbody2D>();

        playerColliders =
            currentPlayer.GetComponentsInChildren<Collider2D>(true);
    }

    private void IgnoreBodyCollisions()
    {
        if (playerBody == null ||
            bossColliders == null ||
            playerColliders == null)
        {
            return;
        }

        foreach (Collider2D bossCollider in bossColliders)
        {
            // 센서와 별도의 Rigidbody2D를 가진 공격 오브젝트는 제외
            if (bossCollider == null ||
                !bossCollider.isActiveAndEnabled ||
                bossCollider.isTrigger ||
                bossCollider.attachedRigidbody != bossBody)
            {
                continue;
            }

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null ||
                    !playerCollider.isActiveAndEnabled ||
                    playerCollider.isTrigger ||
                    playerCollider.attachedRigidbody != playerBody)
                {
                    continue;
                }

                // 몸끼리만 통과시킵니다.
                // 바닥 충돌 및 Trigger 센서는 유지합니다.
                Physics2D.IgnoreCollision(
                    bossCollider,
                    playerCollider,
                    true);
            }
        }
    }

    private void RemoveInvalid(HashSet<Collider2D> colliders)
    {
        colliders.RemoveWhere(
            collider =>
                collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandleExit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleEnter(collision.collider);
        ReportBounceContact(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HandleExit(collision.collider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ReportDashContact(other);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ReportDashContact(collision.collider);
        ReportBounceContact(collision);
    }

    private void ReportBounceContact(Collision2D collision)
    {
        if (sensorType == SensorType.Body && owner != null)
        {
            owner.NotifyPattern3Collision(collision);
        }
    }

    private void ReportDashContact(Collider2D other)
    {
        if (sensorType == SensorType.Body && owner != null)
        {
            owner.NotifyPattern2Contact(other);
        }
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

        ReportDashContact(other);

        if (other.CompareTag(dashFloorTag))
        {
            dashFloors.Add(other);
        }

        if (other.CompareTag(playerAttackTag) &&
            other.TryGetComponent<PlayerHitbox>(out var hitbox) &&
            owner != null)
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

        currentPlayer = null;
        playerBody = null;
        playerColliders = null;
    }
}