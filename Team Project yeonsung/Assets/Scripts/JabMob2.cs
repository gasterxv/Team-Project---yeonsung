using UnityEngine;

public class JabMob2 : EnemyBase
{
    [Header("몸박 점프 공격")]
    [SerializeField] private Collider2D attackBodyCollider;
    [SerializeField, Min(1)] private int contactDamage = 10;
    [SerializeField, Min(0f)] private float attackHorizontalSpeed = 10f;
    [SerializeField, Min(0.1f)] private float attackJumpSpeed = 3.5f;
    [SerializeField] private LayerMask attackGroundLayers;
    [SerializeField, Min(0.2f)] private float maxAttackAirTime = 3f;
    [Tooltip("좌우 몸통 가장자리 사이의 공격 시작 거리. 추적 정지 간격 + 0.05 이상으로 자동 보정합니다.")]
    [SerializeField, Min(0f)] private float attackStartGap = 0.4f;
    private Collider2D attackAimCollider;

    [Header("공격 쿨타임 (초)")]
    [Tooltip("공격 종료 후 다음 공격까지의 시간입니다. 잡몹2는 부모의 Monster Attack Interval 대신 이 값을 사용합니다. 변경값은 다음 공격 종료부터 적용됩니다.")]
    [SerializeField, Min(0f)] private float attackCooldown = 2f;

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

    private Rigidbody2D jumpBody;
    private PlayerController contactPlayer;
    private Collider2D[] playerBodies = new Collider2D[0];
    private Collider2D[] enemyBodies;
    private readonly System.Collections.Generic.List<CollisionPair> ignoredPairs =
        new System.Collections.Generic.List<CollisionPair>();

    private struct CollisionPair
    {
        public Collider2D enemy;
        public Collider2D player;
    }
    private bool jumpAttackActive;
    private bool launched;
    private float attackElapsed;
    private int attackDirection;
    private readonly System.Collections.Generic.HashSet<PlayerController> damagedPlayers =
        new System.Collections.Generic.HashSet<PlayerController>();
    private readonly ContactPoint2D[] bodyContacts = new ContactPoint2D[32];

    protected override void Awake()
    {
        // EnemyBase의 Rigidbody2D, 초기 HP 등 초기화 유지
        base.Awake();
        SyncAttackCooldown();
        jumpBody = GetComponent<Rigidbody2D>();
        enemyBodies = GetComponentsInChildren<Collider2D>(true);
        if (attackBodyCollider == null)
        {
            foreach (Collider2D candidate in GetComponentsInChildren<Collider2D>(true))
            {
                if (!candidate.isTrigger && candidate.attachedRigidbody == jumpBody)
                {
                    attackBodyCollider = candidate;
                    break;
                }
            }
        }

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

        if (jumpBody == null || !jumpBody.simulated ||
            jumpBody.bodyType != RigidbodyType2D.Dynamic ||
            attackBodyCollider == null || !attackBodyCollider.enabled ||
            attackBodyCollider.isTrigger || attackBodyCollider.attachedRigidbody != jumpBody)
        {
            Debug.LogError("몸박 공격에는 Dynamic Rigidbody2D와 같은 물리에 연결된 활성 일반 몸통 Collider가 필요합니다.", this);
            Invoke(nameof(FinishTimedAttack), 0.01f);
            return;
        }
        if (attackGroundLayers.value == 0)
            Debug.LogWarning("Attack Ground Layers에 실제 바닥 레이어를 지정하세요.", this);
        // 그림의 기본 방향이나 센서 오프셋 대신 현재 공격 대상의 위치를 사용합니다.
        float differenceX = attackAimCollider != null
            ? attackAimCollider.bounds.center.x - attackBodyCollider.bounds.center.x : 0f;
        attackDirection = Mathf.Abs(differenceX) > 0.001f
            ? (differenceX > 0f ? 1 : -1) : FacingDirection;
        SetFacing(attackDirection);
        attackElapsed = 0f;
        jumpAttackActive = true;
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

    protected override bool IsWithinAttackRange(Collider2D target, bool sensorDetected)
    {
        attackAimCollider = null;
        if (target == null || !target.isActiveAndEnabled || attackBodyCollider == null)
            return false;

        Bounds ownBounds = attackBodyCollider.bounds;
        Bounds targetBounds = target.bounds;
        if (ownBounds.max.y < targetBounds.min.y || targetBounds.max.y < ownBounds.min.y)
            return false;

        float gap = Mathf.Abs(targetBounds.center.x - ownBounds.center.x)
            - ownBounds.extents.x - targetBounds.extents.x;
        float startGap = Mathf.Max(attackStartGap, ChaseStopGap + 0.05f);
        if (gap > startGap) return false;

        attackAimCollider = target;
        return true;
    }

    protected override void UpdateState()
    {
        SyncAttackCooldown();
        if (jumpAttackActive && CurrentState == MonsterState.Attack)
        {
            if (IsDead) ChangeState(MonsterState.Dead);
            // 부모의 공격 잠금 로직이 수평 속도를 지우지 않게 한다.
            return;
        }
        base.UpdateState();
    }

    protected override void FixedUpdate()
    {
        if (IsDead) return;
        RefreshBodyCollisionRules();
        if (!jumpAttackActive || CurrentState != MonsterState.Attack)
        {
            base.FixedUpdate();
            return;
        }
        if (!launched)
        {
            launched = true;
            jumpBody.linearVelocity = new Vector2(
                attackDirection * attackHorizontalSpeed, attackJumpSpeed);
            CheckPlayerOverlap();
            CheckBodyContacts(false);
            return;
        }
        attackElapsed += Time.fixedDeltaTime;
        CheckPlayerOverlap();
        bool landed = CheckBodyContacts(attackElapsed >= 0.08f);
        if (!jumpAttackActive || IsDead) return;
        if (landed || attackElapsed >= maxAttackAirTime)
        {
            FinishTimedAttack();
            return;
        }
        jumpBody.linearVelocity = new Vector2(
            attackDirection * attackHorizontalSpeed, jumpBody.linearVelocity.y);
    }

    private bool CheckBodyContacts(bool allowLanding)
    {
        int count = attackBodyCollider.GetContacts(bodyContacts);
        bool landed = false;
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = bodyContacts[i];
            Collider2D other = contact.collider;
            if (other == attackBodyCollider) other = contact.otherCollider;
            if (other == null || other.attachedRigidbody == jumpBody) continue;
            TryBodyDamage(other);
            if (allowLanding && jumpBody.linearVelocity.y <= 0.1f &&
                contact.normal.y > 0.5f &&
                (attackGroundLayers.value & (1 << other.gameObject.layer)) != 0)
                landed = true;
        }
        return landed;
    }

    private void RefreshBodyCollisionRules()
    {
        if (contactPlayer == null || !contactPlayer.gameObject.activeInHierarchy)
        {
            RestoreBodyCollisions();
            contactPlayer = FindFirstObjectByType<PlayerController>();
            playerBodies = contactPlayer != null
                ? contactPlayer.GetComponentsInChildren<Collider2D>(true)
                : new Collider2D[0];
        }

        // 보스와 동일하게 몸통 충돌만 무시합니다. 센서와 공격 트리거는 유지합니다.
        foreach (Collider2D enemy in enemyBodies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.isTrigger ||
                enemy.attachedRigidbody != jumpBody) continue;
            foreach (Collider2D playerBody in playerBodies)
            {
                if (playerBody == null || !playerBody.isActiveAndEnabled || playerBody.isTrigger)
                    continue;
                if (Physics2D.GetIgnoreCollision(enemy, playerBody)) continue;

                bool recorded = false;
                foreach (CollisionPair pair in ignoredPairs)
                    if (pair.enemy == enemy && pair.player == playerBody) { recorded = true; break; }
                if (!recorded)
                    ignoredPairs.Add(new CollisionPair { enemy = enemy, player = playerBody });
                Physics2D.IgnoreCollision(enemy, playerBody, true);
            }
        }
    }

    private void RestoreBodyCollisions()
    {
        foreach (CollisionPair pair in ignoredPairs)
            if (pair.enemy != null && pair.player != null)
                Physics2D.IgnoreCollision(pair.enemy, pair.player, false);
        ignoredPairs.Clear();
    }

    private void CheckPlayerOverlap()
    {
        // IgnoreCollision을 사용하면 충돌 이벤트가 오지 않으므로 직접 거리 검사합니다.
        foreach (Collider2D playerBody in playerBodies)
            TryBodyDamage(playerBody);
    }

    private void TryBodyDamage(Collider2D other)
    {
        if (!jumpAttackActive || !launched || IsDead ||
            CurrentState != MonsterState.Attack || other == null ||
            attackBodyCollider == null || !other.isActiveAndEnabled || other.isTrigger) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead || damagedPlayers.Contains(player)) return;
        // 넓은 인식 센서 접촉만으로는 피해를 주지 않는다.
        ColliderDistance2D distance = attackBodyCollider.Distance(other);
        if (!distance.isValid || distance.distance > 0f) return;
        damagedPlayers.Add(player);
        // 보스와 같은 피격 위치 기반 넉백을 사용합니다.
        player.TakeDamageFrom(contactDamage, attackBodyCollider.bounds.center);
    }

    private void FinishTimedAttack()
    {
        if (CurrentState != MonsterState.Attack || IsDead) return;
        SyncAttackCooldown();
        jumpAttackActive = false;
        launched = false;
        if (jumpBody != null)
            jumpBody.linearVelocity = new Vector2(0f, jumpBody.linearVelocity.y);
        EndAttack();
    }

    private void SyncAttackCooldown()
    {
        // 남은 쿨타임은 초기화하지 않고 다음 공격 종료 시 사용할 간격만 갱신한다.
        MonsterAttackInterval = Mathf.Max(0f, attackCooldown);
    }
    private void RecoverFromDamage()
    {
        if (isActiveAndEnabled && !IsDead && CurrentState == MonsterState.GetDamage)
            OnDamageAnimationEnd();
    }

    private void ClearAttack()
    {
        CancelInvoke(nameof(FinishTimedAttack));
        jumpAttackActive = false;
        launched = false;
        damagedPlayers.Clear();
    }
    protected override void OnDisable()
    {
        RestoreBodyCollisions();
        contactPlayer = null;
        attackAimCollider = null;
        playerBodies = new Collider2D[0];
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

