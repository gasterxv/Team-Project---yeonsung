using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 테스트용 기록 도구 (게임에는 영향 없음): Play 중 몬스터 상태 변화, 몬스터 공격 판정(공격 상자·몸통박치기)이
// 플레이어와 겹쳤는지, 플레이어 체력이 실제로 줄었는지를 Editor.log에 "[몬스터 기록]"으로 적습니다.
// 메뉴: Tools > 새맵 > 테스트 이동 > 몬스터 기록 켜기·끄기
public static class NewMapMonsterProbe
{
    static bool on;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    class BoxInfo { public int hpAtSpawn; public bool overlapped; public string flagsAtOverlap = ""; public string owner; public float spawnTime; }
    static readonly Dictionary<int, BoxInfo> boxes = new Dictionary<int, BoxInfo>();
    static readonly Dictionary<int, EnemyBase.MonsterState> states = new Dictionary<int, EnemyBase.MonsterState>();
    static readonly Dictionary<int, float> stateSince = new Dictionary<int, float>();
    static readonly Dictionary<int, bool> touchedInAttack = new Dictionary<int, bool>();
    static readonly Dictionary<int, int> hpAtAttack = new Dictionary<int, int>();
    static int lastHp = -1;
    static readonly Dictionary<int, string> sprites = new Dictionary<int, string>();

    [MenuItem("Tools/새맵/테스트 이동/몬스터 기록 켜기·끄기")]
    static void Toggle()
    {
        on = !on;
        boxes.Clear(); states.Clear(); stateSince.Clear(); touchedInAttack.Clear(); hpAtAttack.Clear(); lastHp = -1;
        EditorApplication.update -= Tick;
        if (on) EditorApplication.update += Tick;
        Debug.Log("[몬스터 기록] " + (on ? "켬" : "끔"));
    }

    // 플레이어 공격이 몬스터 피격 센서에 닿았을 때와 같은 함수(NotifyPlayerAttack)로 가장 가까운 몬스터를 한 대 때림
    [MenuItem("Tools/새맵/테스트 이동/가장 가까운 몬스터 한 대 때리기 (10)")]
    static void HitNearest()
    {
        PlayerController p = Object.FindFirstObjectByType<PlayerController>();
        if (p == null) return;
        EnemyBase best = null; float bd = float.MaxValue;
        foreach (EnemyBase e in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (e.IsDead) continue;
            float d = Vector2.Distance(e.transform.position, p.transform.position);
            if (d < bd) { bd = d; best = e; }
        }
        if (best == null) return;
        best.NotifyPlayerAttack(10);
        Debug.Log($"[몬스터 기록] {Time.time:0.00} 시험 타격: {best.name}#{best.GetInstanceID()} (거리 {bd:0.0}) → 상태 {best.CurrentState}");
    }

    static string Flags(PlayerController p)
    {
        object kb = typeof(PlayerController).GetField("isKnockedBack", F)?.GetValue(p);
        object si = typeof(PlayerController).GetField("isSpecialInvincible", F)?.GetValue(p);
        object hi = typeof(PlayerController).GetField("isHitInvincible", F)?.GetValue(p);
        object da = typeof(PlayerController).GetField("isDashing", F)?.GetValue(p);
        return "넉백=" + kb + " 특수무적=" + si + " 피격무적=" + hi + " 대쉬=" + da;
    }

    static bool Overlaps(Collider2D a, PlayerController p)
    {
        foreach (Collider2D c in p.GetComponentsInChildren<Collider2D>())
        {
            if (c.isTrigger || !c.enabled) continue;
            ColliderDistance2D d = a.Distance(c);
            if (d.isValid && d.distance <= 0.02f) return true;
        }
        return false;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        PlayerController p = Object.FindFirstObjectByType<PlayerController>();
        if (p == null) return;
        float t = Time.time;
        if (lastHp >= 0 && p.CurrentHealth != lastHp)
            Debug.Log($"[몬스터 기록] {t:0.00} 플레이어 체력 {lastHp} → {p.CurrentHealth}");
        lastHp = p.CurrentHealth;

        // 몬스터 상태 변화
        foreach (EnemyBase e in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            int id = e.GetInstanceID();
            EnemyBase.MonsterState s = e.CurrentState;
            if (!states.TryGetValue(id, out EnemyBase.MonsterState prev) || prev != s)
            {
                string held = stateSince.TryGetValue(id, out float since) ? $" (앞 상태 {t - since:0.00}초)" : "";
                Debug.Log($"[몬스터 기록] {t:0.00} {e.GetType().Name}#{id} {prev} → {s}{held} 위치 {e.transform.position.x:0.0},{e.transform.position.y:0.0}");
                // 간염 몸통박치기 결과
                if (e is JabMob2 && prev == EnemyBase.MonsterState.Attack && states.ContainsKey(id))
                    Debug.Log($"[몬스터 기록]   간염 박치기 끝: 몸 닿음={(touchedInAttack.TryGetValue(id, out bool tc) && tc)}, 체력 {(hpAtAttack.TryGetValue(id, out int h0) ? h0 : -1)} → {p.CurrentHealth}");
                if (e is JabMob2 && s == EnemyBase.MonsterState.Attack) { touchedInAttack[id] = false; hpAtAttack[id] = p.CurrentHealth; }
                states[id] = s; stateSince[id] = t;
            }
            else if (s == EnemyBase.MonsterState.GetDamage && t - stateSince[id] > 3f && t - stateSince[id] < 3.1f)
                Debug.Log($"[몬스터 기록] {t:0.00} {e.GetType().Name}#{id} 피격 상태로 3초째 멈춰 있음!");
            // 뎅기 공격 중 보이는 그림 순서 (돌아오는 모션 확인용)
            if (e is JabMob1)
            {
                SpriteRenderer sr = e.GetComponentInChildren<SpriteRenderer>();
                string sp = sr != null && sr.sprite != null ? sr.sprite.name : "-";
                if (!sprites.TryGetValue(id, out string ps) || ps != sp)
                {
                    sprites[id] = sp;
                    if (s == EnemyBase.MonsterState.Attack || (ps != null && ps.Contains("attack")))
                        Debug.Log($"[몬스터 기록] {t:0.00}   뎅기#{id} 그림 {sp} (상태 {s}, 이 상태 {t - stateSince[id]:0.00}초째)");
                }
            }
            if (e is JabMob2 && s == EnemyBase.MonsterState.Attack)
            {
                Collider2D body = typeof(JabMob2).GetField("attackBodyCollider", F)?.GetValue(e) as Collider2D;
                if (body != null && Overlaps(body, p) && !touchedInAttack[id])
                {
                    touchedInAttack[id] = true;
                    Debug.Log($"[몬스터 기록] {t:0.00}   간염 몸이 플레이어에 닿음. 체력 {p.CurrentHealth} {Flags(p)}");
                }
            }
        }

        // 뎅기 공격 상자
        HashSet<int> alive = new HashSet<int>();
        foreach (EnemyHitbox hb in Object.FindObjectsByType<EnemyHitbox>(FindObjectsSortMode.None))
        {
            if (!hb.gameObject.activeInHierarchy) continue;
            int id = hb.GetInstanceID();
            alive.Add(id);
            Collider2D c = hb.GetComponent<Collider2D>();
            if (!boxes.TryGetValue(id, out BoxInfo info))
            {
                EnemyBase owner = typeof(EnemyHitbox).GetField("owner", F)?.GetValue(hb) as EnemyBase;
                info = new BoxInfo { hpAtSpawn = p.CurrentHealth, owner = owner != null ? owner.name + "#" + owner.GetInstanceID() : "없음", spawnTime = t };
                boxes[id] = info;
                Bounds b = c != null ? c.bounds : new Bounds(hb.transform.position, Vector3.zero);
                Debug.Log($"[몬스터 기록] {t:0.00} 공격 상자 생김 ({info.owner}) 범위 x {b.min.x:0.00}~{b.max.x:0.00} y {b.min.y:0.00}~{b.max.y:0.00} / 플레이어 x {p.transform.position.x:0.00} y {p.transform.position.y:0.00}");
            }
            if (c != null && !info.overlapped && Overlaps(c, p))
            {
                info.overlapped = true;
                info.flagsAtOverlap = Flags(p);
                Debug.Log($"[몬스터 기록] {t:0.00}   공격 상자가 플레이어에 닿음. 체력 {p.CurrentHealth} {info.flagsAtOverlap}");
            }
        }
        List<int> gone = new List<int>();
        foreach (var kv in boxes) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (int id in gone)
        {
            BoxInfo i = boxes[id];
            Debug.Log($"[몬스터 기록] {t:0.00} 공격 상자 사라짐 ({i.owner}, {t - i.spawnTime:0.00}초): 닿음={i.overlapped}, 체력 {i.hpAtSpawn} → {p.CurrentHealth}" +
                      (i.overlapped && p.CurrentHealth >= i.hpAtSpawn ? "  ← 닿았는데 체력 안 줄음! " + i.flagsAtOverlap : ""));
            boxes.Remove(id);
        }
    }
}
