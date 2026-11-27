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
        BuildBarrierShapes();             // 막의 원래 크기를 기억해 두고 자라나는 그림을 만듦
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
        AnimateBarriers(true);
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
        AnimateBarriers(false);
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

    // ---------------- 막 = 감염된 벽 ----------------
    // 평소엔 안 보임(그냥 벽). 잠기면 위쪽 벽에서 감염된 벽이 아래로 자라나 통로를 막고,
    // 클리어하면 다시 위로 줄어들어 벽 속으로 들어감. 충돌은 잠기는 순간 바로 켜지고, 풀리는 순간 바로 꺼짐.
    [Tooltip("감염된 벽이 자라나거나 줄어드는 시간 (초)")]
    public float barrierGrowTime = 0.45f;
    [Tooltip("감염된 벽 색 (두 색 사이를 꿈틀거리며 오감). 그림이 생기면 흰색으로 두면 그림 색 그대로")]
    public Color infectedColorA = new Color(0.55f, 0.18f, 0.78f, 0.95f);
    public Color infectedColorB = new Color(0.95f, 0.42f, 0.72f, 0.95f);

    class BarrierShape
    {
        public SpriteRenderer source;   // 원래 막 그림 (안 보이게 꺼 둠, 크기 기준)
        public SpriteRenderer visual;   // 실제로 자라나는 그림 (실행할 때 만든 자식)
        public Collider2D[] cols;
        public float top, fullHeight, width;
    }

    readonly List<BarrierShape> shapes = new List<BarrierShape>();
    float barrierAmount;     // 0 = 숨김, 1 = 바닥까지 다 막음
    bool barrierClosed;
    float barrierTime;

    void BuildBarrierShapes()
    {
        shapes.Clear();
        if (barriers == null) return;
        foreach (GameObject b in barriers)
        {
            if (b == null) continue;
            SpriteRenderer src = b.GetComponent<SpriteRenderer>();
            BarrierShape s = new BarrierShape { source = src, cols = b.GetComponents<Collider2D>() };
            Bounds bb = BarrierBounds(b);
            s.top = bb.max.y;
            s.fullHeight = bb.size.y;
            s.width = bb.size.x;
            if (src != null)
            {
                GameObject v = new GameObject("감염된 벽 (자라나는 그림)");
                v.transform.SetParent(b.transform, false);
                s.visual = v.AddComponent<SpriteRenderer>();
                s.visual.sprite = src.sprite;
                s.visual.drawMode = SpriteDrawMode.Sliced;
                s.visual.sortingLayerID = src.sortingLayerID;
                s.visual.sortingOrder = src.sortingOrder + 1;
                src.enabled = false;
            }
            shapes.Add(s);
        }
    }

    // 즉시 열기/닫기 (시작할 때, 죽어서 되돌릴 때)
    void SetBarriers(bool on)
    {
        barrierClosed = on;
        barrierAmount = on ? 1f : 0f;
        foreach (BarrierShape s in shapes) foreach (Collider2D c in s.cols) if (c != null) c.enabled = on;
        ApplyBarrierShape();
    }

    // 애니메이션으로 열기/닫기 (잠길 때, 클리어할 때)
    void AnimateBarriers(bool close)
    {
        barrierClosed = close;
        foreach (BarrierShape s in shapes) foreach (Collider2D c in s.cols) if (c != null) c.enabled = close;
    }

    void Update()
    {
        float target = barrierClosed ? 1f : 0f;
        if (!Mathf.Approximately(barrierAmount, target) || barrierAmount > 0f)
        {
            barrierAmount = Mathf.MoveTowards(barrierAmount, target, Time.deltaTime / Mathf.Max(0.01f, barrierGrowTime));
            barrierTime += Time.deltaTime;
            ApplyBarrierShape();
        }
    }

    void ApplyBarrierShape()
    {
        float k = barrierClosed ? 1f - (1f - barrierAmount) * (1f - barrierAmount) : barrierAmount * barrierAmount;   // 부드럽게
        float pulse = 0.5f + 0.5f * Mathf.Sin(barrierTime * 5f);
        foreach (BarrierShape s in shapes)
        {
            if (s.visual == null) continue;
            float h = s.fullHeight * k;
            s.visual.enabled = h > 0.02f;
            if (!s.visual.enabled) continue;
            float wobble = 1f + 0.12f * Mathf.Sin(barrierTime * 7f + s.top);   // 살아 있는 듯 꿈틀
            s.visual.size = new Vector2(s.width * wobble, h);
            Vector3 p = s.visual.transform.parent.position;
            s.visual.transform.position = new Vector3(p.x, s.top - h * 0.5f, p.z);   // 위쪽 벽에 붙어서 아래로 자람
            s.visual.color = Color.Lerp(infectedColorA, infectedColorB, pulse);
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
