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

    // 플레이어의 Damaged(int)를 연결할 곳
    [SerializeField]
    private UnityEvent<int> onPlayerContactDamage =
        new UnityEvent<int>();

    private bool wasTouchingPlayer;
    private int facingDirection = 1;

    public BossState CurrentState => currentState;
    public int CurrentHP => currentHP;
    public int FacingDirection => facingDirection;

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

        UpdateCooldowns();
        UpdateFacing();

        // 일반 전투 판단은 Walk 상태에서만 실행.
        // 대기·포효·패턴 상태는 종료 조건을 기다림.
        if (currentState == BossState.Walk)
        {
            SelectNextAction();
        }

        switch (currentState)
        {
            case BossState.Walk:
                Walk();
                break;

            case BossState.Pattern1:
                UpdatePattern1();
                break;

            case BossState.Pattern2:
                UpdatePattern2();
                break;

            case BossState.Pattern3:
                UpdatePattern3();
                break;
        }
    }

    private void FixedUpdate()
    {
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
            onPlayerContactDamage.Invoke(contactDamage);
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
        if (playerBodyCollider == null)
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
                // Pattern1 시작
                break;

            case BossState.Pattern2:
                // Pattern2 시작
                break;

            case BossState.Pattern3:
                // Pattern3 시작
                break;

            case BossState.Death:
                // 진행 중인 공격 판정·이동 등을 정리
                // 사망 애니메이션 시작
                break;
        }
    }

    // 포효 애니메이션 또는 포효 연출이 끝났을 때 호출
    public void OnRoarEnd()
    {
        if (currentState != BossState.Roar)
            return;

        ChangeState(BossState.Walk);
        SelectNextAction();
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

        // 일반 전환 잠금을 우회하는 정상 종료 경로
        currentState = BossState.Walk;

        // 종료 직후 다음 행동을 선택
        SelectNextAction();

        if (currentState == BossState.Walk)
        {
            OnStateEntered(BossState.Walk);
        }
    }

    private void Walk()
    {
        // 실제 걷기 이동은 추후 구현
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
}