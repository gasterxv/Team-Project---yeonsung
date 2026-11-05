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

    // 같은 공격박스가 같은 플레이어를 여러 번 때리지 않도록 기록
    private readonly HashSet<PlayerController> hitPlayers =
        new HashSet<PlayerController>();

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
        // 플레이어 몸 콜라이더의 태그 검사
        if (!other.CompareTag("Player"))
            return;

        // 몸 콜라이더가 자식에 있어도 부모의 스크립트를 찾음
        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        if (player == null)
            return;

        // 같은 플레이어에게는 이 공격박스로 한 번만 호출
        if (!hitPlayers.Add(player))
            return;

        player.TakeDamage(GiveDamage());
    }

    private void OnDisable()
    {
        hitPlayers.Clear();
    }
}