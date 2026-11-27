using UnityEngine;

// 아래층으로 가는 구멍을 막는 백혈구 바닥. 위에서 C(낙하공격)로 내려찍어야만 부서집니다.
// 그냥 서 있거나 X로 때리면 안 부서짐. 깬 상태는 체크포인트에 저장되고, 저장 전에 죽으면 다시 생깁니다.
[RequireComponent(typeof(BoxCollider2D))]
public class WhiteCellFloor : ResettableBehaviour
{
    BoxCollider2D col;
    SpriteRenderer sprite;
    bool broken;

    protected override void Awake()
    {
        base.Awake();
        col = GetComponent<BoxCollider2D>();
        sprite = GetComponent<SpriteRenderer>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (broken) return;
        PlayerController player = PlayerStateReader.FindPlayer(collision.collider);
        if (player == null || !PlayerStateReader.IsDiving(player)) return;
        if (!PlayerStateReader.IsFromTop(collision, col)) return;
        Bounds hole = col.bounds;
        SetBroken(true);
        DivePassThrough.Slam(player, hole);   // 구멍 안으로 몸을 맞추고, 멈추지 않고 가장 아래 바닥까지 계속 내려찍음
    }

    // 낙하공격 히트박스가 먼저 닿는 경우도 처리
    void OnTriggerEnter2D(Collider2D other)
    {
        if (broken) return;
        PlayerHitbox hitbox = other.GetComponentInParent<PlayerHitbox>();
        if (hitbox != null && PlayerStateReader.IsFallingHitbox(hitbox))
        {
            Bounds hole = col.bounds;
            SetBroken(true);
            // 낙하 히트박스는 플레이어 자식이 아님(월드에 생성) → 플레이어를 직접 찾음
            PlayerController p = hitbox.GetComponentInParent<PlayerController>();
            if (p == null) p = FindFirstObjectByType<PlayerController>();
            DivePassThrough.Slam(p, hole);
        }
    }

    void SetBroken(bool value)
    {
        broken = value;
        col.enabled = !value;
        if (sprite != null) sprite.enabled = !value;
    }

    protected override object CaptureState() { return broken; }
    protected override void ApplyState(object state) { SetBroken((bool)state); }
}
