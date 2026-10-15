using UnityEngine;

public class JabMob1 : EnemyBase
{
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackSpawnPoint;
    [SerializeField] private float attackBoxLifetime = 0.2f;

    protected override void OnStateChanged(MonsterState newState)
    {
        // 부모의 애니메이션 재생 처리 유지
        base.OnStateChanged(newState);

        if (newState == MonsterState.Attack)
        {
            SpawnAttackBox();
        }
    }

    private void SpawnAttackBox()
    {
        // 소환 지점은 오른쪽 공격 위치로 배치해 둠
        Vector3 localPosition = attackSpawnPoint.localPosition;

        // 바라보는 방향에 맞춰 좌우 위치 변경
        localPosition.x = Mathf.Abs(localPosition.x) * FacingDirection;

        Vector3 spawnPosition =
            transform.TransformPoint(localPosition);

        GameObject attackBox = Instantiate(
            attackBoxPrefab,
            spawnPosition,
            transform.rotation
        );

        Destroy(attackBox, attackBoxLifetime);
    }

    protected override void GetDamage(int damageValue)
    {
        MonsterCurrentHP =
            Mathf.Max(0, MonsterCurrentHP - damageValue);
    }
}