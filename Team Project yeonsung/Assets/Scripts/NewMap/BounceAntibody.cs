using System.Collections;
using UnityEngine;

// C 튕김 항체. 위에서 C(낙하공격)로 내려찍으면 bounceSpeed로 위로 튕겨 오르고, 더블점프도 다시 충전됩니다.
// 그냥 밟거나 몸으로 위에서 닿으면 터져서 아래로 떨어지고, respawnTime초 뒤 다시 생깁니다.
// 옆이나 아래에서 부딪히는 건 무시. 그림은 SpriteRenderer의 Sprite만 바꾸면 됩니다. (지금은 네모 박스)
// 큰 항체(4 보스 전) = 27, 작은 항체(중후반) = 13.
[RequireComponent(typeof(BoxCollider2D))]
public class BounceAntibody : MonoBehaviour
{
    public float bounceSpeed = 13f;
    public float respawnTime = 2f;
    [SerializeField] float waitLandingMax = 0.5f;   // 낙하공격이 끝날 때까지 기다리는 최대 시간 (착지 멈춤은 바로 끝내므로 보통 0.05초 안)
    [SerializeField] bool showDebugLog = false;         // 켜면 Console에 닿을 때·튕길 때 상태를 찍음 (문제 찾을 때만)

    [Header("그림 (비워 두면 이 오브젝트의 네모 그림을 씀)")]
    [Tooltip("실제로 보이는 그림 (자식). 충돌 크기와 따로 크기를 맞추기 위해 자식에 둠")]
    public SpriteRenderer visual;
    [Tooltip("C로 내려찍어 튕기는 순간 잠깐 보여 줄 눌린 그림")]
    public Sprite pressedSprite;
    public float pressedTime = 0.18f;
    [Tooltip("밟고 뛰는 애니메이션 그림 [0, 1, 2]. 있으면 튕길 때 0 → 1 → 2 → 0 순서로 재생 (눌린 그림 대신)")]
    public Sprite[] bounceFrames;
    [Tooltip("애니메이션 한 장 시간 (초)")]
    public float frameTime = 0.08f;

    BoxCollider2D col;
    SpriteRenderer sprite;
    Sprite normalSprite;
    Color baseColor;
    bool popped;
    bool bouncing;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        sprite = visual != null ? visual : GetComponent<SpriteRenderer>();
        if (sprite != null) normalSprite = sprite.sprite;
        if (sprite != null) baseColor = sprite.color;
        if (showDebugLog)
            Debug.Log("[튕김 항체 진단] 시작: " + name + " 힘=" + bounceSpeed + " 콜라이더=" + col.enabled
                + " 트리거=" + col.isTrigger + " 크기=" + col.bounds.size.ToString("F2") + " 윗면 y=" + col.bounds.max.y.ToString("F2")
                + " 태그=" + tag + " 레이어=" + LayerMask.LayerToName(gameObject.layer));
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
        PlayerController player = PlayerStateReader.FindPlayer(collision.collider);
        if (showDebugLog)
        {
            string normals = "";
            foreach (ContactPoint2D c in collision.contacts) normals += c.normal.ToString("F2") + " ";
            Debug.Log("[튕김 항체 진단] " + name + " 닿음: " + collision.collider.name
                + " / 플레이어=" + (player != null) + " / popped=" + popped + " bouncing=" + bouncing
                + " / 상대 바닥 y=" + collision.collider.bounds.min.ToString("F2") + " 내 윗면 y=" + col.bounds.max.y.ToString("F2")
                + " / normal=" + normals
                + " / 위에서=" + (player != null && PlayerStateReader.IsFromTop(collision, col))
                + " / 내려찍기=" + (player != null && PlayerStateReader.IsDiving(player))
                + " 특수공격중=" + (player != null && PlayerStateReader.IsSpecialAttacking(player)));
        }
        if (popped || bouncing) return;
        if (player == null || !PlayerStateReader.IsFromTop(collision, col)) return;

        if (PlayerStateReader.IsDiving(player)) StartCoroutine(BounceRoutine(player));
        else StartCoroutine(PopRoutine());
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (showDebugLog && PlayerStateReader.FindPlayer(collision.collider) != null)
            Debug.Log("[튕김 항체 진단] " + name + " 떨어짐 t=" + Time.time.ToString("F2") + " " + PlayerStateReader.DebugState(PlayerStateReader.FindPlayer(collision.collider)));
    }

    IEnumerator BounceRoutine(PlayerController player)
    {
        bouncing = true;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        // C 낙하공격은 착지 후 멈춰 있는 시간(프리팹 fallingHitboxLingerTime = 1초)이 있고, 그동안 속도를 매 프레임 0으로 만듦.
        // → 항체 위에서는 그 멈춤을 바로 끝내고, 낙하공격이 정상 종료된 직후에 튕겨야 속도가 덮어써지지 않음.
        float waited = 0f;
        while (player != null && PlayerStateReader.IsSpecialAttacking(player) && waited < waitLandingMax)
        {
            PlayerStateReader.EndSpecialLandingNow(player);
            yield return new WaitForFixedUpdate();
            waited += Time.fixedDeltaTime;
            if (showDebugLog) Debug.Log("[튕김 항체 진단] 착지 대기 " + waited.ToString("F2") + " " + PlayerStateReader.DebugState(player)
                + " 속도=" + rb.linearVelocity.ToString("F2"));
        }

        if (player != null && rb != null)
        {
            if (showDebugLog) Debug.Log("[튕김 항체 진단] 튕김 시작 t=" + Time.time.ToString("F2") + " 기다린 시간=" + waited.ToString("F2")
                + " 특수공격중=" + PlayerStateReader.IsSpecialAttacking(player) + " 속도(전)=" + rb.linearVelocity.ToString("F2")
                + " 중력=" + rb.gravityScale);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceSpeed);
            if (DoubleJumpAbility.Instance != null) DoubleJumpAbility.Instance.RefreshAirJump();
            if (sprite != null)
            {
                if (bounceFrames != null && bounceFrames.Length >= 3) StartCoroutine(PlayBounceFrames());   // 0 → 1 → 2 → 0
                else if (pressedSprite != null) sprite.sprite = pressedSprite;   // 눌린 그림
                else sprite.color = Color.white;                                 // 그림이 없으면 하얗게 번쩍
            }
            if (showDebugLog)
            {
                for (int i = 1; i <= 3; i++)
                {
                    yield return new WaitForFixedUpdate();
                    Debug.Log("[튕김 항체 진단] 튕김 후 " + i + "프레임: 속도=" + rb.linearVelocity.ToString("F2")
                        + " y=" + rb.position.y.ToString("F2") + " 특수공격중=" + PlayerStateReader.IsSpecialAttacking(player));
                }
            }
        }

        bool animating = bounceFrames != null && bounceFrames.Length >= 3;
        yield return new WaitForSeconds(animating ? frameTime * 4f : (pressedSprite != null ? pressedTime : 0.15f));
        if (sprite != null) { sprite.color = baseColor; sprite.sprite = normalSprite; }
        bouncing = false;
    }

    // 밟고 뛰는 애니메이션: 0 → 1 → 2 → 0
    IEnumerator PlayBounceFrames()
    {
        int[] order = { 0, 1, 2, 0 };
        foreach (int f in order)
        {
            if (sprite == null) yield break;
            sprite.sprite = bounceFrames[f];
            yield return new WaitForSeconds(frameTime);
        }
    }

    IEnumerator PopRoutine()
    {
        popped = true;
        col.enabled = false;
        SetAlpha(0.15f);
        yield return new WaitForSeconds(respawnTime);
        while (IsOccupied()) yield return new WaitForSeconds(0.2f);
        RestoreNow();
    }

    public void RestoreNow()
    {
        StopAllCoroutines();
        popped = bouncing = false;
        col.enabled = true;
        SetAlpha(1f);
    }

    bool IsOccupied()
    {
        Vector2 center = transform.TransformPoint(col.offset);
        Vector2 size = Vector2.Scale(col.size, new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
        foreach (Collider2D c in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (c != col && !c.isTrigger && PlayerStateReader.FindPlayer(c) != null) return true;
        }
        return false;
    }

    void SetAlpha(float a)
    {
        if (sprite == null) return;
        Color c = baseColor;
        c.a = baseColor.a * a;
        sprite.color = c;
    }
}
