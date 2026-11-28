using UnityEngine;

public class JabMob1 : EnemyBase
{
    [Header("공격 히트박스")]
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackSpawnPoint;

    [Header("공격 히트박스 시간")]
    [SerializeField, Min(0f)] private float attackBoxSpawnDelay = 0.5f;
    [SerializeField, Min(0.01f)] private float attackBoxLifetime = 0.2f;
    [SerializeField, Min(0.01f)] private float attackDuration = 3f;

    [Header("Animator의 상태 이름 - 대소문자까지 동일해야 합니다")]
    [SerializeField] private string idleStateName = "JabMob1Idle";
    [SerializeField] private string walkStateName = "Jabmob1Walk";
    [SerializeField] private string attackStateName = "JabMob1Attack";
    [SerializeField] private string damagedStateName = "JabMob1Damaged";
    [SerializeField] private string deathStateName = "JabMob1Die";

    [Header("사망 후 자동 삭제")]
    [Tooltip("사망 애니메이션 종료 이벤트가 없어도 이 시간 후 삭제합니다. 게임 시간 기준입니다.")]
    [SerializeField, Min(0.1f)] private float deathCleanupDelay = 2f;

    private GameObject activeAttackBox;

    [Header("공격 후 돌아오는 모션")]
    [Tooltip("공격이 끝나기 전 이 시간(초) 동안 공격 그림을 거꾸로 재생해 처음 자세로 돌아옴. 0이면 끔")]
    [SerializeField, Min(0f)] private float attackReturnDuration = 0.35f;
    private bool attackReturnActive;
    private float attackStartTime;

    // [버그 수정] 공격 그림이 발톱을 내리친 장면에서 끝나 그대로 멈춰 있다가 바로 Idle로 바뀌던 것
    // → 공격이 끝나기 직전에 공격 그림을 거꾸로 재생해서 돌아오는 모션을 보여 줌 (새 그림 없이)
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (!attackReturnActive || animator == null || attackReturnDuration <= 0f) return;
        if (CurrentState != MonsterState.Attack) { attackReturnActive = false; animator.speed = 1f; return; }
        float duration = Mathf.Max(attackDuration, attackBoxSpawnDelay + attackBoxLifetime);
        float returnStart = duration - attackReturnDuration;
        float elapsed = Time.time - attackStartTime;
        if (elapsed < returnStart) return;
        int stateHash = Animator.StringToHash("Base Layer." + attackStateName);
        if (!animator.HasState(0, stateHash)) return;
        // 공격 그림을 다 보여 준 뒤에만 (공격 시간이 너무 짧으면 거꾸로 재생 안 함)
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.fullPathHash == stateHash && animator.speed > 0f && info.normalizedTime < 1f) return;
        float t = 1f - Mathf.Clamp01((elapsed - returnStart) / attackReturnDuration);
        animator.speed = 0f;
        animator.Play(stateHash, 0, t);
    }

    // [버그 수정] 몬스터 몸을 밟고 설 수 있던 것(C 내려찍기 포함) → 간염(JabMob2)처럼 플레이어 몸과는 부딪히지 않게
    private PlayerController bodyIgnoredPlayer;

    protected override void FixedUpdate()
    {
        IgnorePlayerBodyCollisions();
        base.FixedUpdate();
    }

    // 몸을 통과하게 되면서, 플레이어가 몸 안에 겹쳐 서 있으면 앞쪽 공격 센서에 안 잡혀 공격하지 않던 것
    // → 몸이 겹쳐 있어도 공격 범위로 봄
    protected override bool IsWithinAttackRange(Collider2D target, bool sensorDetected)
    {
        return sensorDetected || PlayerOverlapsBody();
    }

    private bool PlayerOverlapsBody()
    {
        if (bodyIgnoredPlayer == null) return false;
        Rigidbody2D ownBody = GetComponent<Rigidbody2D>();
        foreach (Collider2D own in GetComponentsInChildren<Collider2D>())
        {
            if (own.isTrigger || own.attachedRigidbody != ownBody) continue;
            foreach (Collider2D pc in bodyIgnoredPlayer.GetComponentsInChildren<Collider2D>())
            {
                if (pc.isTrigger) continue;
                ColliderDistance2D d = own.Distance(pc);
                if (d.isValid && d.distance <= 0.05f) return true;
            }
        }
        return false;
    }

    private void IgnorePlayerBodyCollisions()
    {
        if (bodyIgnoredPlayer != null && bodyIgnoredPlayer.gameObject.activeInHierarchy) return;
        bodyIgnoredPlayer = FindFirstObjectByType<PlayerController>();
        if (bodyIgnoredPlayer == null) return;
        Rigidbody2D ownBody = GetComponent<Rigidbody2D>();
        Collider2D[] playerCols = bodyIgnoredPlayer.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D own in GetComponentsInChildren<Collider2D>(true))
        {
            if (own.isTrigger || own.attachedRigidbody != ownBody) continue;
            foreach (Collider2D pc in playerCols)
                if (!pc.isTrigger) Physics2D.IgnoreCollision(own, pc, true);
        }
    }

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
        // 공격 후 돌아오는 모션 때문에 멈춰 둔 Animator 속도를 되돌림
        if (animator != null) animator.speed = 1f;
        attackReturnActive = newState == MonsterState.Attack;
        attackStartTime = Time.time;

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

        // 플레이어가 몸과 겹쳐 있으면(몸을 통과하므로 가능) 앞쪽 대신 겹친 플레이어 위치에 공격 박스를 만듦
        if (PlayerOverlapsBody())
        {
            float playerX = bodyIgnoredPlayer.GetComponent<Collider2D>() != null
                ? bodyIgnoredPlayer.GetComponent<Collider2D>().bounds.center.x
                : bodyIgnoredPlayer.transform.position.x;
            localPosition.x = transform.InverseTransformPoint(
                new Vector3(playerX, transform.position.y, 0f)).x;
        }

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

        if (activeAttackBox == null) return;

        activeAttackBox.SetActive(false);
        Destroy(activeAttackBox);
        activeAttackBox = null;
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