using System.Collections;
using UnityEngine;

// 보스방 발판. 플레이어나 보스가 닿으면 breakDelay초 뒤 부서지고, restoreDelay초 뒤 다시 생깁니다.
// 다시 생길 자리에 플레이어나 보스가 겹쳐 있으면 비킬 때까지 기다립니다.
[RequireComponent(typeof(BoxCollider2D))]
public class BreakablePlatform : MonoBehaviour
{
    public float breakDelay = 1f;
    public float restoreDelay = 20f;
    [SerializeField] private float blinkInterval = 0.1f;   // 부서지기 전 깜빡임 간격

    private BoxCollider2D box;
    private SpriteRenderer sprite;
    private Color originalColor;
    private bool busy;   // 부서지는 중이거나 부서져 있는 동안 true

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) originalColor = sprite.color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryBreak(collision.collider);
    }

    // 보스가 이미 올라와 있는 상태 등 Enter를 놓친 경우 대비
    void OnCollisionStay2D(Collision2D collision)
    {
        TryBreak(collision.collider);
    }

    void TryBreak(Collider2D other)
    {
        if (busy || !IsPlayerOrBoss(other)) return;
        StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        busy = true;

        // 1) 부서지기 전 깜빡임
        float end = Time.time + breakDelay;
        bool dim = false;
        while (Time.time < end)
        {
            dim = !dim;
            SetAlpha(dim ? 0.4f : 1f);
            yield return new WaitForSeconds(Mathf.Max(0.02f, blinkInterval));
        }

        // 2) 부서짐
        SetVisible(false);

        // 3) 복구 대기 후, 자리에 아무도 없을 때 복구
        yield return new WaitForSeconds(restoreDelay);
        while (IsOccupied())
        {
            yield return new WaitForSeconds(0.2f);
        }

        RestoreNow();
    }

    // 즉시 원래대로 (부활할 때 BossRoomController가 호출)
    public void RestoreNow()
    {
        StopAllCoroutines();
        SetVisible(true);
        busy = false;
    }

    bool IsOccupied()
    {
        Bounds b = box.bounds;
        foreach (Collider2D c in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
        {
            if (c != box && !c.isTrigger && IsPlayerOrBoss(c)) return true;
        }
        return false;
    }

    static bool IsPlayerOrBoss(Collider2D c)
    {
        if (c == null) return false;
        return c.GetComponentInParent<PlayerController>() != null
            || c.GetComponentInParent<BossController>() != null;
    }

    void SetVisible(bool visible)
    {
        box.enabled = visible;
        if (sprite != null)
        {
            sprite.enabled = visible;
            sprite.color = originalColor;
        }
    }

    void SetAlpha(float a)
    {
        if (sprite == null) return;
        Color c = originalColor;
        c.a = originalColor.a * a;
        sprite.color = c;
    }
}
