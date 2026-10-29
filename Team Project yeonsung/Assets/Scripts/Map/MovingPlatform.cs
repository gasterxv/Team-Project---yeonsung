using System.Collections.Generic;
using UnityEngine;

// 기획서의 B: 좌우(또는 상하) 왕복형 플랫폼 (적혈구)
// 위에 올라탄 플레이어는 플랫폼과 같이 움직입니다.
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("왕복 설정")]
    public Vector2 moveOffset = new Vector2(3f, 0f);   // 시작 위치에서 얼마나 움직일지
    public float cycleTime = 3f;                        // 한 번 갔다 오는 시간(초)

    private Rigidbody2D rb;
    private Vector2 startPos;
    private Vector2 lastPos;
    private readonly HashSet<Rigidbody2D> riders = new HashSet<Rigidbody2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        startPos = rb.position;
        lastPos = startPos;
    }

    void FixedUpdate()
    {
        // 0 -> 1 -> 0 으로 부드럽게 왕복
        float t = (1f - Mathf.Cos(Time.time / Mathf.Max(0.1f, cycleTime) * Mathf.PI * 2f)) * 0.5f;
        Vector2 next = startPos + moveOffset * t;
        rb.MovePosition(next);

        // 위에 탄 플레이어도 플랫폼이 움직인 만큼 같이 옮겨줍니다.
        Vector2 delta = next - lastPos;
        riders.RemoveWhere(r => r == null);
        foreach (Rigidbody2D rider in riders)
        {
            rider.position += delta;
        }
        lastPos = next;
    }

    // 위에서 밟았을 때만 같이 움직이는 대상으로 등록합니다.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.rigidbody == null || collision.gameObject.GetComponent<PlayerController>() == null)
        {
            return;
        }

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                riders.Add(collision.rigidbody);
                break;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.rigidbody != null)
        {
            riders.Remove(collision.rigidbody);
        }
    }

    void OnDrawGizmos()
    {
        Vector3 from = Application.isPlaying ? (Vector3)startPos : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, from + (Vector3)moveOffset);
    }
}
