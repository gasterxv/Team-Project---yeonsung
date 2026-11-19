using System.Collections.Generic;
using UnityEngine;

// 혈관 아래쪽을 빠르게 흐르는 혈류
// 닿으면 대미지를 주고 위로 튕겨내며, 흐르는 방향으로 플레이어를 밀어냅니다.
[RequireComponent(typeof(BoxCollider2D))]
public class BloodFlow : MonoBehaviour
{
    [Header("대미지 설정")]
    public int damage = 1;                  // 닿았을 때 대미지
    public float bounceVelocity = 9f;      // 닿으면 위로 튕겨내는 속도 (0이면 튕기지 않음)

    [Header("흐름 설정")]
    public float flowSpeed = 3f;            // 흐르는 속도 (+ 오른쪽, - 왼쪽)
    public float pushStrength = 0.5f;       // 플레이어를 흐름 방향으로 미는 비율 (0이면 밀지 않음)

    [Header("흐름 표시 (보기용)")]
    public int streakCount = 12;            // 흐름을 보여주는 줄무늬 개수
    public Color streakColor = new Color(1f, 0.45f, 0.45f, 0.6f);

    private BoxCollider2D area;
    private readonly List<Transform> streaks = new List<Transform>();

    void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        CreateStreaks();
    }

    void Update()
    {
        // 줄무늬를 흐르는 방향으로 움직이고, 끝에 닿으면 반대편으로 되돌립니다.
        Bounds b = area.bounds;
        foreach (Transform s in streaks)
        {
            Vector3 p = s.position;
            p.x += flowSpeed * Time.deltaTime;
            if (p.x > b.max.x) p.x = b.min.x;
            if (p.x < b.min.x) p.x = b.max.x;
            s.position = p;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead)
        {
            return;
        }

        // 처음 닿으면 무적 중이어도 튕겨내서 혈류 밖으로 빠져나올 수 있게 합니다.
        player.TakeDamage(damage);
        Bounce(player);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead)
        {
            return;
        }

        // 혈류 안에 계속 있으면 무적이 끝날 때마다 다시 대미지 + 튕김
        if (!player.IsInvincible)
        {
            player.TakeDamage(damage);
            Bounce(player);
        }

        // PlayerController가 매 프레임 가로 속도를 덮어쓰기 때문에 위치를 직접 밀어줍니다.
        player.transform.position += Vector3.right * (flowSpeed * pushStrength * Time.deltaTime);
    }

    void Bounce(PlayerController player)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null && bounceVelocity > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceVelocity);
        }
    }

    void CreateStreaks()
    {
        SpriteRenderer body = GetComponent<SpriteRenderer>();
        if (body == null || streakCount <= 0)
        {
            return;
        }

        Bounds b = area.bounds;
        for (int i = 0; i < streakCount; i++)
        {
            GameObject go = new GameObject("Streak");
            go.transform.SetParent(transform, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body.sprite;
            sr.color = streakColor;
            sr.sortingOrder = body.sortingOrder + 1;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(Random.Range(0.8f, 2f), 0.08f);

            go.transform.position = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y + 0.15f, b.max.y - 0.15f),
                transform.position.z);
            streaks.Add(go.transform);
        }
    }

    // Scene 화면에서 혈류 범위를 표시합니다.
    void OnDrawGizmos()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
