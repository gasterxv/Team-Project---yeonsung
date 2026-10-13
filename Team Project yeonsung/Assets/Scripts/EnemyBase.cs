using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    public enum MonsterState
    {
        Idle,
        Walk,
        Attack,
        Dead,
        GetDamage
    }

    [Header("컴포넌트")]
    [SerializeField] protected Animator animator;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D monsterBodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    // 원본 스프라이트가 오른쪽을 보고 있는지
    [SerializeField] private bool spriteFacesRight = true;
    protected int FacingDirection => facingDirection;

    [Header("센서")]
    [SerializeField] private EnemySensor recognitionSensor;
    [SerializeField] private EnemySensor attackRangeSensor;

    [Header("능력치")]
    [SerializeField] protected int MonsterCurrentHP = 100;
    [SerializeField] protected float MonsterWalkSpeed = 2f;

    // 실제 공격 간격 설정은 별도 공격 코드에서 처리
    [SerializeField] protected float MonsterAttackCooldown = 0f;

    [Header("순찰")]
    [SerializeField] private float patrolWalkTime = 5f;
    [SerializeField] private float patrolIdleTime = 3f;

    protected MonsterState currentState = MonsterState.Idle;

    public MonsterState CurrentState => currentState;

    private Collider2D targetCollider;

    private bool isAttackLocked;
    private bool isPatrolling;

    // 0: 오른쪽 이동, 1: 대기, 2: 왼쪽 이동, 3: 대기
    private int patrolStep;
    private float patrolTimer;

    // 오른쪽 1, 왼쪽 -1
    private int facingDirection = 1;

    // 수평 이동 입력: -1, 0, 1
    private float moveDirection;

    protected virtual void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    protected virtual void Start()
    {
        SetFacing(1);

        if (MonsterCurrentHP <= 0)
        {
            ChangeState(MonsterState.Dead);
        }
        else
        {
            OnStateChanged(currentState);
        }
    }

    protected virtual void Update()
    {
        // 남은 쿨타임 감소
        MonsterAttackCooldown = Mathf.Max(
            0f,
            MonsterAttackCooldown - Time.deltaTime
        );

        UpdateState();

        switch (currentState)
        {
            case MonsterState.Idle:
                Idle();
                break;

            case MonsterState.Walk:
                Walk();
                break;

            case MonsterState.Attack:
                Attack();
                break;

            case MonsterState.Dead:
                Death();
                break;

            case MonsterState.GetDamage:
                // 피격 종료 이벤트를 기다림
                break;
        }
    }

    protected virtual void FixedUpdate()
    {
        // 걷기 상태에서만 수평 이동
        float horizontalSpeed = currentState == MonsterState.Walk
            ? moveDirection * MonsterWalkSpeed
            : 0f;

        // 낙하·점프 등의 수직 속도는 유지
        body.linearVelocity = new Vector2(
            horizontalSpeed,
            body.linearVelocity.y
        );
    }

    protected virtual void UpdateState()
    {
        if (currentState == MonsterState.Dead)
            return;

        if (MonsterCurrentHP <= 0)
        {
            ChangeState(MonsterState.Dead);
            return;
        }

        Collider2D attackTarget = attackRangeSensor.PlayerCollider;
        Collider2D recognitionTarget = recognitionSensor.PlayerCollider;

        // 인식 범위의 플레이어를 저장.
        // 공격 범위에서만 감지되는 경우에도 대응.
        targetCollider = recognitionTarget != null
            ? recognitionTarget
            : attackTarget;

        if (targetCollider != null)
        {
            // 플레이어를 발견하면 순찰 중단
            isPatrolling = false;
        }

        // 공격 중에는 이동과 방향 전환을 하지 않음
        if (isAttackLocked)
        {
            StopHorizontalMovement();
            return;
        }

        if (targetCollider != null)
        {
            UpdateFacingToTarget();

            if (attackTarget != null)
            {
                StopHorizontalMovement();

                ChangeState(
                    MonsterAttackCooldown <= 0f
                        ? MonsterState.Attack
                        : MonsterState.Idle
                );
            }
            else
            {
                float differenceX =
                    targetCollider.bounds.center.x -
                    monsterBodyCollider.bounds.center.x;

                moveDirection = differenceX > 0f
                    ? 1f
                    : differenceX < 0f ? -1f : 0f;

                ChangeState(MonsterState.Walk);
            }

            return;
        }

        // 플레이어가 없을 때 피격 동작을 순찰로 덮어쓰지 않음
        if (currentState == MonsterState.GetDamage)
        {
            StopHorizontalMovement();
            return;
        }

        UpdatePatrol();
    }

    private void UpdateFacingToTarget()
    {
        float differenceX =
            targetCollider.bounds.center.x -
            monsterBodyCollider.bounds.center.x;

        if (differenceX > 0f)
        {
            SetFacing(1);
        }
        else if (differenceX < 0f)
        {
            SetFacing(-1);
        }

        // 같은 X좌표이면 기존 방향 유지
    }

    private void SetFacing(int direction)
    {
        facingDirection = direction;

        bool facingLeft = facingDirection < 0;

        spriteRenderer.flipX = spriteFacesRight
            ? facingLeft
            : !facingLeft;
    }

    private void UpdatePatrol()
    {
        if (!isPatrolling)
        {
            // 플레이어를 놓친 뒤에는 순찰을 처음부터 시작
            isPatrolling = true;
            patrolStep = 0;
            patrolTimer = patrolWalkTime;
        }
        else
        {
            patrolTimer -= Time.deltaTime;

            if (patrolTimer <= 0f)
            {
                patrolStep = (patrolStep + 1) % 4;

                patrolTimer = patrolStep == 0 || patrolStep == 2
                    ? patrolWalkTime
                    : patrolIdleTime;
            }
        }

        switch (patrolStep)
        {
            case 0:
                moveDirection = 1f;
                SetFacing(1);
                ChangeState(MonsterState.Walk);
                break;

            case 1:
            case 3:
                StopHorizontalMovement();
                ChangeState(MonsterState.Idle);
                break;

            case 2:
                moveDirection = -1f;
                SetFacing(-1);
                ChangeState(MonsterState.Walk);
                break;
        }
    }

    private void StopHorizontalMovement()
    {
        moveDirection = 0f;

        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );
    }

    public void ChangeState(MonsterState nextState)
    {
        if (currentState == MonsterState.Dead)
            return;

        if (MonsterCurrentHP <= 0)
            nextState = MonsterState.Dead;

        if (isAttackLocked && nextState != MonsterState.Dead)
            return;

        if (currentState == nextState)
            return;

        currentState = nextState;
        isAttackLocked = nextState == MonsterState.Attack;

        if (nextState != MonsterState.Walk)
        {
            StopHorizontalMovement();
        }

        if (nextState == MonsterState.Dead)
        {
            isPatrolling = false;
        }

        OnStateChanged(nextState);
    }

    protected virtual void OnStateChanged(MonsterState newState)
    {
        switch (newState)
        {
            case MonsterState.Idle:
                animator.Play("Base Layer.Idle", 0, 0f);
                break;

            case MonsterState.Walk:
                animator.Play("Base Layer.Walk", 0, 0f);
                break;

            case MonsterState.Attack:
                animator.Play("Base Layer.Attack", 0, 0f);
                break;

            case MonsterState.GetDamage:
                animator.Play("Base Layer.GetDamage", 0, 0f);
                break;

            case MonsterState.Dead:
                animator.Play("Base Layer.Death", 0, 0f);
                break;
        }
    }

    public void NotifyPlayerAttack(int damageValue)
    {
        if (currentState == MonsterState.Dead)
            return;

        if (MonsterCurrentHP <= 0)
        {
            ChangeState(MonsterState.Dead);
            return;
        }

        GetDamage(damageValue);

        ChangeState(
            MonsterCurrentHP <= 0
                ? MonsterState.Dead
                : MonsterState.GetDamage
        );
    }

    // 공격 애니메이션 마지막에 연결
    public void OnAttackAnimationEnd()
    {
        if (currentState != MonsterState.Attack)
            return;

        isAttackLocked = false;

        ChangeState(MonsterState.Idle);
        UpdateState();
    }

    // 피격 애니메이션 마지막에 연결
    public void OnDamageAnimationEnd()
    {
        if (currentState != MonsterState.GetDamage)
            return;

        ChangeState(MonsterState.Idle);
        UpdateState();
    }

    // 사망 애니메이션 마지막에 연결
    public void OnDeathAnimationEnd()
    {
        if (currentState == MonsterState.Dead)
        {
            gameObject.SetActive(false);
        }
    }

    protected virtual void GetDamage(int damageValue) { }
    protected virtual void Death() { }
    protected virtual void Attack() { }
    protected virtual void Walk() { }
    protected virtual void Idle() { }
}