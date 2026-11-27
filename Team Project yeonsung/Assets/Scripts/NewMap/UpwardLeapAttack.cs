using System.Reflection;
using UnityEngine;

// 간염(몸통박치기 몬스터, JabMob2)이 위쪽 발판에 있는 플레이어에게도 뛰어올라 공격하게 합니다.
// 팀원 JabMob2는 고치지 않고, 같은 오브젝트에 붙어서
//  1) 플레이어가 위에 있으면 공격을 시작시키고 (원래는 높이가 겹칠 때만 공격)
//  2) 뛰어오르는 순간 위로 뛰는 힘을 높이만큼 키웁니다.
[DefaultExecutionOrder(50)]   // 몬스터 스크립트가 움직인 뒤에 덧씌움
public class UpwardLeapAttack : MonoBehaviour
{
    [Tooltip("이 높이(발 기준)까지 위에 있는 플레이어에게 뛰어오름")]
    public float maxHeight = 5f;
    [Tooltip("옆으로 이 거리 안에 있을 때만")]
    public float maxHorizontal = 5f;
    [Tooltip("목표 높이보다 더 뛰는 여유")]
    public float extraHeight = 1f;
    [Tooltip("옆으로 가장 빠른 속도 (원래 점프 옆속도와 같게)")]
    public float maxSideSpeed = 5.5f;

    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo AimField = typeof(JabMob2).GetField("attackAimCollider", F);
    static readonly FieldInfo LaunchedField = typeof(JabMob2).GetField("launched", F);
    static readonly FieldInfo BodyField = typeof(JabMob2).GetField("attackBodyCollider", F);
    static readonly FieldInfo CooldownField = typeof(EnemyBase).GetField("MonsterAttackCooldown", F);
    static readonly FieldInfo RecognitionField = typeof(EnemyBase).GetField("recognitionSensor", F);

    JabMob2 mob;
    Rigidbody2D body;
    Collider2D bodyCollider;
    EnemySensor recognition;
    bool leaping, applied;
    float leapVy, sideSpeed;
    int leapDir;

    void Awake()
    {
        mob = GetComponent<JabMob2>();
        body = GetComponent<Rigidbody2D>();
        if (mob != null)
        {
            bodyCollider = BodyField != null ? BodyField.GetValue(mob) as Collider2D : null;
            recognition = RecognitionField != null ? RecognitionField.GetValue(mob) as EnemySensor : null;
        }
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
    }

    void OnDisable() { leaping = applied = false; }

    float Gravity { get { return Mathf.Abs(Physics2D.gravity.y * (body != null ? body.gravityScale : 1f)); } }

    void Update()
    {
        if (mob == null || body == null || bodyCollider == null || mob.IsDead || Time.timeScale == 0f) return;
        if (leaping && mob.CurrentState != EnemyBase.MonsterState.Attack) leaping = applied = false;
        if (leaping) return;

        EnemyBase.MonsterState s = mob.CurrentState;
        if (s != EnemyBase.MonsterState.Idle && s != EnemyBase.MonsterState.Walk) return;
        if (CooldownField != null && (float)CooldownField.GetValue(mob) > 0f) return;
        if (Mathf.Abs(body.linearVelocity.y) > 0.2f) return;   // 땅에 서 있을 때만

        Collider2D target = recognition != null ? recognition.PlayerCollider : null;
        if (target == null) return;
        Bounds own = bodyCollider.bounds, t = target.bounds;
        if (t.min.y <= own.max.y) return;                       // 높이가 겹치면 원래 공격이 처리
        float height = t.min.y - own.min.y;
        if (height > maxHeight) return;
        float dx = t.center.x - own.center.x;
        float gap = Mathf.Abs(dx) - own.extents.x - t.extents.x;
        if (gap > maxHorizontal) return;
        if (Blocked(own, t)) return;

        if (AimField != null) AimField.SetValue(mob, target);
        mob.ChangeState(EnemyBase.MonsterState.Attack);
        if (mob.CurrentState != EnemyBase.MonsterState.Attack) return;

        leaping = true;
        applied = false;
        leapVy = Mathf.Sqrt(2f * Gravity * (height + extraHeight));
        float rise = leapVy / Gravity;                          // 꼭대기까지 걸리는 시간
        leapDir = Mathf.Abs(dx) > 0.01f ? (dx > 0f ? 1 : -1) : 1;
        // 꼭대기쯤에서 플레이어 위치에 닿게 옆속도를 맞춤
        sideSpeed = Mathf.Clamp(Mathf.Abs(dx) / Mathf.Max(0.2f, rise), 1.5f, maxSideSpeed);
    }

    // 뛰는 길이 막혀 있으면 안 뜀: ① 머리 위(바로 위 발판 아래 등) ② 꼭대기 높이에서 플레이어 쪽으로
    bool Blocked(Bounds own, Bounds t)
    {
        float peakBottom = t.min.y + extraHeight * 0.5f;
        Vector2 head = new Vector2(own.center.x, own.max.y + 0.05f);
        Vector2 top = new Vector2(own.center.x, peakBottom + own.size.y);
        return Hits(head, top) || Hits(new Vector2(own.center.x, peakBottom), new Vector2(t.center.x, peakBottom));
    }

    bool Hits(Vector2 from, Vector2 to)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to))
        {
            Collider2D c = hit.collider;
            if (c == null || c.isTrigger || c.attachedRigidbody == body) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            if (c.CompareTag("Ground")) return true;
        }
        return false;
    }

    void FixedUpdate()
    {
        if (!leaping || mob == null || body == null) return;
        if (mob.CurrentState != EnemyBase.MonsterState.Attack || mob.IsDead) { leaping = applied = false; return; }
        bool launched = LaunchedField != null && (bool)LaunchedField.GetValue(mob);
        if (!launched) return;
        float vy = body.linearVelocity.y;
        if (!applied) { vy = leapVy; applied = true; }
        body.linearVelocity = new Vector2(leapDir * sideSpeed, vy);
    }
}
