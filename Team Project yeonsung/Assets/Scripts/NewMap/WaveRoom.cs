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

    enum RoomState { Ready, Running, Cleared }

    RoomState state = RoomState.Ready;
    readonly List<EnemyBase> alive = new List<EnemyBase>();
    Coroutine running;

    protected override void Awake()
    {
        base.Awake();
        GetComponent<BoxCollider2D>().isTrigger = true;
        SetBarriers(false);
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
        yield return new WaitForSeconds(firstWaveDelay);
        for (int w = 0; w < waves.Length; w++)
        {
            List<EnemyBase> order = new List<EnemyBase>();
            for (int i = 0; i < waves[w].dengue; i++) order.Add(denguePrefab);
            for (int i = 0; i < waves[w].hepatitis; i++) order.Add(hepatitisPrefab);

            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == null) { Debug.LogWarning("[새맵] 웨이브 방: 몬스터 프리팹이 비어 있어요."); continue; }
                Vector2 p = spawnPoints[i % spawnPoints.Length] + new Vector2((i / spawnPoints.Length) * 1.5f, 0f);
                EnemyBase e = Instantiate(order[i], p, Quaternion.identity);
                e.UseSpawnPointManagement();   // 카메라 거리로 사라지지 않게
                alive.Add(e);
                yield return new WaitForSeconds(spawnStagger);
            }
            Debug.Log("[새맵] 웨이브 " + (w + 1) + " / " + waves.Length + " (뎅기 " + waves[w].dengue + ", 간염 " + waves[w].hepatitis + ")");

            while (AnyAlive()) yield return new WaitForSeconds(0.2f);
            if (w < waves.Length - 1) yield return new WaitForSeconds(betweenWaves);
        }

        state = RoomState.Cleared;
        SetBarriers(false);
        running = null;
        Debug.Log("[새맵] 웨이브 방 클리어: 막이 사라졌어요.");
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
        bool cleared = (bool)saved;
        state = cleared ? RoomState.Cleared : RoomState.Ready;
        SetBarriers(false);
    }
}
