using UnityEngine;

public class JabMob1 : EnemyBase
{
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackSpawnPoint;

    [Header("공격 히트박스 시간")]
    [SerializeField, Min(0f)]
    private float attackBoxSpawnDelay = 0.5f; // N초

    [SerializeField, Min(0.01f)]
    private float attackBoxLifetime = 0.2f;   // C초

    protected override void OnStateChanged(MonsterState newState)
    {
        // 상태가 바뀌면 남아 있는 예약 취소
        CancelInvoke(nameof(SpawnAttackBox));
        CancelInvoke(nameof(FinishTimedAttack));

        base.OnStateChanged(newState);

        if (newState == MonsterState.Attack)
        {
            // N초 뒤 공격박스 생성
            if (attackBoxSpawnDelay <= 0f)
            {
                SpawnAttackBox();
            }
            else
            {
                Invoke(nameof(SpawnAttackBox), attackBoxSpawnDelay);
            }

            // 기존 공격 상태 유지 시간
            Invoke(nameof(FinishTimedAttack), 3f);
        }
    }

    private void SpawnAttackBox()
    {
        // 생성 전에 공격이 취소됐다면 생성하지 않음
        if (CurrentState != MonsterState.Attack)
            return;

        if (attackBoxPrefab == null || attackSpawnPoint == null)
        {
            Debug.LogWarning(
                "공격박스 프리팹과 생성 위치를 연결해주세요.",
                this
            );
            return;
        }

        Vector3 localPosition = attackSpawnPoint.localPosition;
        localPosition.x =
            Mathf.Abs(localPosition.x) * FacingDirection;

        Vector3 spawnPosition =
            transform.TransformPoint(localPosition);

        GameObject attackBox = Instantiate(
            attackBoxPrefab,
            spawnPosition,
            transform.rotation
        );

        // 생성 시점부터 C초 뒤 삭제
        Destroy(attackBox, attackBoxLifetime);
    }

    private void FinishTimedAttack()
    {
        EndAttack();
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(SpawnAttackBox));
        CancelInvoke(nameof(FinishTimedAttack));
    }

    protected override void GetDamage(int damageValue)
    {
        MonsterCurrentHP =
            Mathf.Max(0, MonsterCurrentHP - damageValue);
    }
}