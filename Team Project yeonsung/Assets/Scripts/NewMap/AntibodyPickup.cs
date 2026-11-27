using UnityEngine;

// 중후반 항체 칸의 항체. 먹으면 더블점프가 켜집니다.
// 먹은 상태는 체크포인트에 저장되고, 저장 전에 죽으면 다시 나타나고 더블점프도 꺼집니다.
[RequireComponent(typeof(Collider2D))]
public class AntibodyPickup : ResettableBehaviour
{
    Collider2D col;
    SpriteRenderer[] sprites;
    bool collected;

    protected override void Awake()
    {
        base.Awake();
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || PlayerStateReader.FindPlayer(other) == null) return;
        if (DoubleJumpAbility.Instance == null)
        {
            Debug.LogWarning("[새맵] 씬에 DoubleJumpAbility가 없어요. 새맵 회색 상자 메뉴를 다시 실행해 주세요.");
            return;
        }
        DoubleJumpAbility.Instance.Unlock();
        SetCollected(true);
    }

    void SetCollected(bool value)
    {
        collected = value;
        col.enabled = !value;
        foreach (SpriteRenderer s in sprites) s.enabled = !value;
    }

    protected override object CaptureState() { return collected; }
    protected override void ApplyState(object state) { SetCollected((bool)state); }
}
