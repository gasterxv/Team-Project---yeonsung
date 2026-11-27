using System.Collections;
using UnityEngine;

// 부서지는 발판. 플레이어나 보스가 발판 **위에 올라서면** breakDelay초 뒤 부서지고, restoreDelay초 뒤 다시 생깁니다.
// 옆이나 아래에서 부딪히는 건 무시합니다.
// 다시 생길 자리에 플레이어나 보스가 겹쳐 있으면 비킬 때까지 기다립니다.
// 플레이어가 죽었다가 부활하면 바로 원래대로 돌아옵니다.
[RequireComponent(typeof(BoxCollider2D))]
public class BreakablePlatform : MonoBehaviour
{
    public float breakDelay = 1f;
    public float restoreDelay = 20f;
    [SerializeField] private float blinkInterval = 0.1f;   // 부서지기 전 깜빡임 간격
    [SerializeField] private float topTolerance = 0.1f;    // 발 위치가 발판 윗면에서 이만큼 아래까지는 '위에 올라섬'으로 봄

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

    void OnEnable()
    {
        GameFlowManager.OnPlayerRespawn += RestoreNow;
    }

    void OnDisable()
    {
        GameFlowManager.OnPlayerRespawn -= RestoreNow;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryBreak(collision);
    }

    // 보스가 이미 올라와 있는 상태 등 Enter를 놓친 경우 대비
    void OnCollisionStay2D(Collision2D collision)
    {
        TryBreak(collision);
    }

    void TryBreak(Collision2D collision)
    {
        if (busy || !IsPlayerOrBoss(collision.collider)) return;
        if (!IsStandingOnTop(collision)) return;
        StartCoroutine(BreakRoutine());
    }

    // 위에서 밟았는지: 부딪힌 면이 발판 윗면이고, 상대의 발이 윗면 근처 이상에 있을 때만
    bool IsStandingOnTop(Collision2D collision)
    {
        float top = box.bounds.max.y;
        if (collision.collider.bounds.min.y < top - topTolerance) return false;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) return true;   // MovingPlatform과 같은 기준 (위에서 누름)
        }
        return false;
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
        // 부서진 동안엔 콜라이더가 꺼져 있어 box.bounds가 0이 되므로, 위치·크기를 직접 계산합니다.
        Vector2 center = transform.TransformPoint(box.offset);
        Vector2 size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
        foreach (Collider2D c in Physics2D.OverlapBoxAll(center, size, 0f))
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
