using UnityEngine;

// Unity 6 기준. BossSensor / PlayerController는 기존 스크립트를 사용합니다.
public class BossController : MonoBehaviour
{
    // 기존 Inspector enum 값 보존: 제거한 Roar의 값 1은 사용하지 않습니다.
    public enum BossState { Waiting = 0, Walk = 2, Pattern1 = 3, Pattern2 = 4, Pattern3 = 5, Death = 6 }
    private enum PatternPhase { None, Ready, Active, Recovery }

    [Header("애니메이션: BossForm의 Animator")]
    [SerializeField] private Animator animator;
    [Header("테스트: Q 대기 / E 이동 / R 패턴1 / A 패턴2 / S 패턴3 / D 사망")]
    [SerializeField] private bool enableDebugKeys = true;
    [Header("몸과 방향")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bossBodyCollider;
    [SerializeField] private Collider2D playerBodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRight = true;
    [Header("센서")]
    [SerializeField] private BossSensor pattern1Sensor;
    [SerializeField] private BossSensor bodySensor;
    [Header("HP와 전투 시작")]
    [SerializeField, Min(1)] private int maxHP = 200;
    [SerializeField] private int currentHP = 200;
    [SerializeField, Min(1)] private int hitsRequiredToStart = 3;
    [SerializeField, Min(0)] private int contactDamage = 10;
    [Header("패턴 종료 후 쿨타임")]
    [SerializeField, Min(0f)] private float pattern1Interval = 10f;
    [SerializeField, Min(0f)] private float pattern2Interval = 20f;
    [SerializeField, Min(0f)] private float pattern3Interval = 40f;
    [SerializeField] private float pattern1Cooldown;
    [SerializeField] private float pattern2Cooldown;
    [SerializeField] private float pattern3Cooldown;
    [Header("일반 점프 이동")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.1f;
    [SerializeField, Min(0f)] private float jumpHorizontalSpeed = 3f;
    [SerializeField, Min(0.1f)] private float jumpVerticalSpeed = 7f;
    [SerializeField, Min(0f)] private float jumpWaitTime = 0.5f;
    [Header("패턴 2: 구르기")]
    [SerializeField, Min(0f)] private float pattern2DashSpeed = 5f;
    [SerializeField, Min(0.02f)] private float pattern2ReadyDuration = 0.5f;
    [SerializeField, Min(0.02f)] private float pattern2StopDuration = 0.4f;
    [SerializeField] private string pattern2StopTag = "BossRoomTacle";
    [Header("패턴 3: 반사 점프")]
    [SerializeField, Min(0.1f)] private float pattern3Impulse = 20f;
    [SerializeField, Range(15f, 75f)] private float pattern3LaunchAngle = 45f;
    [SerializeField, Min(1)] private int pattern3BounceLimit = 4;
    [SerializeField, Range(0.1f, 0.5f)] private float pattern3MinAxis = 0.35f;
    [Tooltip("물리 반사는 즉시 처리하고 충돌 스프라이트만 이 시간 동안 표시합니다.")]
    [SerializeField, Min(0.02f)] private float pattern3ImpactDuration = 0.12f;
    [Header("현재 상태 (확인용)")]
    [SerializeField] private BossState currentState = BossState.Waiting;
    [SerializeField] private PatternPhase phase;

    private const string Idle = "BossIdle";
    private const string WalkAnimation = "BossWalk";
    private const string Dead = "BossDead";
    private const string P1Ready = "BossPattern1_Ready";
    private const string P1Sky = "BossPattern1_Sky";
    private const string P1Drop = "BossPattern1_Drop";
    private const string P1Land = "BossPattern1_land";
    private const string P2Ready = "BossPattern2_Ready";
    private const string P2Roll = "BossPattern2_Rolling";
    private const string P2Stop = "BossPattern2_RollingStop";
    private const string P3Ready = "BossPattern3_Ready";
    private const string P3Floor = "BossPattern3_Collide_floor";
    private const string P3Ceiling = "BossPattern3_Collide_Ceiling";
    private const string P3Jump = "BossPattern3_Jump";
    private const string P3Up = "BossPattern3_diagonal_UP";
    private const string P3Down = "BossPattern3_diagonal_Down";

    private PlayerController damageTarget;
    private bool hasStartedCombat;
    private bool destructionRequested;
    private GameObject deathRoot;
    public event System.Action Defeated;
    private bool wasTouchingPlayer;
    private bool isWalkJumping;
    private bool hasLeftGround;
    private int waitingHitCount;
    private int facingDirection = 1;
    private int pattern2Direction = 1;
    private int pattern3BounceCount;
    private float jumpWaitRemaining;
    private float phaseRemaining;
    private float impactUntil;
    private float pattern3LastBounceTime = float.NegativeInfinity;
    private Vector2 pattern3IncomingVelocity;
    private string playingAnimation;
    private int animationStartedFrame;

    public BossState CurrentState => currentState;
    public int CurrentHP => currentHP;
    public int FacingDirection => facingDirection;
    // 기존 외부 코드와의 호환용. 사망 이미지는 이제 BossDead가 담당합니다.
    public bool IsSpriteChange => currentState == BossState.Death;

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null && spriteRenderer != null)
            animator = spriteRenderer.GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (body == null || bossBodyCollider == null || groundCheck == null ||
            animator == null || animator.runtimeAnimatorController == null ||
            spriteRenderer == null || pattern1Sensor == null || bodySensor == null)
        {
            Debug.LogError("보스의 Body, Boss Body Collider, Ground Check, Sprite Renderer, Animator 및 두 Sensor를 연결하세요.", this);
            enabled = false;
            return;
        }
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.angularVelocity = 0f;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Base Layer 바로 아래에 같은 이름의 상태가 있어야 합니다.
        string[] states = { Idle, WalkAnimation, Dead, P1Ready, P1Sky, P1Drop, P1Land,
            P2Ready, P2Roll, P2Stop, P3Ready, P3Floor, P3Ceiling, P3Jump, P3Up, P3Down };
        foreach (string state in states)
        {
            if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state)))
            {
                Debug.LogError("Animator Base Layer에서 상태를 찾지 못했습니다: " + state, this);
                enabled = false;
            }
        }
    }

    private void Start()
    {
        if (!enabled) return;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        currentState = BossState.Waiting;
        SetFacing(1);
        PlayAnimation(Idle);
        if (currentHP <= 0) ChangeState(BossState.Death);
    }

    public void SetDeathRoot(GameObject root)
    {
        // 보스 자신 또는 보스를 포함하는 루트만 삭제 대상으로 허용합니다.
        if (root != null && (root == gameObject || transform.IsChildOf(root.transform)))
            deathRoot = root;
    }

    private void Update()
    {
        if (currentState == BossState.Death)
        {
            if (!destructionRequested && AnimationFinished(Dead))
            {
                destructionRequested = true;
                GameObject target = deathRoot != null ? deathRoot : gameObject;
                target.SetActive(false);
                Destroy(target);
            }
            return;
        }
        if (currentHP <= 0) { ChangeState(BossState.Death); return; }
        if (enableDebugKeys && HandleDebugKeys()) return;
        pattern1Cooldown = Mathf.Max(0f, pattern1Cooldown - Time.deltaTime);
        pattern2Cooldown = Mathf.Max(0f, pattern2Cooldown - Time.deltaTime);
        pattern3Cooldown = Mathf.Max(0f, pattern3Cooldown - Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (currentState == BossState.Death || currentHP <= 0) return;
        RefreshPlayerTarget();
        if (playerBodyCollider != null && playerBodyCollider.isActiveAndEnabled &&
            bossBodyCollider.isActiveAndEnabled)
            Physics2D.IgnoreCollision(bossBodyCollider, playerBodyCollider, true);

        UpdateFacing();
        switch (currentState)
        {
            case BossState.Waiting: StopHorizontal(); break;
            case BossState.Walk: UpdateWalkJump(); break;
            case BossState.Pattern1: UpdatePattern1(); break;
            case BossState.Pattern2: UpdatePattern2(); break;
            case BossState.Pattern3: UpdatePattern3(); break;
        }
        UpdateContactDamage();
    }

    private void PlayAnimation(string state, bool restart = false)
    {
        // 매 프레임 처음부터 재생하는 것을 방지합니다.
        if (!restart && playingAnimation == state) return;
        playingAnimation = state;
        animationStartedFrame = Time.frameCount;
        animator.Play(Animator.StringToHash("Base Layer." + state), 0, 0f);
    }

    private bool AnimationFinished(string state)
    {
        // Play 직후에는 Animator가 이전 상태를 보고할 수 있으므로 다음 프레임부터 검사합니다.
        if (playingAnimation != state || Time.frameCount <= animationStartedFrame) return false;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.fullPathHash == Animator.StringToHash("Base Layer." + state) &&
               info.normalizedTime >= 1f;
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    private void StopHorizontal()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void SetFacing(int direction)
    {
        facingDirection = direction < 0 ? -1 : 1;
        spriteRenderer.flipX = spriteFacesRight ? facingDirection < 0 : facingDirection > 0;
    }

    private void UpdateFacing()
    {
        if (IsPattern(currentState) || isWalkJumping || playerBodyCollider == null ||
            !IsGrounded() || Mathf.Abs(body.linearVelocity.y) > 0.1f) return;
        float difference = playerBodyCollider.bounds.center.x - bossBodyCollider.bounds.center.x;
        if (Mathf.Abs(difference) > 0.001f) SetFacing(difference > 0f ? 1 : -1);
    }

    private int PlayerDirection()
    {
        if (playerBodyCollider == null) return facingDirection;
        float difference = playerBodyCollider.bounds.center.x - bossBodyCollider.bounds.center.x;
        return Mathf.Abs(difference) < 0.001f ? facingDirection : difference > 0f ? 1 : -1;
    }

    private bool IsPattern(BossState state)
    {
        return state == BossState.Pattern1 || state == BossState.Pattern2 || state == BossState.Pattern3;
    }

    private void ChangeState(BossState nextState)
    {
        if (currentState == BossState.Death) return;
        if (currentHP <= 0) nextState = BossState.Death;
        if (hasStartedCombat && nextState == BossState.Waiting) return;
        if (currentState == nextState) return;
        if (IsPattern(currentState) && nextState != BossState.Death) return;
        currentState = nextState;
        EnterState();
    }

    private void EnterState()
    {
        phase = PatternPhase.None;
        phaseRemaining = 0f;
        impactUntil = 0f;
        isWalkJumping = false;
        hasLeftGround = false;
        StopHorizontal();
        if (currentState != BossState.Waiting) hasStartedCombat = true;
        switch (currentState)
        {
            case BossState.Waiting: PlayAnimation(Idle); break;
            case BossState.Walk:
                jumpWaitRemaining = jumpWaitTime;
                PlayAnimation(Idle);
                break;
            case BossState.Pattern1:
                SetFacing(PlayerDirection());
                phase = PatternPhase.Ready;
                PlayAnimation(P1Ready, true);
                break;
            case BossState.Pattern2:
                pattern2Direction = PlayerDirection();
                SetFacing(pattern2Direction);
                phase = PatternPhase.Ready;
                phaseRemaining = pattern2ReadyDuration;
                PlayAnimation(P2Ready, true);
                break;
            case BossState.Pattern3:
                SetFacing(PlayerDirection());
                phase = PatternPhase.Ready;
                pattern3BounceCount = 0;
                pattern3LastBounceTime = float.NegativeInfinity;
                pattern3IncomingVelocity = Vector2.zero;
                PlayAnimation(P3Ready, true);
                break;
            case BossState.Death: EnterDeath(); break;
        }
    }

    private void UpdateWalkJump()
    {
        bool grounded = IsGrounded();
        if (isWalkJumping)
        {
            if (!grounded) hasLeftGround = true;
            if (!hasLeftGround || body.linearVelocity.y > 0f || !grounded) return;
            isWalkJumping = false;
            StopHorizontal();
            jumpWaitRemaining = jumpWaitTime;
            UpdateFacing();
            PlayAnimation(Idle);
            SelectNextAction();
            return;
        }
        if (!grounded || Mathf.Abs(body.linearVelocity.y) > 0.1f) return;
        StopHorizontal();
        jumpWaitRemaining -= Time.fixedDeltaTime;
        if (jumpWaitRemaining > 0f || playerBodyCollider == null) return;
        UpdateFacing();
        body.linearVelocity = new Vector2(facingDirection * jumpHorizontalSpeed, jumpVerticalSpeed);
        isWalkJumping = true;
        hasLeftGround = false;
        PlayAnimation(WalkAnimation, true);
    }

    private void SelectNextAction()
    {
        if (currentState != BossState.Walk || playerBodyCollider == null) return;
        if (currentHP <= maxHP * 0.5f && pattern3Cooldown <= 0f)
            ChangeState(BossState.Pattern3);
        else if (bodySensor.IsTouchingDashFloor && pattern2Cooldown <= 0f)
            ChangeState(BossState.Pattern2);
        else if (pattern1Sensor.HasPlayer && !bodySensor.IsTouchingDashFloor && pattern1Cooldown <= 0f)
            ChangeState(BossState.Pattern1);
    }

    private void UpdatePattern1()
    {
        if (phase == PatternPhase.Ready)
        {
            StopHorizontal();
            if (!AnimationFinished(P1Ready) || !IsGrounded()) return;
            phase = PatternPhase.Active;
            body.linearVelocity = new Vector2(facingDirection * jumpHorizontalSpeed * 2f,
                jumpVerticalSpeed * Mathf.Sqrt(2f));
            hasLeftGround = false;
            PlayAnimation(P1Sky);
            return;
        }
        if (phase == PatternPhase.Recovery)
        {
            StopHorizontal();
            if (AnimationFinished(P1Land)) OnPattern1End();
            return;
        }
        if (phase != PatternPhase.Active) return;
        bool grounded = IsGrounded();
        if (!grounded) hasLeftGround = true;
        if (body.linearVelocity.y > 0f) return;
        PlayAnimation(P1Drop);
        if (!hasLeftGround || !grounded) return;
        StopHorizontal();
        phase = PatternPhase.Recovery;
        PlayAnimation(P1Land, true);
    }

    private void UpdatePattern2()
    {
        if (phase == PatternPhase.Active)
        {
            body.linearVelocity = new Vector2(pattern2Direction * pattern2DashSpeed, body.linearVelocity.y);
            return;
        }
        StopHorizontal();
        phaseRemaining -= Time.fixedDeltaTime;
        if (phaseRemaining > 0f) return;
        if (phase == PatternPhase.Ready)
        {
            if (!IsGrounded()) return;
            phase = PatternPhase.Active;
            PlayAnimation(P2Roll);
            body.linearVelocity = new Vector2(pattern2Direction * pattern2DashSpeed, body.linearVelocity.y);
        }
        else if (phase == PatternPhase.Recovery) OnPattern2End();
    }

    public void NotifyPattern2Contact(Collider2D other)
    {
        if (!enabled || currentHP <= 0 || currentState != BossState.Pattern2 ||
            phase != PatternPhase.Active || other == null || other.tag != pattern2StopTag) return;
        // 자식의 넓은 센서가 먼저 벽에 닿아도 몸이 닿기 전에는 멈추지 않습니다.
        ColliderDistance2D distance = bossBodyCollider.Distance(other);
        if (!distance.isValid || distance.distance > 0.01f) return;
        StopHorizontal();
        phase = PatternPhase.Recovery;
        phaseRemaining = pattern2StopDuration;
        PlayAnimation(P2Stop, true);
        SetFacing(-pattern2Direction);
    }

    private void UpdatePattern3()
    {
        if (phase == PatternPhase.Ready)
        {
            StopHorizontal();
            if (!AnimationFinished(P3Ready) || !IsGrounded()) return;
            phase = PatternPhase.Active;
            float angle = pattern3LaunchAngle * Mathf.Deg2Rad;
            ApplyPattern3Impulse(new Vector2(facingDirection * Mathf.Cos(angle), Mathf.Sin(angle)));
            PlayAnimation(P3Jump);
            return;
        }
        if (phase == PatternPhase.Recovery)
        {
            StopHorizontal();
            phaseRemaining -= Time.fixedDeltaTime;
            if (phaseRemaining <= 0f) OnPattern3End();
            return;
        }
        if (phase != PatternPhase.Active) return;
        pattern3IncomingVelocity = body.linearVelocity;
        // 충돌 포즈가 이동 애니메이션에 즉시 덮이지 않게 합니다.
        if (Time.time < impactUntil) return;
        if (body.linearVelocity.y < -0.01f) PlayAnimation(P3Down);
        else if (body.linearVelocity.y > 0.01f)
            PlayAnimation(pattern3BounceCount == 0 ? P3Jump : P3Up);
    }

    private bool IsPattern3Surface(Collider2D other)
    {
        if (other == null || other.isTrigger) return false;
        string tagName = other.tag;
        return tagName == "Ground" || tagName == pattern2StopTag ||
               tagName == "BoosRoomDashFloor" || tagName == "BoseRoomDashFloor";
    }

    public void NotifyPattern3Collision(Collision2D collision)
    {
        if (!enabled || currentHP <= 0 || currentState != BossState.Pattern3 ||
            phase != PatternPhase.Active || !IsPattern3Surface(collision.collider) ||
            pattern3LastBounceTime == Time.fixedTime) return;
        Vector2 incoming = pattern3IncomingVelocity;
        if (incoming.sqrMagnitude < 0.001f) return;
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
        if (normal.sqrMagnitude < 0.5f) return;
        bool floor = normal.y > 0.5f && incoming.y < 0f;
        bool ceiling = normal.y < -0.5f && incoming.y > 0f;

        if (pattern3BounceCount >= pattern3BounceLimit)
        {
            if (floor)
            {
                body.linearVelocity = Vector2.zero;
                phase = PatternPhase.Recovery;
                phaseRemaining = pattern3ImpactDuration;
                PlayAnimation(P3Floor, true);
            }
            return;
        }
        Vector2 reflected = Vector2.Reflect(incoming.normalized, normal).normalized;
        float xSign = reflected.x < 0f ? -1f : reflected.x > 0f ? 1f : facingDirection;
        float ySign = reflected.y < 0f ? -1f : 1f;
        reflected = new Vector2(xSign * Mathf.Max(Mathf.Abs(reflected.x), pattern3MinAxis),
            ySign * Mathf.Max(Mathf.Abs(reflected.y), pattern3MinAxis)).normalized;
        if (Vector2.Dot(reflected, normal) <= 0f)
            reflected = Vector2.Reflect(incoming.normalized, normal).normalized;
        pattern3LastBounceTime = Time.fixedTime;
        pattern3BounceCount++;
        ApplyPattern3Impulse(reflected);
        if (floor || ceiling)
        {
            impactUntil = Time.time + pattern3ImpactDuration;
            PlayAnimation(floor ? P3Floor : P3Ceiling, true);
        }
        else if (Time.time >= impactUntil)
            PlayAnimation(reflected.y > 0f ? P3Up : P3Down);
    }

    private void ApplyPattern3Impulse(Vector2 direction)
    {
        body.linearVelocity = Vector2.zero;
        body.AddForce(direction.normalized * pattern3Impulse, ForceMode2D.Impulse);
        pattern3IncomingVelocity = direction.normalized * (pattern3Impulse / body.mass);
        if (Mathf.Abs(direction.x) > 0.001f) SetFacing(direction.x > 0f ? 1 : -1);
    }

    private void OnCollisionEnter2D(Collision2D collision) { ReportCollision(collision); }
    private void OnCollisionStay2D(Collision2D collision) { ReportCollision(collision); }
    private void ReportCollision(Collision2D collision)
    {
        NotifyPattern2Contact(collision.collider);
        NotifyPattern3Collision(collision);
    }
    private void OnTriggerEnter2D(Collider2D other) { NotifyPattern2Contact(other); }
    private void OnTriggerStay2D(Collider2D other) { NotifyPattern2Contact(other); }

    private void SetPatternCooldown(BossState state)
    {
        if (state == BossState.Pattern1) pattern1Cooldown = pattern1Interval;
        else if (state == BossState.Pattern2) pattern2Cooldown = pattern2Interval;
        else if (state == BossState.Pattern3) pattern3Cooldown = pattern3Interval;
    }

    // 기존 센서와의 호환용 공개 메서드. 조기 애니메이션 이벤트로 종료되지 않게 검사합니다.
    public void OnPattern1End()
    {
        if (phase == PatternPhase.Recovery && AnimationFinished(P1Land)) FinishPattern(BossState.Pattern1);
    }
    public void OnPattern2End()
    {
        if (phase == PatternPhase.Recovery && phaseRemaining <= 0f) FinishPattern(BossState.Pattern2);
    }
    public void OnPattern3End()
    {
        if (phase == PatternPhase.Recovery && phaseRemaining <= 0f) FinishPattern(BossState.Pattern3);
    }
    private void FinishPattern(BossState expected)
    {
        if (currentState != expected) return;
        if (currentHP <= 0) { ChangeState(BossState.Death); return; }
        SetPatternCooldown(expected);
        currentState = BossState.Walk;
        EnterState();
    }

    public void NotifyPlayerAttack(int damageValue) { Damaged(damageValue); }
    public void TakeDamage(int damageValue) { Damaged(damageValue); }
    public void Damaged(int damageValue)
    {
        if (!enabled || currentState == BossState.Death || damageValue <= 0) return;
        if (currentHP <= 0) { ChangeState(BossState.Death); return; }
        if (currentState == BossState.Waiting)
        {
            waitingHitCount++;
            if (waitingHitCount >= hitsRequiredToStart) ChangeState(BossState.Walk);
            return;
        }
        currentHP = Mathf.Max(0, currentHP - damageValue);
        if (currentHP <= 0) ChangeState(BossState.Death);
    }

    private void EnterDeath()
    {
        wasTouchingPlayer = false;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        foreach (Collider2D collider in body.GetComponentsInChildren<Collider2D>(true))
            if (collider.attachedRigidbody == body) collider.isTrigger = true;
        PlayAnimation(Dead, true);
        // 삭제되기 전에 보스방에 처치를 알립니다.
        Defeated?.Invoke();
    }

    private void RefreshPlayerTarget()
    {
        if (damageTarget != null && playerBodyCollider != null &&
            playerBodyCollider.isActiveAndEnabled &&
            playerBodyCollider.GetComponentInParent<PlayerController>() == damageTarget) return;
        damageTarget = FindFirstObjectByType<PlayerController>();
        wasTouchingPlayer = false;
        if (damageTarget == null) { playerBodyCollider = null; return; }
        if (playerBodyCollider != null && playerBodyCollider.isActiveAndEnabled &&
            playerBodyCollider.GetComponentInParent<PlayerController>() == damageTarget) return;
        playerBodyCollider = null;
        foreach (Collider2D candidate in damageTarget.GetComponentsInChildren<Collider2D>())
        {
            if (candidate.isActiveAndEnabled && !candidate.isTrigger)
            {
                playerBodyCollider = candidate;
                break;
            }
        }
    }

    private void UpdateContactDamage()
    {
        if (currentState == BossState.Waiting || currentState == BossState.Death ||
            playerBodyCollider == null || !playerBodyCollider.isActiveAndEnabled ||
            !bossBodyCollider.isActiveAndEnabled)
        {
            wasTouchingPlayer = false;
            return;
        }
        ColliderDistance2D distance = bossBodyCollider.Distance(playerBodyCollider);
        bool touching = distance.isValid && distance.distance <= 0f;
        if (touching && !wasTouchingPlayer && damageTarget != null && !damageTarget.IsDead)
        {
            int damage = currentState == BossState.Pattern1 ? 30 : contactDamage;
            if (damage > 0) damageTarget.TakeDamageFrom(damage, bossBodyCollider.bounds.center);
        }
        wasTouchingPlayer = touching;
    }

    private void OnDisable()
    {
        wasTouchingPlayer = false;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private bool HandleDebugKeys()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return false;
        if (keyboard.qKey.wasPressedThisFrame) DebugChangeState(BossState.Waiting);
        else if (keyboard.eKey.wasPressedThisFrame) DebugChangeState(BossState.Walk);
        else if (keyboard.rKey.wasPressedThisFrame) DebugChangeState(BossState.Pattern1);
        else if (keyboard.aKey.wasPressedThisFrame) DebugChangeState(BossState.Pattern2);
        else if (keyboard.sKey.wasPressedThisFrame) DebugChangeState(BossState.Pattern3);
        else if (keyboard.dKey.wasPressedThisFrame) DebugChangeState(BossState.Death);
        else return false;
        return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Q)) DebugChangeState(BossState.Waiting);
        else if (Input.GetKeyDown(KeyCode.E)) DebugChangeState(BossState.Walk);
        else if (Input.GetKeyDown(KeyCode.R)) DebugChangeState(BossState.Pattern1);
        else if (Input.GetKeyDown(KeyCode.A)) DebugChangeState(BossState.Pattern2);
        else if (Input.GetKeyDown(KeyCode.S)) DebugChangeState(BossState.Pattern3);
        else if (Input.GetKeyDown(KeyCode.D)) DebugChangeState(BossState.Death);
        else return false;
        return true;
#else
        return false;
#endif
    }

    private void DebugChangeState(BossState nextState)
    {
        if (currentState == BossState.Death || currentState == nextState) return;
        if (hasStartedCombat && nextState == BossState.Waiting) return;
        if (currentHP <= 0) nextState = BossState.Death;
        if (IsPattern(currentState)) SetPatternCooldown(currentState);
        body.linearVelocity = Vector2.zero;
        if (nextState == BossState.Waiting) waitingHitCount = 0;
        currentState = nextState;
        EnterState();
    }
}
