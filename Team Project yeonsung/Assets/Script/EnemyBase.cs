using UnityEngine;

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

    [SerializeField] protected Animator animator;

    [SerializeField] protected int HP = 100;

    // 공격 간격 자체가 아닌 '남은 공격 쿨타임'
    [SerializeField] protected float MonsterAttackCooldown = 0f;

    [SerializeField] private EnemySensor recognitionSensor;
    [SerializeField] private EnemySensor attackRangeSensor;

    protected MonsterState currentState = MonsterState.Idle;

    // 몬스터 매니저 등에서 현재 상태 확인 가능
    public MonsterState CurrentState => currentState;

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        // 현재 조건에 맞춰 상태를 먼저 결정
        UpdateState();

        // 결정된 상태의 행동 실행
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
                GetDamage();
                break;
        }
    }

    protected virtual void UpdateState()
    {
        // 사망 상태에서는 변경 불가
        if (currentState == MonsterState.Dead)
            return;

        if (HP <= 0)
        {
            ChangeState(MonsterState.Dead);
            return;
        }

        // 공격 범위 안이라면 쿨타임에 따라 상태 결정
        if (attackRangeSensor.HasPlayer)
        {
            if (MonsterAttackCooldown <= 0f)
            {
                ChangeState(MonsterState.Attack);
            }
            else
            {
                ChangeState(MonsterState.Idle);
            }
        }
        // 공격 범위 밖이지만 인식 범위 안이라면 이동
        else if (recognitionSensor.HasPlayer)
        {
            ChangeState(MonsterState.Walk);
        }

        // 두 범위 모두 밖이면 여기서는 상태를 바꾸지 않음.
        // 몬스터 매니저에서 처리.
    }

    public void ChangeState(MonsterState nextState)
    {
        if (currentState == MonsterState.Dead)
            return;

        // HP가 0 이하면 요청한 상태보다 사망을 우선
        if (HP <= 0)
            nextState = MonsterState.Dead;

        if (currentState == nextState)
            return;

        currentState = nextState;

        // 상태가 바뀐 순간 한 번 실행
        OnStateChanged(nextState);
    }

    protected virtual void OnStateChanged(MonsterState newState)
    {
        // 애니메이션 시작 등 상태 변경 순간의 처리가 필요하면
        // 자식 클래스에서 override하여 구현
    }

    // 피격용 센서가 플레이어 공격을 감지했을 때 호출
    public void NotifyPlayerAttack()
    {
        if (currentState == MonsterState.Dead)
            return;

        ChangeState(
            HP > 0
                ? MonsterState.GetDamage
                : MonsterState.Dead
        );
    }

    protected virtual void Death()
    {
        gameObject.SetActive(false);
    }

    protected virtual void GetDamage()
    {
    }

    protected virtual void Attack()
    {
    }

    protected virtual void Walk()
    {
    }

    protected virtual void Idle()
    {
    }
}