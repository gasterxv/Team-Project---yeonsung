using System.Collections;
using UnityEngine;

// 방 사이 통로를 막는 백혈구 벽. X(일반공격)로만 부서집니다. C(낙하공격)는 안 통함.
// 깬 상태는 체크포인트에 저장되고, 저장 전에 죽으면 다시 생깁니다.
[RequireComponent(typeof(BoxCollider2D))]
public class WhiteCellWall : ResettableBehaviour
{
    public int hitsToBreak = 1;

    BoxCollider2D col;
    SpriteRenderer sprite;
    Color baseColor;
    int hits;
    bool broken;

    protected override void Awake()
    {
        base.Awake();
        col = GetComponent<BoxCollider2D>();
        sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) baseColor = sprite.color;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (broken) return;
        PlayerHitbox hitbox = other.GetComponentInParent<PlayerHitbox>();
        if (hitbox == null || PlayerStateReader.IsFallingHitbox(hitbox)) return;

        hits++;
        if (hits >= hitsToBreak) SetBroken(true);
        else StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        if (sprite == null) yield break;
        sprite.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        sprite.color = baseColor;
    }

    void SetBroken(bool value)
    {
        broken = value;
        hits = 0;
        col.enabled = !value;
        if (sprite != null)
        {
            sprite.enabled = !value;
            sprite.color = baseColor;
        }
    }

    protected override object CaptureState() { return broken; }
    protected override void ApplyState(object state) { SetBroken((bool)state); }
}
