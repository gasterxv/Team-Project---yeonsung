using System.Collections.Generic;
using UnityEngine;

public class BossSensor : MonoBehaviour
{
    public enum SensorType { Pattern1Range, Body }

    [SerializeField] private BossController owner;
    [SerializeField] private SensorType sensorType;
    [Header("태그")]
    [SerializeField] private string playerBodyTag = "Player";
    [SerializeField] private string playerAttackTag = "PlayerAttack";
    [SerializeField] private string dashFloorTag = "BoseRoomDashFloor";

    private readonly HashSet<Collider2D> players = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> dashFloors = new HashSet<Collider2D>();

    public bool HasPlayer
    {
        get { RemoveInvalid(players); return players.Count > 0; }
    }

    public bool IsTouchingDashFloor
    {
        get { RemoveInvalid(dashFloors); return dashFloors.Count > 0; }
    }

    private void Awake()
    {
        if (owner == null) owner = GetComponentInParent<BossController>();
    }

    private void RemoveInvalid(HashSet<Collider2D> colliders)
    {
        colliders.RemoveWhere(collider => collider == null || !collider.enabled ||
            !collider.gameObject.activeInHierarchy);
    }

    private void OnTriggerEnter2D(Collider2D other) { HandleEnter(other); }
    private void OnTriggerExit2D(Collider2D other) { HandleExit(other); }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleEnter(collision.collider);
        ReportBounceContact(collision);
    }
    private void OnCollisionExit2D(Collision2D collision) { HandleExit(collision.collider); }

    // 이미 접촉한 상태에서 패턴 2에 들어가도 정지 조건을 확인한다.
    private void OnTriggerStay2D(Collider2D other) { ReportDashContact(other); }
    private void OnCollisionStay2D(Collision2D collision)
    {
        ReportDashContact(collision.collider);
        ReportBounceContact(collision);
    }

    private void ReportBounceContact(Collision2D collision)
    {
        if (sensorType == SensorType.Body && owner != null)
            owner.NotifyPattern3Collision(collision);
    }

    private void ReportDashContact(Collider2D other)
    {
        if (sensorType == SensorType.Body && owner != null)
            owner.NotifyPattern2Contact(other);
    }

    private void HandleEnter(Collider2D other)
    {
        if (sensorType == SensorType.Pattern1Range)
        {
            if (other.CompareTag(playerBodyTag)) players.Add(other);
            return;
        }

        ReportDashContact(other);
        if (other.CompareTag(dashFloorTag)) dashFloors.Add(other);

        if (other.CompareTag(playerAttackTag) &&
            other.TryGetComponent<PlayerHitbox>(out var hitbox) && owner != null)
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
