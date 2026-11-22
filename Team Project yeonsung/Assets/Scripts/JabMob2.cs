using UnityEngine;

public class JabMob2 : EnemyBase
{
    [Header("공격 히트박스")]
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackSpawnPoint;

    [Header("공격 히트박스 시간")]
    [SerializeField, Min(0f)] private float attackBoxSpawnDelay = 0.5f;
    [SerializeField, Min(0.01f)] private float attackBoxLifetime = 0.2f;
    [SerializeField, Min(0.01f)] private float attackDuration = 3f;

    [Header("Animator의 상태 이름 - 대소문자까지 동일해야 합니다")]
    [SerializeField] private string idleStateName = "JabMob2Idle";
    [SerializeField] private string walkStateName = "JabMob2Walk";
    [SerializeField] private string attackStateName = "JabMob2Attack";
    [SerializeField] private string damagedStateName = "JabMob2Damaged";
    [SerializeField] private string deathStateName = "JabMob2Die";

    [Header("사망 후 자동 삭제")]
    [Tooltip("사망 애니메이션 종료 이벤트가 없어도 이 시간 후 삭제합니다. 게임 시간 기준입니다.")]
    [SerializeField, Min(0.1f)] private float deathCleanupDelay = 2f;

    [Header("피격 종료 이벤트 누락 대비")]
    [SerializeField, Min(0.01f)] private float damageRecoveryDelay = 0.5f;

    private GameObject activeAttackBox;

    protected override void Awake()
    {
        // EnemyBase의 Rigidbody2D, 초기 HP 등 초기화 유지
        base.Awake();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            Debug.LogError("몬스터의 Animator를 연결해주세요.", this);
        }
    }

    protected override void OnStateChanged(MonsterState newState)
    {
        ClearAttack();
        CancelInvoke(nameof(RecoverFromDamage));

        if (newState == MonsterState.Dead)
        {
            // 몸, 인식 센서, 피격 영역 등 모든 콜라이더 비활성화
            foreach (Collider2D col in
                     GetComponentsInChildren<Collider2D>(true))
            {
                col.enabled = false;
            }

            // 바닥 충돌도 없어지므로 시체가 떨어지지 않게 물리 정지
            foreach (Rigidbody2D rb in
                     GetComponentsInChildren<Rigidbody2D>(true))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }

            // 충돌을 끈 다음 사망 애니메이션 재생
            PlayStateAnimation(newState);

            // 종료 이벤트가 누락되어도 삭제
            Destroy(gameObject, Mathf.Max(0.1f, deathCleanupDelay));
            return;
        }

        PlayStateAnimation(newState);

        if (newState == MonsterState.GetDamage)
        {
            Invoke(nameof(RecoverFromDamage), Mathf.Max(0.01f, damageRecoveryDelay));
        }

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

    private void PlayStateAnimation(MonsterState state)
    {
        if (animator == null) return;

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                "Animator의 Controller를 연결해주세요.", this);
            return;
        }

        string stateName;

        switch (state)
        {
            case MonsterState.Idle:
                stateName = idleStateName;
                break;

            case MonsterState.Walk:
                stateName = walkStateName;
                break;

            case MonsterState.Attack:
                stateName = attackStateName;
                break;

            case MonsterState.GetDamage:
                stateName = damagedStateName;
                break;

            case MonsterState.Dead:
                stateName = deathStateName;
                break;

            default:
                return;
        }

        int stateHash = Animator.StringToHash("Base Layer." + stateName);

        if (!animator.HasState(0, stateHash))
        {
            // 기존 공통 이름을 사용하는 Animator도 지원한다.
            string fallbackName = state == MonsterState.Dead ? "Death" : state.ToString();
            int fallbackHash = Animator.StringToHash("Base Layer." + fallbackName);
            if (animator.HasState(0, fallbackHash))
            {
                animator.Play(fallbackHash, 0, 0f);
                return;
            }
            Debug.LogWarning(
                $"Animator에 'Base Layer.{stateName}' 상태가 없습니다. " +
                "상태 이름과 대소문자를 확인해주세요.",
                this);
            return;
        }

        animator.Play(stateHash, 0, 0f);
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

    private void RecoverFromDamage()
    {
        if (isActiveAndEnabled && !IsDead && CurrentState == MonsterState.GetDamage)
            OnDamageAnimationEnd();
    }

    private void ClearAttack()
    {
        CancelInvoke(nameof(SpawnAttackBox));
        CancelInvoke(nameof(FinishTimedAttack));

        if (activeAttackBox == null) return;

        activeAttackBox.SetActive(false);
        Destroy(activeAttackBox);
        activeAttackBox = null;
    }

    protected override void OnDisable()
    {
        ClearAttack();
        CancelInvoke(nameof(RecoverFromDamage));
        base.OnDisable();
    }

    protected override void GetDamage(int damageValue)
    {
        int previousHP = MonsterCurrentHP;

        MonsterCurrentHP =
            Mathf.Max(0, MonsterCurrentHP - Mathf.Max(0, damageValue));

        Debug.Log(
            $"[{name}] HP: {previousHP} → {MonsterCurrentHP}, " +
            $"받은 데미지: {damageValue}",
            this);
    }
}
