using UnityEngine;

public class JabMob1 : EnemyBase
{
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackSpawnPoint;

    [Header("공격 히트박스 시간")]
    [SerializeField, Min(0f)] private float attackBoxSpawnDelay = 0.5f;
    [SerializeField, Min(0.01f)] private float attackBoxLifetime = 0.2f;
    [SerializeField, Min(0.01f)] private float attackDuration = 3f;

    private GameObject activeAttackBox;

    protected override void OnStateChanged(MonsterState newState)
    {
        ClearAttack();
        base.OnStateChanged(newState);

        if (newState != MonsterState.Attack) return;

        if (attackBoxSpawnDelay <= 0f)
        {
            SpawnAttackBox();
        }
        else
        {
            Invoke(nameof(SpawnAttackBox), attackBoxSpawnDelay);
        }

        float duration = Mathf.Max(
            attackDuration,
            attackBoxSpawnDelay + attackBoxLifetime);

        Invoke(nameof(FinishTimedAttack), duration);
    }

    private void SpawnAttackBox()
    {
        if (CurrentState != MonsterState.Attack || IsDead) return;

        if (attackBoxPrefab == null || attackSpawnPoint == null)
        {
            Debug.LogWarning(
                "공격박스 프리팹과 생성 위치를 연결해주세요.", this);
            return;
        }

        if (attackBoxPrefab.GetComponent<EnemyHitbox>() == null)
        {
            Debug.LogError(
                "공격박스 프리팹 루트에 EnemyHitbox를 붙여주세요.", this);
            return;
        }

        Vector3 localPosition =
            transform.InverseTransformPoint(attackSpawnPoint.position);

        localPosition.x =
            Mathf.Abs(localPosition.x) * FacingDirection;

        activeAttackBox = Instantiate(
            attackBoxPrefab,
            transform.TransformPoint(localPosition),
            transform.rotation);

        activeAttackBox.SetActive(true);

        EnemyHitbox hitbox =
            activeAttackBox.GetComponent<EnemyHitbox>();

        hitbox.Initialize(this, transform.position);

        Destroy(activeAttackBox, attackBoxLifetime);
    }

    private void FinishTimedAttack()
    {
        EndAttack();
    }

    private void ClearAttack()
    {
        CancelInvoke(nameof(SpawnAttackBox));
        CancelInvoke(nameof(FinishTimedAttack));

        if (activeAttackBox != null)
        {
            activeAttackBox.SetActive(false);
            Destroy(activeAttackBox);
            activeAttackBox = null;
        }
    }

    protected override void OnDisable()
    {
        ClearAttack();
        base.OnDisable();
    }

    protected override void GetDamage(int damageValue)
    {
        int previousHP = MonsterCurrentHP;

        MonsterCurrentHP =
            Mathf.Max(0, MonsterCurrentHP - damageValue);

        Debug.Log(
            $"[{name}] HP: {previousHP} → {MonsterCurrentHP}, " +
            $"받은 데미지: {damageValue}",
            this);
    }
}