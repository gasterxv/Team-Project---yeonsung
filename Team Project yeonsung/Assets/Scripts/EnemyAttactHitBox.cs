using System.Collections.Generic;
using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    public enum EnemyAttackType
    {
        Pattern1,
        Pattern2,
        Pattern3,
        Pattern4
    }

    [SerializeField] private EnemyAttackType enemyAttackType;

    [SerializeField] private int Pattern1 = 10;
    [SerializeField] private int Pattern2 = 10;
    [SerializeField] private int Pattern3 = 10;
    [SerializeField] private int Pattern4 = 10;

    private EnemyBase owner;
    private Vector2 attackOrigin;
    private bool initialized;

    private readonly HashSet<PlayerController> hitPlayers =
        new HashSet<PlayerController>();

    // JabMob1이 공격박스를 생성한 직후 호출합니다.
    public void Initialize(EnemyBase attacker, Vector2 attackerPosition)
    {
        owner = attacker;
        attackOrigin = attackerPosition;
        initialized = true;
        hitPlayers.Clear();
    }

    public int GiveDamage()
    {
        switch (enemyAttackType)
        {
            case EnemyAttackType.Pattern1:
                return Pattern1;

            case EnemyAttackType.Pattern2:
                return Pattern2;

            case EnemyAttackType.Pattern3:
                return Pattern3;

            case EnemyAttackType.Pattern4:
                return Pattern4;

            default:
                return 0;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHitPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHitPlayer(other);
    }

    private void TryHitPlayer(Collider2D other)
    {
        if (Time.timeScale == 0f) return;

        // 초기화된 몬스터 공격박스는 주인이 사라지거나 죽으면 무효입니다.
        if (initialized &&
            (owner == null ||
             owner.IsDead ||
             !owner.gameObject.activeInHierarchy ||
             owner.CurrentState != EnemyBase.MonsterState.Attack))
        {
            return;
        }

        // 플레이어 공격용 Trigger는 제외합니다.
        if (other.isTrigger) return;

        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        if (player == null ||
            player.IsDead ||
            player.IsInvincible ||
            hitPlayers.Contains(player))
        {
            return;
        }

        int damage = GiveDamage();
        if (damage <= 0) return;

        // 다른 코드에서 초기화 없이 생성한 경우에는 공격박스 위치 사용
        Vector2 origin = initialized
            ? attackOrigin
            : (Vector2)transform.position;

        int previousHealth = player.CurrentHealth;

        // 기존 플레이어의 피해 + 넉백 처리
        player.TakeDamageFrom(damage, origin);

        // 실제 피해가 적용되었을 때만 중복 공격을 차단합니다.
        if (player.CurrentHealth < previousHealth)
        {
            hitPlayers.Add(player);
        }
    }

    private void OnDisable()
    {
        hitPlayers.Clear();
        owner = null;
        initialized = false;
    }
}