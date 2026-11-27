using UnityEngine;

// 몬스터 배치 자리. 게임이 시작되면 이 자리에 몬스터를 만들고,
// 죽으면 체크포인트 규칙을 따름: 마지막 체크포인트 '전'에 잡은 몬스터는 그대로 죽어 있고,
// 체크포인트 '후'에 잡은 몬스터는 플레이어가 죽으면 다시 살아남. (살아 있던 몬스터도 처음 자리·체력으로 되돌림)
// waitForAmbush를 켜면 처음엔 비어 있다가 AmbushZone이 Activate()를 부를 때 나타남 (함정 2 B 매복).
// 몬스터 프리팹·EnemyBase는 고치지 않음 (거리로 사라지는 기능만 UseSpawnPointManagement로 끔).
public class MonsterSpot : ResettableBehaviour
{
    public EnemyBase prefab;
    [Tooltip("켜면 처음엔 안 나오고, 매복 구역(AmbushZone)에 플레이어가 들어올 때 나옴")]
    public bool waitForAmbush;

    EnemyBase instance;
    bool defeated;
    bool spawned;
    bool active;   // 나올 차례가 됐는지 (매복이 아니면 처음부터 true)

    struct Saved { public bool defeated, active; }

    protected override void Awake()
    {
        base.Awake();
        active = !waitForAmbush;   // 체크포인트 관리자가 Start에서 상태를 저장하기 전에 정해 둠
    }

    void Start()
    {
        if (active && !defeated && !spawned) Spawn();
    }

    void Update()
    {
        if (defeated || !spawned) return;
        if (instance == null || instance.IsDead) defeated = true;   // 처치됨 (삭제된 경우 포함)
    }

    // 매복 시작 (AmbushZone이 부름)
    public void Activate()
    {
        if (active) return;
        active = true;
        if (!defeated) Spawn();
    }

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
        spawned = false;
        if (instance == null) return;
        instance.gameObject.SetActive(false);
        Destroy(instance.gameObject);
        instance = null;
    }

    protected override object CaptureState() { return new Saved { defeated = defeated, active = active }; }

    protected override void ApplyState(object state)
    {
        Saved s = (Saved)state;
        active = s.active;
        if (!active) { Remove(); defeated = false; return; }   // 매복 전 상태로
        if (s.defeated) { Remove(); defeated = true; }
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
        Gizmos.DrawWireSphere(transform.position, waitForAmbush ? 0.35f : 0.5f);
    }
}
