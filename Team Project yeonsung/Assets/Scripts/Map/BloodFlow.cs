using System.Collections.Generic;
using UnityEngine;

// 혈관 아래쪽을 빠르게 흐르는 혈류
// 닿으면 무적 여부와 상관없이 플레이어가 즉사합니다.
[RequireComponent(typeof(BoxCollider2D))]
public class BloodFlow : MonoBehaviour
{
    [Header("흐름 설정")]
    public float flowSpeed = 3f;            // 줄무늬가 흐르는 속도 (+ 오른쪽, - 왼쪽)

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
        Kill(other);
    }

    // 혈류 안에서 부활하는 등 Enter를 놓친 경우를 대비합니다.
    void OnTriggerStay2D(Collider2D other)
    {
        Kill(other);
    }

    void Kill(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead)
        {
            return;
        }

        player.KillInstantly();
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
