using System.Collections.Generic;
using UnityEngine;

// C(낙하공격)로 내려찍는 동안에는 얇은 발판을 뚫고 지나가서 가장 아래 바닥에 바로 박힘.
// - 통과: 고정 발판, 부서지는 발판 (이름에 '발판'이 들어가고 두께 1 미만, 새맵_회색상자 안)
// - 통과 안 함: 움직이는 혈소판(밑이 혈류라 위험), C 튕김 항체, C 백혈구 바닥, 두꺼운 땅
// PlayerController는 고치지 않음: 내려찍는 동안만 플레이어와 발판 사이 충돌을 꺼 두고, 끝나면 다시 켬.
public class DivePassThrough : MonoBehaviour
{
    [Tooltip("이 두께(세로) 미만인 블록만 통과")]
    public float maxThickness = 1f;

    readonly List<Collider2D> platforms = new List<Collider2D>();
    PlayerController player;
    Collider2D[] playerBodies = new Collider2D[0];
    bool ignoring;

    void Start()
    {
        CollectPlatforms();
    }

    void CollectPlatforms()
    {
        platforms.Clear();
        GameObject root = GameObject.Find("새맵_회색상자");
        if (root == null) return;
        foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
        {
            if (c.isTrigger || c.bounds.size.y >= maxThickness) continue;
            if (c.GetComponent<MovingPlatform>() != null) continue;
            if (c.GetComponent<BounceAntibody>() != null || c.GetComponent<WhiteCellFloor>() != null) continue;
            if (!c.CompareTag("Ground")) continue;
            // 이름이 '발판'인 것만 (얇은 땅 턱·복도 끝 땅 등은 통과하면 안 되므로 제외)
            bool isPlatform = c.name.Contains("발판") || (c.transform.parent != null && c.transform.parent.name == "발판");
            if (!isPlatform) continue;
            platforms.Add(c);
        }
    }

    // ---- 백혈구 바닥을 깬 뒤 계속 내려찍기 ----
    // C로 백혈구 바닥을 깨면 낙하공격이 '착지'로 끝나서 1초 멈춰 버림 → 멈춤을 끝내고 가장 아래 바닥까지 빠르게 떨어뜨림
    static DivePassThrough instance;
    [Tooltip("백혈구 바닥을 깬 뒤 떨어지는 속도 (C 낙하 속도와 같게)")]
    public float slamSpeed = 25f;
    bool slamming;
    float slamTime;

    void Awake() { instance = this; }

    public static void Slam(PlayerController p)
    {
        if (instance == null || p == null) return;
        instance.slamming = true;
        instance.slamTime = 0f;
    }

    void FixedUpdate()
    {
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            SetIgnore(false);
            slamming = false;
            player = FindFirstObjectByType<PlayerController>();
            playerBodies = player != null ? player.GetComponentsInChildren<Collider2D>(true) : new Collider2D[0];
        }
        if (player == null) return;

        if (slamming)
        {
            slamTime += Time.fixedDeltaTime;
            PlayerStateReader.EndSpecialLandingNow(player);   // 착지 멈춤 바로 끝냄
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            // 먼저 착지했는지 봄 (지난 물리 단계에서 바닥에 막혀 속도가 0이 됐으면 착지)
            bool landed = slamTime > 0.1f && PlayerStateReader.IsGrounded(player) && rb != null && rb.linearVelocity.y > -0.5f;
            if (landed || slamTime > 3f || player.IsDead) slamming = false;
            else if (rb != null && !PlayerStateReader.IsSpecialAttacking(player))
                rb.linearVelocity = new Vector2(0f, -slamSpeed);
        }

        SetIgnore(slamming || PlayerStateReader.IsDiving(player));
    }

    void SetIgnore(bool on)
    {
        if (on == ignoring) return;
        ignoring = on;
        foreach (Collider2D body in playerBodies)
        {
            if (body == null || body.isTrigger) continue;
            foreach (Collider2D p in platforms)
                if (p != null) Physics2D.IgnoreCollision(body, p, on);
        }
    }

    void OnDisable()
    {
        SetIgnore(false);
    }
}
