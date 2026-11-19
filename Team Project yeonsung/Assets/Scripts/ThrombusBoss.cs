using UnityEngine;

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

    [Header("물리 충돌")]
    [SerializeField] private LayerMask terrainForceLayers;

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
    private bool hasStartedCombat;

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

    [Header("포효")]
    [SerializeField, Min(0f)] private float roarDuration = 3f;

    [Header("패턴 2: 돌진")]
    [SerializeField, Min(0f)] private float pattern2DashSpeed = 5f;
    [SerializeField] private string pattern2StopTag = "BossRoomTacle";
    private int pattern2Direction = 1;

    [Header("패턴 3: 반사 점프")]
    [SerializeField, Min(0.1f)] private float pattern3Impulse = 20f;
    [SerializeField, Range(15f, 75f)] private float pattern3LaunchAngle = 45f;
    [SerializeField, Min(1)] private int pattern3BounceLimit = 4;
    [Tooltip("거의 수평/수직으로만 튕기지 않도록 하는 최소 축 성분")]
    [SerializeField, Range(0.1f, 0.5f)] private float pattern3MinAxis = 0.35f;
    private int pattern3BounceCount;
    private Vector2 pattern3IncomingVelocity;
    private float pattern3LastBounceTime = float.NegativeInfinity;

    [Header("사망 이미지")]
    [SerializeField] private bool isSpriteChange = false;

    // Resources 폴더를 기준으로 한 경로. 확장자는 제외합니다.
    [SerializeField] private string deathSpritePath = "몬스터/보스/보스사망";

    private float nextJumpDebugTime;

    public bool IsSpriteChange => isSpriteChange;

    private bool isWalkJumping;
    private float jumpWaitRemaining;



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

        if (body == null)
        {
            Debug.LogError("보스에 Rigidbody2D가 필요합니다.", this);
            return;
        }

        // 현재 각도를 유지하고 물리 회전을 방지
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.angularVelocity = 0f;

        // 보스 Rigidbody2D에 연결된 콜라이더에 적용
        foreach (Collider2D collider in
                 body.GetComponentsInChildren<Collider2D>(true))
        {
            // 별도의 Rigidbody2D를 가진 자식은 제외
            if (collider.attachedRigidbody != body)
                continue;


        }
    }
    private void LogJumpStatus()
    {
        if (Time.time < nextJumpDebugTime)
            return;

        nextJumpDebugTime = Time.time + 1f;

        if (body == null || groundCheck == null)
        {
            Debug.LogWarning(
                $"[점프 확인] 상태={currentState}, " +
                $"Body 연결={body != null}, " +
                $"Ground Check 연결={groundCheck != null}",
                this
            );
            return;
        }

        Collider2D ground = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        string groundName = ground != null ? ground.name : "없음";

        Debug.Log(
            $"[점프 확인] 상태={currentState}, " +
            $"바닥={groundName}, " +
            $"점프중={isWalkJumping}, " +
            $"Y속도={body.linearVelocity.y:F3}, " +
            $"대기={jumpWaitRemaining:F2}, " +
            $"물리타입={body.bodyType}, " +
            $"Simulated={body.simulated}, " +
            $"고정={body.constraints}",
            this
        );
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
        if (currentState == BossState.Death)
            return;

        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
            return;
        }

        if (enableDebugKeys && HandleDebugKeys())
            return;

        UpdateCooldowns();

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
        UpdateFacing();
        OnPattern1End();
    }
    // 플레이어가 부활하면 새 인스턴스를 자동으로 연결합니다.
    private PlayerController damageTarget;

    private void RefreshPlayerTarget()
    {
        if (damageTarget != null && playerBodyCollider != null)
            return;

        damageTarget = FindFirstObjectByType<PlayerController>();

        if (damageTarget == null)
        {
            playerBodyCollider = null;
            wasTouchingPlayer = false;
            return;
        }

        if (playerBodyCollider == null ||
            playerBodyCollider.GetComponentInParent<PlayerController>() != damageTarget)
        {
            playerBodyCollider = damageTarget.GetComponent<Collider2D>();

            if (playerBodyCollider == null)
            {
                foreach (Collider2D candidate in
                         damageTarget.GetComponentsInChildren<Collider2D>())
                {
                    if (candidate.isActiveAndEnabled && !candidate.isTrigger)
                    {
                        playerBodyCollider = candidate;
                        break;
                    }
                }
            }
        }

        wasTouchingPlayer = false;
    }

    private void FixedUpdate()
    {
        RefreshPlayerTarget();

        // 몸끼리 밀어내는 대신 거리 판정과 플레이어 넉백을 사용합니다.
        // 센서/공격 히트박스와 바닥 충돌은 변경하지 않습니다.
        if (bossBodyCollider != null && playerBodyCollider != null &&
            bossBodyCollider.isActiveAndEnabled && playerBodyCollider.isActiveAndEnabled)
        {
            Physics2D.IgnoreCollision(bossBodyCollider, playerBodyCollider, true);
        }


        if (currentState == BossState.Death || currentHP <= 0)
        {
            wasTouchingPlayer = false;
            return;
        }
        UpdateFacing();
        LogJumpStatus(); // 추가
        CheckPattern1Landing();
        UpdateWalkJump();
        UpdatePattern2();
        UpdatePattern3();

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

        // 대기 상태에서는 접촉 피해를 주지 않습니다.
        if (currentState == BossState.Waiting)
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
            if (damageTarget != null && !damageTarget.IsDead && damage > 0)
            {
                damageTarget.TakeDamageFrom(damage, bossBodyCollider.bounds.center);
            }
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
        // 돌진 시작 시 정한 방향을 끝날 때까지 유지한다.
        if (currentState == BossState.Pattern2 || currentState == BossState.Pattern3)
            return;

        if (body == null ||
            groundCheck == null ||
            playerBodyCollider == null ||
            bossBodyCollider == null)
        {
            return;
        }

        // 점프가 진행 중이면 방향을 바꾸지 않음
        if (isWalkJumping || isPattern1Jumping)
            return;

        // 바닥 근처를 지나가는 공중 상태도 제외
        if (Mathf.Abs(body.linearVelocity.y) > 0.1f)
            return;

        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        ) != null;

        if (!isGrounded)
            return;

        float differenceX =
            playerBodyCollider.bounds.center.x -
            bossBodyCollider.bounds.center.x;

        if (differenceX > 0f)
            SetFacing(1);
        else if (differenceX < 0f)
            SetFacing(-1);

        // X좌표가 같으면 기존 방향 유지
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

    public void NotifyPlayerAttack(int damageValue)
    {
        Damaged(damageValue);
    }

    public void Damaged(int damageValue)
    {
        if (currentState == BossState.Death || damageValue <= 0)
            return;

        Debug.Log("피격 받았다", this); // 추가

        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
            return;
        }

        // 대기 중에는 HP를 깎지 않고 피격 횟수만 기록
        if (currentState == BossState.Waiting)
        {
            waitingHitCount++;

            Debug.Log(
                $"보스 대기 중 피격: {waitingHitCount}/{hitsRequiredToStart}, " +
                $"받은 피해량: {damageValue}",
                this
            );

            if (waitingHitCount >= hitsRequiredToStart)
            {
                ChangeState(BossState.Roar);
            }

            return;
        }

        // Roar를 포함한 전투 중에는 실제 HP 감소
        currentHP = Mathf.Max(0, currentHP - damageValue);

        Debug.Log(
            $"보스 피해: {damageValue}, 남은 HP: {currentHP}",
            this
        );

        if (currentHP <= 0)
        {
            ChangeState(BossState.Death);
        }
    }

    public void TakeDamage(int damageValue)
    {
        NotifyPlayerAttack(damageValue);

    }
    private void ChangeState(BossState nextState)
    {
        if (currentState == BossState.Death)
            return;

        // 사망을 다른 제한보다 먼저 판단
        if (currentHP <= 0)
            nextState = BossState.Death;

        // 전투 시작 후 Waiting·Roar 재진입 금지
        if (hasStartedCombat &&
            (nextState == BossState.Waiting ||
             nextState == BossState.Roar))
        {
            return;
        }

        if (currentState == nextState)
            return;

        // 패턴 실행 중에는 사망 외 일반 상태 전환 차단
        // 정상적인 패턴 종료는 FinishPattern()에서 처리
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
                hasStartedCombat = true;

                CancelInvoke(nameof(OnRoarEnd));
                Invoke(nameof(OnRoarEnd), roarDuration);
                break;



            case BossState.Walk:
                // 걷기 애니메이션 시작
                break;

            case BossState.Pattern1:
                // Pattern1 상태에 들어올 때 한 번만 점프
                StartPattern1Jump();
                break;

            case BossState.Pattern2:
                StartPattern2Dash();
                break;

            case BossState.Pattern3:
                StartPattern3Bounce();
                break;

            case BossState.Death:
                EnterDeath();
                break;
        }
    }

    private void EnterDeath()
    {
        // 행동 기록 정리
        isWalkJumping = false;
        isPattern1Jumping = false;
        jumpWaitRemaining = 0f;
        wasTouchingPlayer = false;

        // 사망 위치에 정지
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        // 이 스크립트와 같은 오브젝트에 있는 콜라이더
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            collider.isTrigger = true;
        }

        // 몸 콜라이더가 자식 오브젝트에 있어도 처리
        if (bossBodyCollider != null)
        {
            bossBodyCollider.isTrigger = true;
        }

        // 스프라이트 변경 상태를 켜고 이미지 교체
        isSpriteChange = true;
        ApplyDeathSprite();
    }

    private void ApplyDeathSprite()
    {
        if (!isSpriteChange)
            return;

        if (spriteRenderer == null)
        {
            Debug.LogError(
                "BossController의 Sprite Renderer에 " +
                "BossForm의 Sprite Renderer를 연결하세요.",
                this
            );
            return;
        }

        Sprite deathSprite = Resources.Load<Sprite>(deathSpritePath);

        if (deathSprite == null)
        {
            Debug.LogError(
                $"사망 이미지를 찾지 못했습니다: Resources/{deathSpritePath}\n" +
                "파일 경로와 이미지의 Sprite 설정을 확인하세요.",
                this
            );
            return;
        }

        spriteRenderer.sprite = deathSprite;
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
        if (currentState != BossState.Pattern2)
            return;

        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

        jumpWaitRemaining = jumpWaitTime;
        FinishPattern(BossState.Pattern2);
    }

    public void OnPattern3End()
    {
        if (currentState != BossState.Pattern3) return;
        if (body != null) body.linearVelocity = Vector2.zero;
        isWalkJumping = false;
        isPattern1Jumping = false;
        jumpWaitRemaining = jumpWaitTime;
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
                UpdateFacing();
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
        if (body == null)
            return;

        if (playerBodyCollider == null || bossBodyCollider == null)
        {
            Debug.LogWarning(
                "[방향 확인] Player Body Collider 또는 " +
                "Boss Body Collider가 연결되지 않았습니다.",
                this
            );
            return;
        }

        // UpdateWalkJump에서 바닥 조건을 통과한 후 호출됨
        UpdateFacing();

        float playerX = playerBodyCollider.bounds.center.x;
        float bossX = bossBodyCollider.bounds.center.x;

        body.linearVelocity = new Vector2(
            facingDirection * jumpHorizontalSpeed,
            jumpVerticalSpeed
        );

        isWalkJumping = true;

        Debug.Log(
            $"[점프 방향] 대상={playerBodyCollider.name}, " +
            $"플레이어X={playerX:F2}, 보스X={bossX:F2}, " +
            $"방향={facingDirection}, " +
            $"X속도={body.linearVelocity.x:F2}",
            this
        );
    }

    private void UpdateJumpTracking()
    {
        if (body == null ||
            playerBodyCollider == null ||
            bossBodyCollider == null)
        {
            return;
        }

        float horizontalSpeed;

        if (currentState == BossState.Walk && isWalkJumping)
        {
            horizontalSpeed = jumpHorizontalSpeed;
        }
        else if (currentState == BossState.Pattern1 && isPattern1Jumping)
        {
            horizontalSpeed = jumpHorizontalSpeed * 2f;
        }
        else
        {
            return;
        }

        // 플레이어 방향으로 수평 속도만 변경
        // 상승·낙하 속도는 그대로 유지
        body.linearVelocity = new Vector2(
            facingDirection * horizontalSpeed,
            body.linearVelocity.y
        );
    }
    private void StartPattern2Dash()
    {
        if (body == null || playerBodyCollider == null)
        {
            Debug.LogWarning("돌진에 필요한 Body 또는 Player Body Collider를 연결하세요.", this);
            OnPattern2End();
            return;
        }

        float differenceX = playerBodyCollider.transform.position.x - transform.position.x;
        // X좌표가 같으면 기존 바라보는 방향을 사용한다.
        pattern2Direction = differenceX > 0f ? 1 : differenceX < 0f ? -1 : facingDirection;
        SetFacing(pattern2Direction);
        isWalkJumping = false;
        isPattern1Jumping = false;
        UpdatePattern2();
    }

    private void UpdatePattern2()
    {
        if (currentState != BossState.Pattern2 || currentHP <= 0 || body == null)
            return;

        // 수직 속도와 중력은 유지하고 수평 속도만 일정하게 설정한다.
        body.linearVelocity = new Vector2(
            pattern2Direction * pattern2DashSpeed,
            body.linearVelocity.y
        );
    }

    // Body 센서가 자식 오브젝트에 있는 경우에도 이 함수로 접촉을 전달한다.
    public void NotifyPattern2Contact(Collider2D other)
    {
        if (currentState != BossState.Pattern2 || other == null)
            return;

        if (other.CompareTag(pattern2StopTag))
            OnPattern2End();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        NotifyPattern2Contact(collision.collider);
        NotifyPattern3Collision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        NotifyPattern2Contact(collision.collider);
        NotifyPattern3Collision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        NotifyPattern2TriggerContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        NotifyPattern2TriggerContact(other);
    }

    private void NotifyPattern2TriggerContact(Collider2D other)
    {
        // 넓은 패턴 감지 센서가 벽에 먼저 닿았다고 돌진을 끝내지 않는다.
        if (currentState != BossState.Pattern2 || other == null || bossBodyCollider == null)
            return;
        ColliderDistance2D distance = bossBodyCollider.Distance(other);
        if (distance.isValid && distance.distance <= 0f)
            NotifyPattern2Contact(other);
    }

    private void UpdatePattern3()
    {
        if (currentState != BossState.Pattern3 || body == null) return;
        // 충돌 처리로 속도가 0이 되기 전의 진행 방향을 저장한다.
        pattern3IncomingVelocity = body.linearVelocity;
    }

    private void StartPattern3Bounce()
    {
        if (body == null || playerBodyCollider == null || bossBodyCollider == null)
        {
            Debug.LogWarning("패턴 3의 Body, Boss Body Collider, Player Body Collider를 연결하세요.", this);
            OnPattern3End();
            return;
        }

        float differenceX = playerBodyCollider.transform.position.x - transform.position.x;
        int direction = differenceX > 0f ? 1 : differenceX < 0f ? -1 : facingDirection;
        float angle = pattern3LaunchAngle * Mathf.Deg2Rad;
        pattern3BounceCount = 0;
        pattern3LastBounceTime = float.NegativeInfinity;
        isWalkJumping = false;
        isPattern1Jumping = false;
        ApplyPattern3Impulse(new Vector2(direction * Mathf.Cos(angle), Mathf.Sin(angle)));
    }

    private bool IsPattern3Surface(Collider2D other)
    {
        if (other == null || other.isTrigger) return false;
        // tag 문자열 비교는 프로젝트에 별칭 태그가 없어도 예외를 발생시키지 않는다.
        string surfaceTag = other.tag;
        return surfaceTag == "Ground" || surfaceTag == "BossRoomTacle" ||
            surfaceTag == "BoosRoomDashFloor" || surfaceTag == "BoseRoomDashFloor";
    }

    public void NotifyPattern3Collision(Collision2D collision)
    {
        if (currentState != BossState.Pattern3 || currentHP <= 0 || body == null ||
            !IsPattern3Surface(collision.collider) ||
            pattern3LastBounceTime == Time.fixedTime)
            return;

        Vector2 incoming = pattern3IncomingVelocity;
        if (incoming.sqrMagnitude < 0.001f) return;

        // 모서리에 여러 접점이 있으면 가장 정면으로 부딪힌 면을 선택한다.
        Vector2 normal = Vector2.zero;
        float strongestApproach = -0.01f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 candidate = collision.GetContact(i).normal;
            float approach = Vector2.Dot(incoming, candidate);
            if (approach < strongestApproach)
            {
                strongestApproach = approach;
                normal = candidate;
            }
        }
        // 점프 직후 아직 바닥과 닿아 있어도, 바닥에서 멀어지는 중이면 무시한다.
        if (normal.sqrMagnitude < 0.5f) return;

        if (pattern3BounceCount >= pattern3BounceLimit)
        {
            // 마지막 반동의 비행을 끝낸 뒤 바닥에 착지하면 이동 상태로 복귀한다.
            // 벽/천장 접촉은 추가 충격 없이 일반 물리 충돌로 처리한다.
            if (normal.y > 0.5f && incoming.y < 0f) OnPattern3End();
            return;
        }

        Vector2 reflected = Vector2.Reflect(incoming.normalized, normal).normalized;
        float xSign = reflected.x < 0f ? -1f : reflected.x > 0f ? 1f : facingDirection;
        float ySign = reflected.y < 0f ? -1f : 1f;
        reflected = new Vector2(
            xSign * Mathf.Max(Mathf.Abs(reflected.x), pattern3MinAxis),
            ySign * Mathf.Max(Mathf.Abs(reflected.y), pattern3MinAxis)
        ).normalized;
        // 경사면에서 각도 보정 때문에 표면 안쪽을 향하지 않도록 보장한다.
        if (Vector2.Dot(reflected, normal) <= 0f)
            reflected = Vector2.Reflect(incoming.normalized, normal).normalized;

        pattern3LastBounceTime = Time.fixedTime;
        pattern3BounceCount++;
        ApplyPattern3Impulse(reflected);
    }

    private void ApplyPattern3Impulse(Vector2 direction)
    {
        // 매번 동일한 충격량으로 발사하여 이전 속도가 누적되지 않게 한다.
        body.linearVelocity = Vector2.zero;
        body.AddForce(direction.normalized * pattern3Impulse, ForceMode2D.Impulse);
        pattern3IncomingVelocity = direction.normalized * (pattern3Impulse / body.mass);
        if (Mathf.Abs(direction.x) > 0.001f) SetFacing(direction.x > 0f ? 1 : -1);
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
        if (currentState == BossState.Death)
            return;

        if (currentHP <= 0)
            nextState = BossState.Death;

        // 전투 시작 후 Q·W로 대기·포효에 돌아가지 못하게 함
        if (hasStartedCombat &&
            (nextState == BossState.Waiting ||
             nextState == BossState.Roar))
        {
            return;
        }

        if (currentState == nextState)
            return;

        // 진행 중이던 패턴에서 나가면 쿨타임 설정
        if (IsPattern(currentState))
        {
            SetPatternCooldown(currentState);
        }

        // 이전 이동 기록 정리
        isWalkJumping = false;
        isPattern1Jumping = false;
        jumpWaitRemaining = 0f;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (nextState == BossState.Waiting)
        {
            waitingHitCount = 0;
        }

        // 테스트로 포효를 건너뛰고 전투에 들어가도 시작 기록
        if (nextState == BossState.Roar ||
            nextState == BossState.Walk ||
            IsPattern(nextState))
        {
            hasStartedCombat = true;
        }

        currentState = nextState;
        OnStateEntered(nextState);

        Debug.Log($"보스 테스트 상태: {currentState}", this);
    }
}
