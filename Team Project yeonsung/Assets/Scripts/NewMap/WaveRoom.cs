using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 웨이브 방. 플레이어가 방 바닥(이 오브젝트의 트리거 영역)에 내려서면 양쪽 막이 생겨 잠기고,
// 웨이브를 차례로 소환합니다. 한 웨이브를 다 잡으면 잠깐 쉬고 다음 웨이브. 마지막까지 다 잡으면 막이 사라짐.
// 깬 상태는 체크포인트에 저장되고, 깨기 전에 죽으면 남은 몬스터를 치우고 처음(잠기기 전)으로 돌아갑니다.
// 몬스터 프리팹·EnemyBase는 고치지 않음. (소환한 몬스터는 UseSpawnPointManagement로 거리 디스폰만 끔)
[RequireComponent(typeof(BoxCollider2D))]
public class WaveRoom : ResettableBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public int dengue;
        public int hepatitis;
    }

    public EnemyBase denguePrefab;
    public EnemyBase hepatitisPrefab;
    public Vector2[] spawnPoints = { new Vector2(96f, -31f), new Vector2(100.5f, -31f), new Vector2(105f, -31f) };
    public Wave[] waves =
    {
        new Wave { dengue = 3, hepatitis = 0 },
        new Wave { dengue = 0, hepatitis = 3 },
        new Wave { dengue = 2, hepatitis = 2 },
    };
    public GameObject[] barriers;          // 잠길 때 켜지는 막 (콜라이더+그림)
    public float firstWaveDelay = 1f;      // 잠기고 첫 웨이브까지
    public float betweenWaves = 1.5f;      // 웨이브 사이 쉬는 시간
    public float spawnStagger = 0.25f;     // 같은 웨이브 안에서 한 마리씩 나오는 간격
    public float countdownStep = 0.5f;     // 3·2·1 한 숫자 시간
    public float floorY = -31.6f;          // 소환 예고 얼룩을 그릴 바닥 높이

    readonly List<SpawnWarningBlob> blobs = new List<SpawnWarningBlob>();

    enum RoomState { Ready, Running, Cleared }

    RoomState state = RoomState.Ready;
    readonly List<EnemyBase> alive = new List<EnemyBase>();
    Coroutine running;

    [Tooltip("켜면 시작할 때 방 바닥 높이를 직접 찾아서 잠김 영역·소환 위치·예고 얼룩을 바닥에 맞춤 (씬에서 바닥을 옮겨도 따라감)")]
    public bool autoFitFloor = true;
    [Tooltip("바닥을 찾을 때 위에서 아래로 쏘는 시작 높이 (방 천장 바로 아래)")]
    public float scanFromY = -14.2f;

    protected override void Awake()
    {
        base.Awake();
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (autoFitFloor) FitToFloor();   // 막 콜라이더를 끄기 전에 (막에 레이가 걸리지 않게 막은 건너뜀)
        SetBarriers(false);
    }

    void FitToFloor()
    {
        Physics2D.SyncTransforms();
        BoxCollider2D zone = GetComponent<BoxCollider2D>();
        float? center = FloorAt(transform.position.x);
        if (center == null) { Debug.LogWarning("[새맵] 웨이브 방: 바닥을 못 찾아서 기존 값을 써요."); return; }

        floorY = center.Value;
        // 잠김 영역: 바닥 위 2칸, 가로는 양쪽 막보다 1칸 안쪽부터
        // (바닥이 평평하면 막 바로 위에서 잠겨 플레이어가 막에 밀려 밖에 갇히는 문제 방지)
        // 오브젝트는 안 옮기고 콜라이더 위치·크기만 → 자식인 막은 그대로
        float x0 = transform.position.x - zone.size.x * 0.5f, x1 = transform.position.x + zone.size.x * 0.5f;
        if (barriers != null && barriers.Length >= 2 && barriers[0] != null && barriers[1] != null)
        {
            Bounds l = BarrierBounds(barriers[0]), r = BarrierBounds(barriers[1]);
            if (l.center.x > r.center.x) { Bounds tmp = l; l = r; r = tmp; }
            x0 = l.max.x + 1f;
            x1 = r.min.x - 1f;
        }
        float sx = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        zone.size = new Vector2(Mathf.Max(1f, x1 - x0) / sx, zone.size.y);
        float worldCenterY = floorY + zone.size.y * sy * 0.5f;
        zone.offset = new Vector2(((x0 + x1) * 0.5f - transform.position.x) / sx, (worldCenterY - transform.position.y) / sy);
        // 소환 위치: 각 x에서 바닥을 다시 찾아 그 위 0.8
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            float? f = FloorAt(spawnPoints[i].x);
            spawnPoints[i] = new Vector2(spawnPoints[i].x, (f ?? floorY) + 0.8f);
        }
        Debug.Log("[새맵] 웨이브 방: 바닥 높이 " + floorY.ToString("0.##") + "에 맞췄어요.");
    }

    // x 위치에서 위→아래로 내려가며 처음 만나는 '두꺼운' 땅 윗면 (얇은 발판·트리거·막·플레이어는 무시)
    float? FloorAt(float x)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, scanFromY), Vector2.down, 40f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit2D h in hits)
        {
            Collider2D c = h.collider;
            if (c == null || c.isTrigger || h.distance <= 0f) continue;
            if (IsBarrier(c) || PlayerStateReader.FindPlayer(c) != null || c.GetComponentInParent<EnemyBase>() != null) continue;
            if (c.bounds.size.y < 1f) continue;   // 발판(두께 0.6)은 건너뜀
            return h.point.y;
        }
        return null;
    }

    static Bounds BarrierBounds(GameObject b)
    {
        SpriteRenderer s = b.GetComponent<SpriteRenderer>();
        if (s != null) return s.bounds;
        Collider2D c = b.GetComponent<Collider2D>();
        return c != null ? c.bounds : new Bounds(b.transform.position, Vector3.zero);
    }

    bool IsBarrier(Collider2D c)
    {
        if (barriers == null) return false;
        foreach (GameObject b in barriers) if (b != null && c.gameObject == b) return true;
        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (state != RoomState.Ready || PlayerStateReader.FindPlayer(other) == null) return;
        state = RoomState.Running;
        SetBarriers(true);
        running = StartCoroutine(RunWaves());
        Debug.Log("[새맵] 웨이브 방 잠김: " + waves.Length + "웨이브 시작");
    }

    IEnumerator RunWaves()
    {
        // 잠김 알림: 빨간 위험 경고 + 테두리 빨간 깜빡임 (클리어할 때까지)
        ScreenAnnouncer.ShowDanger("감염 반응 감지!", "통로가 막혔다 — 모든 바이러스를 처치하라", 2f);
        ScreenAnnouncer.SetAlarm(true);
        yield return new WaitForSeconds(Mathf.Max(firstWaveDelay, 2f));

        for (int w = 0; w < waves.Length; w++)
        {
            List<EnemyBase> order = new List<EnemyBase>();
            for (int i = 0; i < waves[w].dengue; i++) order.Add(denguePrefab);
            for (int i = 0; i < waves[w].hepatitis; i++) order.Add(hepatitisPrefab);

            List<Vector2> positions = SpawnPositionsAwayFromPlayer(order.Count);

            // 웨이브 번호 + 3·2·1, 그동안 나올 자리 바닥에 보라 얼룩이 커짐
            bool last = w == waves.Length - 1;
            ScreenAnnouncer.Show("웨이브 " + (w + 1) + " / " + waves.Length, last ? "마지막 웨이브!" : WaveText(waves[w]), 1.4f);
            float warnTime = 0.5f + countdownStep * 3f;
            foreach (Vector2 p in positions) blobs.Add(SpawnWarningBlob.Create(new Vector2(p.x, floorY), warnTime));
            yield return new WaitForSeconds(0.5f);
            for (int c = 3; c >= 1; c--)
            {
                ScreenAnnouncer.Count(c.ToString(), countdownStep);
                yield return new WaitForSeconds(countdownStep);
            }

            for (int i = 0; i < order.Count; i++)
            {
                if (i < blobs.Count && blobs[i] != null) blobs[i].Pop();
                if (order[i] == null) { Debug.LogWarning("[새맵] 웨이브 방: 몬스터 프리팹이 비어 있어요."); continue; }
                EnemyBase e = Instantiate(order[i], positions[i], Quaternion.identity);
                e.UseSpawnPointManagement();   // 카메라 거리로 사라지지 않게
                alive.Add(e);
                yield return new WaitForSeconds(spawnStagger);
            }
            blobs.Clear();
            Debug.Log("[새맵] 웨이브 " + (w + 1) + " / " + waves.Length + " (뎅기 " + waves[w].dengue + ", 간염 " + waves[w].hepatitis + ")");

            while (AnyAlive()) yield return new WaitForSeconds(0.2f);
            if (!last) yield return new WaitForSeconds(betweenWaves);
        }

        state = RoomState.Cleared;
        SetBarriers(false);
        running = null;
        ScreenAnnouncer.SetAlarm(false);
        ScreenAnnouncer.Show("정화 완료!", "막이 사라졌다", 2.2f);
        Debug.Log("[새맵] 웨이브 방 클리어: 막이 사라졌어요.");
    }

    [Tooltip("플레이어와 이 거리(가로) 안의 소환 위치는 되도록 쓰지 않음 (몬스터가 플레이어 위에 바로 나와서 맞는 것 방지)")]
    public float keepAwayFromPlayer = 3f;

    // 플레이어에게서 먼 소환 위치부터 씀. 너무 가까운 곳은 다른 곳이 있으면 건너뜀.
    List<Vector2> SpawnPositionsAwayFromPlayer(int count)
    {
        PlayerController p = FindFirstObjectByType<PlayerController>();
        float px = p != null ? p.transform.position.x : float.NaN;
        List<Vector2> pts = new List<Vector2>(spawnPoints);
        if (!float.IsNaN(px))
        {
            pts.Sort((a, b) => Mathf.Abs(b.x - px).CompareTo(Mathf.Abs(a.x - px)));   // 먼 곳부터
            List<Vector2> far = pts.FindAll(v => Mathf.Abs(v.x - px) >= keepAwayFromPlayer);
            if (far.Count > 0) pts = far;
        }
        List<Vector2> result = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            Vector2 baseP = pts[i % pts.Count];
            // 같은 자리에 두 번째로 나오면 방 가운데 쪽으로 1.5칸 비켜서 (벽 속으로 들어가지 않게)
            float toCenter = Mathf.Sign(transform.position.x - baseP.x);
            if (toCenter == 0f) toCenter = 1f;
            result.Add(baseP + new Vector2((i / pts.Count) * 1.5f * toCenter, 0f));
        }
        return result;
    }

    static string WaveText(Wave w)
    {
        if (w.dengue > 0 && w.hepatitis > 0) return "뎅기 " + w.dengue + " · 간염 " + w.hepatitis;
        if (w.dengue > 0) return "뎅기 바이러스 " + w.dengue;
        return "간염 바이러스 " + w.hepatitis;
    }

    bool AnyAlive()
    {
        alive.RemoveAll(e => e == null || e.IsDead);
        return alive.Count > 0;
    }

    void SetBarriers(bool on)
    {
        if (barriers == null) return;
        foreach (GameObject b in barriers)
        {
            if (b == null) continue;
            foreach (Collider2D c in b.GetComponents<Collider2D>()) c.enabled = on;
            foreach (SpriteRenderer s in b.GetComponents<SpriteRenderer>()) s.enabled = on;
        }
    }

    void ClearMonsters()
    {
        foreach (EnemyBase e in alive)
        {
            if (e == null) continue;
            e.gameObject.SetActive(false);
            Destroy(e.gameObject);
        }
        alive.Clear();
    }

    protected override object CaptureState() { return state == RoomState.Cleared; }

    protected override void ApplyState(object saved)
    {
        if (running != null) { StopCoroutine(running); running = null; }
        ClearMonsters();
        foreach (SpawnWarningBlob b in blobs) if (b != null) Destroy(b.gameObject);
        blobs.Clear();
        ScreenAnnouncer.HideAll();
        bool cleared = (bool)saved;
        state = cleared ? RoomState.Cleared : RoomState.Ready;
        SetBarriers(false);
    }
}
