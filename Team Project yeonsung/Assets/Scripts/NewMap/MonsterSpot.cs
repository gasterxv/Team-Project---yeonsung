using UnityEngine;

// 몬스터 배치 자리. 게임이 시작되면 이 자리에 몬스터를 만들고,
// 죽으면 체크포인트 규칙을 따름: 마지막 체크포인트 '전'에 잡은 몬스터는 그대로 죽어 있고,
// 체크포인트 '후'에 잡은 몬스터는 플레이어가 죽으면 다시 살아남. (살아 있던 몬스터도 처음 자리·체력으로 되돌림)
// 몬스터 프리팹·EnemyBase는 고치지 않음 (거리로 사라지는 기능만 UseSpawnPointManagement로 끔).
public class MonsterSpot : ResettableBehaviour
{
    public EnemyBase prefab;

    EnemyBase instance;
    bool defeated;

    void Start()
    {
        if (!defeated) Spawn();
    }

    void Update()
    {
        if (!defeated && instance != null && instance.IsDead) defeated = true;
        else if (!defeated && instance == null && Application.isPlaying && spawned) defeated = true;   // 처치 후 삭제된 경우
    }

    bool spawned;

    void Spawn()
    {
        Remove();
        if (prefab == null) { Debug.LogWarning("[새맵] " + name + ": 몬스터 프리팹이 비어 있어요."); return; }
        instance = Instantiate(prefab, transform.position, Quaternion.identity);
        instance.UseSpawnPointManagement();   // 카메라에서 멀어져도 사라지지 않게
        spawned = true;
        defeated = false;
    }

    void Remove()
    {
        if (instance == null) return;
        instance.gameObject.SetActive(false);
        Destroy(instance.gameObject);
        instance = null;
    }

    protected override object CaptureState() { return defeated; }

    protected override void ApplyState(object state)
    {
        bool wasDefeated = (bool)state;
        if (wasDefeated) { Remove(); defeated = true; spawned = false; }
        else Spawn();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Remove();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = prefab != null && prefab.name.Contains("Hepatitis") ? new Color(0.9f, 0.5f, 0.3f) : new Color(0.6f, 0.5f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
