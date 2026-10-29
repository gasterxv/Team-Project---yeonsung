using UnityEngine;
using UnityEngine.Events;
public class BossController : MonoBehaviour
{
    public enum BossState
    {
        Waiting,
        Roar,
        Walk,
        Pattern1,
        Pattern2,
        Pattern3,
        Death
    }

    [Header("테스트")]
    [SerializeField] private bool enableDebugKeys = true;
    

    [Header("몸과 방향")]
    [SerializeField] private Collider2D bossBodyCollider;
    [SerializeField] private Collider2D playerBodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRight = true;

    [Header("센서")]
    [SerializeField] private BossSensor pattern1Sensor;
    [SerializeField] private BossSensor bodySensor;

    [Header("HP")]
    [SerializeField, Min(1)] private int maxHP = 200;
    [SerializeField] private int currentHP = 200;

    [Header("대기 상태")]
    [SerializeField, Min(1)] private int hitsRequiredToStart = 3;
    private int waitingHitCount;

    [Header("패턴 종료 후 대기 시간")]
    [SerializeField, Min(0f)] private float pattern1Interval = 10f;
    [SerializeField, Min(0f)] private float pattern2Interval = 20f;
    [SerializeField, Min(0f)] private float pattern3Interval = 40f;

    [Header("남은 쿨타임")]
    [SerializeField] private float pattern1Cooldown;
    [SerializeField] private float pattern2Cooldown;
    [SerializeField] private float pattern3Cooldown;

    [Header("현재 상태")]
    [SerializeField] private BossState currentState = BossState.Waiting;

    [Header("몸 접촉 피해")]
    [SerializeField, Min(0)] private int contactDamage = 10;

    [Header("점프 이동")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.1f;
    [SerializeField, Min(0f)] private float jumpHorizontalSpeed = 3f;
    [SerializeField, Min(0.1f)] private float jumpVerticalSpeed = 7f;
    [SerializeField, Min(0f)] private float jumpWaitTime = 0.5f;

    private bool isWalkJumping;
    private float jumpWaitRemaining;

    // 플레이어의 Damaged(int)를 연결할 곳
    [SerializeField]
    private UnityEvent<int> onPlayerContactDamage =
        new UnityEvent<int>();

    private bool wasTouchingPlayer;
    private int facingDirection = 1;

    public BossState CurrentState => currentState;
    public int CurrentHP => currentHP;
    public int FacingDirection => facingDirection;
    private bool isPattern1Jumping;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void Start()
    {
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        // 시작 상태와 기본 방향 설정
        currentState = BossState.Waiting;
        waitingHitCount = 0;
        SetFacing(1);

        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
        }
        else
        {
            OnStateEntered(currentState);
        }
    }

    private void Update()
    {

        // 테스트 키로 상태를 바꾼 프레임에는 일반 판단을 건너뜀
        if (enableDebugKeys && HandleDebugKeys())
            return;

        if (currentState == BossState.Death)
            return;

        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
            return;
        }

        UpdateCooldowns();
        UpdateFacing();

        // 다음 패턴 선택은 Walk 점프 착지 시 처리
    }
    private void StartPattern1Jump()
    {
        body.linearVelocity = new Vector2(
            // 수평 속도는 Walk 점프의 2배
            facingDirection * jumpHorizontalSpeed * 2f,

            // 높이를 약 2배로 만들기 위한 수직 속도
            jumpVerticalSpeed * Mathf.Sqrt(2f)
        );

        isPattern1Jumping = true;
    }
    private void CheckPattern1Landing()
    {
        if (currentState != BossState.Pattern1 ||
            !isPattern1Jumping ||
            body == null ||
            groundCheck == null)
        {
            return;
        }

        // 상승 중에는 착지로 판단하지 않음
        if (body.linearVelocity.y > 0f)
            return;

        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        ) != null;

        if (!isGrounded)
            return;

        isPattern1Jumping = false;

        // 착지 후 수평 이동 정지
        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );

        // 기존 종료 함수가 쿨타임 설정과 Walk 복귀를 처리
        OnPattern1End();
    }
    private void FixedUpdate()
    {
        if (currentState == BossState.Death || currentHP <= 0)
        {
            wasTouchingPlayer = false;
            return;
        }
        CheckPattern1Landing();
        UpdateWalkJump();
        if (currentState == BossState.Death ||
            currentHP <= 0 ||
            bossBodyCollider == null ||
            playerBodyCollider == null ||
            !bossBodyCollider.isActiveAndEnabled ||
            !playerBodyCollider.isActiveAndEnabled)
        {
            wasTouchingPlayer = false;
            return;
        }

        // 두 콜라이더 사이의 실제 거리 확인
        ColliderDistance2D result =
            bossBodyCollider.Distance(playerBodyCollider);

        bool isTouching =
            result.isValid && result.distance <= 0f;

        // 떨어져 있다가 처음 닿았을 때만 피해 전달
        if (isTouching && !wasTouchingPlayer)
        {
            wasTouchingPlayer = true;
            int damage = currentState == BossState.Pattern1 ? 30 : contactDamage;
            onPlayerContactDamage.Invoke(damage);
        }
        else
        {
            wasTouchingPlayer = isTouching;
        }
    }

    private void OnDisable()
    {
        wasTouchingPlayer = false;
    }

    private void UpdateCooldowns()
    {
        pattern1Cooldown = Mathf.Max(
            0f, pattern1Cooldown - Time.deltaTime
        );

        pattern2Cooldown = Mathf.Max(
            0f, pattern2Cooldown - Time.deltaTime
        );

        pattern3Cooldown = Mathf.Max(
            0f, pattern3Cooldown - Time.deltaTime
        );
    }

    private void UpdateFacing()
    {
        if (playerBodyCollider == null || bossBodyCollider == null)
            return;

        float differenceX =
            playerBodyCollider.bounds.center.x -
            bossBodyCollider.bounds.center.x;

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

        if (spriteRenderer == null)
            return;

        bool facingLeft = direction < 0;

        spriteRenderer.flipX = spriteFacesRight
            ? facingLeft
            : !facingLeft;
    }

    private void SelectNextAction()
    {
        if (currentState != BossState.Walk)
            return;

        // 우선순위: Pattern3 → Pattern2 → Pattern1
        if (currentHP <= maxHP * 0.5f &&
            pattern3Cooldown <= 0f)
        {
            ChangeState(BossState.Pattern3);
        }
        else if (bodySensor.IsTouchingDashFloor &&
                 pattern2Cooldown <= 0f)
        {
            ChangeState(BossState.Pattern2);
        }
        else if (pattern1Sensor.HasPlayer &&
                 !bodySensor.IsTouchingDashFloor &&
                 pattern1Cooldown <= 0f)
        {
            ChangeState(BossState.Pattern1);
        }

        // 조건을 만족하지 않으면 Walk 유지
    }

    public void Damaged(int damageValue)
    {
        if (currentState == BossState.Death || damageValue <= 0)
            return;

        currentHP = Mathf.Max(0, currentHP - damageValue);

        // 세 번째 피해와 사망이 동시에 발생하면 사망 우선
        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
            return;
        }

        if (currentState == BossState.Waiting)
        {
            waitingHitCount++;

            if (waitingHitCount >= hitsRequiredToStart)
            {
                ChangeState(BossState.Roar);
            }
        }

        // 전투 중 피해를 받아도 별도의 피격 상태로 바꾸지 않음.
        // Pattern3 조건은 다음 패턴 선택 시 평가.
    }

    private void ChangeState(BossState nextState)
    {
        if (currentState == BossState.Death)
            return;

        if (currentHP <= 0)
            nextState = BossState.Death;

        if (currentState == nextState)
            return;

        // 실행 중인 패턴은 사망 외의 일반 전환을 차단.
        // 정상 종료는 FinishPattern()을 사용.
        if (IsPattern(currentState) &&
            nextState != BossState.Death)
        {
            return;
        }

        if (IsPattern(currentState))
        {
            SetPatternCooldown(currentState);
        }

        currentState = nextState;
        OnStateEntered(nextState);
    }

    private bool IsPattern(BossState state)
    {
        return state == BossState.Pattern1 ||
               state == BossState.Pattern2 ||
               state == BossState.Pattern3;
    }

    private void SetPatternCooldown(BossState finishedState)
    {
        switch (finishedState)
        {
            case BossState.Pattern1:
                pattern1Cooldown = pattern1Interval;
                break;

            case BossState.Pattern2:
                pattern2Cooldown = pattern2Interval;
                break;

            case BossState.Pattern3:
                pattern3Cooldown = pattern3Interval;
                break;
        }
    }

    private void OnStateEntered(BossState state)
    {
        // 상태에 진입할 때 한 번만 실행.
        // 애니메이션 시작이나 패턴 시작 처리는 여기에 연결.
        switch (state)
        {
            case BossState.Waiting:
                break;

            case BossState.Roar:
                // 포효 애니메이션 시작
                break;

            case BossState.Walk:
                // 걷기 애니메이션 시작
                break;

            case BossState.Pattern1:
                // Pattern1 상태에 들어올 때 한 번만 점프
                StartPattern1Jump();
                break;

            case BossState.Pattern2:
                // Pattern2 시작
                break;

            case BossState.Pattern3:
                // Pattern3 시작
                break;

            case BossState.Death:
                isWalkJumping = false;
                isPattern1Jumping = false;
                jumpWaitRemaining = 0f;

                if (body != null)
                {
                    // 수평 이동을 멈추고, 공중에서는 중력으로 떨어지도록 유지
                    body.linearVelocity =
                        new Vector2(0f, body.linearVelocity.y);
                }

                // 추후 사망 애니메이션 연결
                break;
        }
    }

    // 포효 애니메이션 또는 포효 연출이 끝났을 때 호출
    public void OnRoarEnd()
    {
        if (currentState != BossState.Roar)
            return;

        ChangeState(BossState.Walk);
    }

    // 각 패턴의 실제 행동 또는 애니메이션 종료 시 호출
    public void OnPattern1End()
    {
        FinishPattern(BossState.Pattern1);
    }

    public void OnPattern2End()
    {
        FinishPattern(BossState.Pattern2);
    }

    public void OnPattern3End()
    {
        FinishPattern(BossState.Pattern3);
    }

    private void FinishPattern(BossState expectedState)
    {
        // 이미 사망했거나 다른 패턴이면 무시
        if (currentState != expectedState)
            return;

        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
            return;
        }

        // 패턴을 벗어나는 시점부터 쿨타임 시작
        SetPatternCooldown(expectedState);

        currentState = BossState.Walk;
        OnStateEntered(BossState.Walk);
    }

    private void UpdateWalkJump()
    {
        if (body == null || groundCheck == null)
            return;

        // 일반 바닥 레이어만 검사
        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        ) != null;

        // 점프 후, 내려오는 중 바닥을 감지하면 착지 처리
        if (isWalkJumping &&
    body.linearVelocity.y <= 0f &&
    isGrounded)
        {
            isWalkJumping = false;
            jumpWaitRemaining = jumpWaitTime;

            if (currentState == BossState.Walk)
            {
                body.linearVelocity = new Vector2(
                    0f,
                    body.linearVelocity.y
                );

                // 한 번의 점프가 끝난 시점에만 패턴 판단
                SelectNextAction();
            }
        }

        // 다른 상태에서는 새 점프를 시작하지 않음
        if (currentState != BossState.Walk || currentHP <= 0)
            return;

        // 공중에서는 다시 점프하지 않음
        if (isWalkJumping ||
            !isGrounded ||
            Mathf.Abs(body.linearVelocity.y) > 0.1f)
        {
            return;
        }

        // 바닥에서 점프 사이 대기
        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );

        if (jumpWaitRemaining > 0f)
        {
            jumpWaitRemaining = Mathf.Max(
                0f,
                jumpWaitRemaining - Time.fixedDeltaTime
            );

            return;
        }

        Walk();
    }

    private void Walk()
    {
        // 점프하는 순간 바라보는 방향으로 속도 설정
        body.linearVelocity = new Vector2(
            facingDirection * jumpHorizontalSpeed,
            jumpVerticalSpeed
        );

        isWalkJumping = true;
    }

    private void UpdatePattern1()
    {
        // Pattern1 상태에서 매 프레임 필요한 처리
    }

    private void UpdatePattern2()
    {
        // Pattern2 상태에서 매 프레임 필요한 처리
    }

    private void UpdatePattern3()
    {
        // Pattern3 상태에서 매 프레임 필요한 처리
    }

    private bool HandleDebugKeys()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null)
            return false;

        if (keyboard.qKey.wasPressedThisFrame)
            DebugChangeState(BossState.Waiting);
        else if (keyboard.wKey.wasPressedThisFrame)
            DebugChangeState(BossState.Roar);
        else if (keyboard.eKey.wasPressedThisFrame)
            DebugChangeState(BossState.Walk);
        else if (keyboard.rKey.wasPressedThisFrame)
            DebugChangeState(BossState.Pattern1);
        else if (keyboard.aKey.wasPressedThisFrame)
            DebugChangeState(BossState.Pattern2);
        else if (keyboard.sKey.wasPressedThisFrame)
            DebugChangeState(BossState.Pattern3);
        else if (keyboard.dKey.wasPressedThisFrame)
            DebugChangeState(BossState.Death);
        else
            return false;

#elif ENABLE_LEGACY_INPUT_MANAGER
    if (Input.GetKeyDown(KeyCode.Q))
        DebugChangeState(BossState.Waiting);
    else if (Input.GetKeyDown(KeyCode.W))
        DebugChangeState(BossState.Roar);
    else if (Input.GetKeyDown(KeyCode.E))
        DebugChangeState(BossState.Walk);
    else if (Input.GetKeyDown(KeyCode.R))
        DebugChangeState(BossState.Pattern1);
    else if (Input.GetKeyDown(KeyCode.A))
        DebugChangeState(BossState.Pattern2);
    else if (Input.GetKeyDown(KeyCode.S))
        DebugChangeState(BossState.Pattern3);
    else if (Input.GetKeyDown(KeyCode.D))
        DebugChangeState(BossState.Death);
    else
        return false;
#else
    return false;
#endif

#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
        return true;
#endif
    }

    private void DebugChangeState(BossState nextState)
    {
        // 사망 후에는 테스트 키로도 상태를 바꾸지 않음
        if (currentState == BossState.Death)
            return;

        if (currentHP <= 0)
            nextState = BossState.Death;

        if (currentState == nextState)
            return;

        // 실행 중이던 패턴의 쿨타임 설정
        if (IsPattern(currentState))
            SetPatternCooldown(currentState);

        // 이전 점프 기록과 이동 속도 정리
        isWalkJumping = false;
        isPattern1Jumping = false;
        jumpWaitRemaining = 0f;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        if (nextState == BossState.Waiting)
            waitingHitCount = 0;

        // 테스트에서는 패턴 잠금·진입 조건을 건너뛰고 변경
        currentState = nextState;
        OnStateEntered(nextState);

        Debug.Log($"보스 테스트 상태: {currentState}", this);
    }
}