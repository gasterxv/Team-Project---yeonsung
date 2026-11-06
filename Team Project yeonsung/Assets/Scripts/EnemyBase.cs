using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    public enum MonsterState { Idle, Walk, Attack, Dead, GetDamage }
    private enum BehaviorMode { Patrol, Chase, WaitBeforeReturn, ReturnHome }

    [Header("컴포넌트")]
    [SerializeField] protected Animator animator;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D monsterBodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRight = true;

    [Header("센서")]
    [SerializeField] private EnemySensor recognitionSensor;
    [SerializeField] private EnemySensor attackRangeSensor;

    [Header("능력치: 플레이어 속도에 맞춰 인스펙터에서 조정")]
    [SerializeField] protected int MonsterCurrentHP = 100;
    [SerializeField, Min(0f)] protected float MonsterWalkSpeed = 6f;
    [SerializeField, Min(0f)] private float chaseSpeed = 8f;
    [SerializeField, Min(0f)] private float returnSpeed = 6f;
    [SerializeField, Min(0f)] protected float MonsterAttackInterval = 2f;
    [SerializeField] protected float MonsterAttackCooldown;

    [Header("최초 배치 위치 기준 순찰 및 복귀 (월드 단위)")]
    [SerializeField, Min(0.1f)] private float patrolRadius = 3f;
    [SerializeField, Min(0f)] private float patrolIdleTime = 2f;
    [SerializeField, Min(0.1f)] private float maxChaseDistance = 10f;
    [SerializeField, Min(0f)] private float waitBeforeReturn = 1f;
    [SerializeField, Min(0.01f)] private float homeTolerance = 0.15f;
    [SerializeField, Min(0.01f)] private float homeVerticalTolerance = 0.5f;
    [SerializeField, Min(0.1f)] private float returnTimeout = 5f;

    [Header("낭떠러지 감지 (사용 시 Ground Layers 필수)")]
    [SerializeField] private bool avoidCliffs = true;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField, Min(0.01f)] private float groundProbeAhead = 0.2f;
    [SerializeField, Min(0.01f)] private float groundProbeDepth = 0.6f;

    [Header("거리 디스폰: 재등장은 외부 스폰 관리자가 담당")]
    [SerializeField] private bool enableDistanceDespawn;
    [SerializeField] private Transform player;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private bool useCameraWidth = true;
    [Tooltip("1이면 플레이어 중심에서 좌우 각각 화면 전체 폭만큼 유지")]
    [SerializeField, Min(0.1f)] private float screenWidthsEachSide = 1f;
    [SerializeField, Min(0.1f)] private float fallbackHorizontalDistance = 25f;
    [Tooltip("0이면 세로 거리로 디스폰하지 않음")]
    [SerializeField, Min(0f)] private float verticalDespawnDistance;

    protected MonsterState currentState = MonsterState.Idle;
    public MonsterState CurrentState => currentState;
    public float MaxChaseDistance => maxChaseDistance;
    public bool IsDead => MonsterCurrentHP <= 0 || currentState == MonsterState.Dead;
    public event System.Action<EnemyBase> Died;
    protected int FacingDirection => facingDirection;

    private bool spawnPointManaged;

    // 스폰 관리자를 사용하는 몬스터는 카메라 기준으로 따로 디스폰하지 않는다.
    public void UseSpawnPointManagement()
    {
        spawnPointManaged = true;
    }

    private BehaviorMode behavior;
    private Vector2 homePosition;
    private Collider2D targetCollider;
    private bool isAttackLocked;
    private int facingDirection = 1;
    private int patrolDirection = 1;
    private float moveDirection;
    private float behaviorTimer;
    private float patrolIdleTimer;
    private int initialHP;

    protected virtual void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (monsterBodyCollider == null) monsterBodyCollider = GetComponent<Collider2D>();
        initialHP = MonsterCurrentHP;
        if (gameplayCamera == null) gameplayCamera = Camera.main;
    }

    protected virtual void OnEnable()
    {
        // 씬 배치용. 풀링 시 위치 설정 후 InitializeSpawn()을 다시 호출한다.
        InitializeSpawn(transform.position);
    }

    public void InitializeSpawn(Vector2 spawnPosition)
    {
        homePosition = spawnPosition;
        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        MonsterCurrentHP = initialHP;
        MonsterAttackCooldown = 0f;
        targetCollider = null;
        isAttackLocked = false;
        moveDirection = 0f;
        behavior = BehaviorMode.Patrol;
        behaviorTimer = 0f;
        patrolIdleTimer = patrolIdleTime;
        patrolDirection = 1;
        SetFacing(1);
        currentState = initialHP > 0 ? MonsterState.Idle : MonsterState.Dead;
        OnStateChanged(currentState);
    }

    protected virtual void OnDisable()
    {
        targetCollider = null;
        isAttackLocked = false;
        moveDirection = 0f;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    protected virtual void Update()
    {
        if (ShouldDespawn())
        {
            gameObject.SetActive(false);
            return;
        }

        MonsterAttackCooldown = Mathf.Max(0f, MonsterAttackCooldown - Time.deltaTime);
        UpdateState();
        switch (currentState)
        {
            case MonsterState.Idle: Idle(); break;
            case MonsterState.Walk: Walk(); break;
            case MonsterState.Attack: Attack(); break;
            case MonsterState.Dead: Death(); break;
        }
    }

    protected virtual void FixedUpdate()
    {
        float speed = behavior == BehaviorMode.Chase ? chaseSpeed
            : behavior == BehaviorMode.ReturnHome ? returnSpeed : MonsterWalkSpeed;
        float horizontal = currentState == MonsterState.Walk ? moveDirection * speed : 0f;

        // 목표를 지나쳐 좌우로 떨리는 현상을 방지한다.
        if (currentState == MonsterState.Walk && behavior != BehaviorMode.Chase)
        {
            float goalX = behavior == BehaviorMode.ReturnHome ? homePosition.x
                : homePosition.x + patrolDirection * patrolRadius;
            horizontal = Mathf.Clamp((goalX - body.position.x) / Time.fixedDeltaTime, -speed, speed);
        }

        if (horizontal != 0f && !HasGroundAhead(Mathf.Sign(horizontal)))
        {
            horizontal = 0f;
            if (behavior == BehaviorMode.Chase) BeginReturn();
            else if (behavior == BehaviorMode.Patrol)
            {
                patrolDirection *= -1;
                patrolIdleTimer = patrolIdleTime;
                ChangeState(MonsterState.Idle);
            }
            // 복귀 중 길이 없으면 timeout 후 안전한 최초 위치로 재배치한다.
        }
        body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
    }

    protected virtual void UpdateState()
    {
        if (currentState == MonsterState.Dead) return;
        if (MonsterCurrentHP <= 0) { ChangeState(MonsterState.Dead); return; }

        // 공격 중이어도 귀환 거리 제한은 우선 적용한다.
        if (behavior == BehaviorMode.Chase &&
            Vector2.Distance(body.position, homePosition) >= maxChaseDistance)
        {
            BeginReturn();
            return;
        }

        // 플레이어 감지 여부와 관계없이 피격 애니메이션을 유지한다.
        if (currentState == MonsterState.GetDamage)
        {
            StopHorizontalMovement();
            return;
        }

        // 이 두 단계에서는 센서에 플레이어가 있어도 재추적하지 않는다.
        if (behavior == BehaviorMode.WaitBeforeReturn || behavior == BehaviorMode.ReturnHome)
        {
            UpdateReturn();
            return;
        }

        Collider2D attackTarget = ReadTarget(attackRangeSensor);
        targetCollider = ReadTarget(recognitionSensor);
        if (targetCollider == null) targetCollider = attackTarget;

        if (targetCollider == null)
        {
            if (behavior == BehaviorMode.Chase) BeginReturn();
            else UpdatePatrol();
            return;
        }

        // 순찰 위치가 제한 거리 밖인 설정에서도 추적 진입을 차단한다.
        if (Vector2.Distance(body.position, homePosition) >= maxChaseDistance)
        {
            BeginReturn();
            return;
        }

        behavior = BehaviorMode.Chase;
        if (isAttackLocked) { StopHorizontalMovement(); return; }
        float centerX = monsterBodyCollider != null ? monsterBodyCollider.bounds.center.x : body.position.x;
        float differenceX = targetCollider.bounds.center.x - centerX;
        if (Mathf.Abs(differenceX) > 0.01f) SetFacing(differenceX > 0f ? 1 : -1);

        if (attackTarget != null)
        {
            StopHorizontalMovement();
            ChangeState(MonsterAttackCooldown <= 0f ? MonsterState.Attack : MonsterState.Idle);
        }
        else
        {
            moveDirection = Mathf.Abs(differenceX) <= 0.05f ? 0f : Mathf.Sign(differenceX);
            ChangeState(MonsterState.Walk);
        }
    }

    private static Collider2D ReadTarget(EnemySensor sensor)
    {
        Collider2D candidate = sensor != null ? sensor.PlayerCollider : null;
        return candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy
            ? candidate : null;
    }

    private void BeginReturn()
    {
        targetCollider = null;
        behavior = BehaviorMode.WaitBeforeReturn;
        behaviorTimer = waitBeforeReturn;
        isAttackLocked = false;
        StopHorizontalMovement();
        ChangeState(MonsterState.Idle);
    }

    private void UpdateReturn()
    {
        if (behavior == BehaviorMode.WaitBeforeReturn)
        {
            StopHorizontalMovement();
            behaviorTimer -= Time.deltaTime;
            if (behaviorTimer <= 0f)
            {
                behavior = BehaviorMode.ReturnHome;
                behaviorTimer = returnTimeout;
            }
            return;
        }

        behaviorTimer -= Time.deltaTime;
        Vector2 offset = homePosition - body.position;
        bool arrived = Mathf.Abs(offset.x) <= homeTolerance &&
            Mathf.Abs(offset.y) <= homeVerticalTolerance;
        if (arrived || behaviorTimer <= 0f)
        {
            if (!arrived)
            {
                body.position = homePosition;
                body.linearVelocity = Vector2.zero;
            }
            behavior = BehaviorMode.Patrol;
            patrolDirection = 1;
            patrolIdleTimer = patrolIdleTime;
            StopHorizontalMovement();
            ChangeState(MonsterState.Idle);
            return;
        }

        moveDirection = Mathf.Abs(offset.x) <= homeTolerance ? 0f : Mathf.Sign(offset.x);
        if (moveDirection != 0f) SetFacing((int)moveDirection);
        ChangeState(moveDirection == 0f ? MonsterState.Idle : MonsterState.Walk);
    }

    private void UpdatePatrol()
    {
        if (patrolIdleTimer > 0f)
        {
            patrolIdleTimer -= Time.deltaTime;
            StopHorizontalMovement();
            ChangeState(MonsterState.Idle);
            return;
        }

        float goalX = homePosition.x + patrolDirection * patrolRadius;
        float offset = goalX - body.position.x;
        if (Mathf.Abs(offset) <= homeTolerance)
        {
            patrolDirection *= -1;
            patrolIdleTimer = patrolIdleTime;
            StopHorizontalMovement();
            ChangeState(MonsterState.Idle);
            return;
        }
        moveDirection = Mathf.Sign(offset);
        SetFacing((int)moveDirection);
        ChangeState(MonsterState.Walk);
    }

    private bool HasGroundAhead(float direction)
    {
        if (!avoidCliffs || groundLayers.value == 0 || monsterBodyCollider == null) return true;
        Bounds bounds = monsterBodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x + direction * (bounds.extents.x + groundProbeAhead),
            bounds.min.y + 0.1f);
        return Physics2D.Raycast(origin, Vector2.down, groundProbeDepth + 0.1f, groundLayers).collider != null;
    }

    private bool ShouldDespawn()
    {
        if (spawnPointManaged || !enableDistanceDespawn || player == null) return false;
        float range = fallbackHorizontalDistance;
        if (useCameraWidth && gameplayCamera != null && gameplayCamera.orthographic)
            range = 2f * gameplayCamera.orthographicSize * gameplayCamera.aspect * screenWidthsEachSide;
        Vector2 offset = (Vector2)player.position - body.position;
        return Mathf.Abs(offset.x) > range ||
            (verticalDespawnDistance > 0f && Mathf.Abs(offset.y) > verticalDespawnDistance);
    }

    private void SetFacing(int direction)
    {
        facingDirection = direction;
        if (spriteRenderer != null)
            spriteRenderer.flipX = spriteFacesRight ? direction < 0 : direction > 0;
    }

    private void StopHorizontalMovement()
    {
        moveDirection = 0f;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    public void ChangeState(MonsterState nextState)
    {
        if (currentState == MonsterState.Dead) return;
        if (MonsterCurrentHP <= 0) nextState = MonsterState.Dead;
        if (isAttackLocked && nextState != MonsterState.Dead) return;
        if (currentState == nextState) return;
        if (nextState == MonsterState.Attack && MonsterAttackCooldown > 0f) return;
        if (currentState == MonsterState.Attack) MonsterAttackCooldown = MonsterAttackInterval;
        currentState = nextState;
        isAttackLocked = nextState == MonsterState.Attack;
        if (nextState != MonsterState.Walk) StopHorizontalMovement();
        OnStateChanged(nextState);
        if (nextState == MonsterState.Dead) Died?.Invoke(this);
    }

    protected virtual void OnStateChanged(MonsterState newState)
    {
        if (animator == null) return;
        string animationName = newState == MonsterState.Dead ? "Death" : newState.ToString();
        animator.Play("Base Layer." + animationName, 0, 0f);
    }

    public void NotifyPlayerAttack(int damageValue)
    {
        if (currentState == MonsterState.Dead) return;
        GetDamage(Mathf.Max(0, damageValue));
        ChangeState(MonsterCurrentHP <= 0 ? MonsterState.Dead : MonsterState.GetDamage);
    }

    protected void EndAttack()
    {
        if (currentState != MonsterState.Attack) return;
        isAttackLocked = false;
        ChangeState(MonsterState.Idle);
        UpdateState();
    }

    public void OnDamageAnimationEnd()
    {
        if (currentState != MonsterState.GetDamage) return;
        ChangeState(MonsterState.Idle);
        UpdateState();
    }

    public void OnDeathAnimationEnd()
    {
        if (currentState == MonsterState.Dead) gameObject.SetActive(false);
    }

    protected virtual void GetDamage(int damageValue) { }
    protected virtual void Death() { }
    protected virtual void Attack() { }
    protected virtual void Walk() { }
    protected virtual void Idle() { }
}
