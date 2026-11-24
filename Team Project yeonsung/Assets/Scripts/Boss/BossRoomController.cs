using UnityEngine;

// 보스방 흐름 관리
// - 플레이어가 입장 감지 영역(이 오브젝트의 트리거)에 들어오면 입구 문이 닫힘
// - 보스를 잡으면 입구/출구 문이 모두 사라짐
// - 보스전 중 죽고 부활하면 입구 문이 열리고, 보스(체력/위치)와 발판이 처음 상태로 돌아감
[RequireComponent(typeof(Collider2D))]
public class BossRoomController : MonoBehaviour
{
    public BossRoomDoor entranceDoor;
    public BossRoomDoor exitDoor;
    public GameObject bossRoot;                 // 보스 프리팹 인스턴스의 최상위 오브젝트
    public BreakablePlatform[] platforms;

    private BossController boss;
    private GameObject bossTemplate;            // 처음 상태의 보스 복제본 (꺼 둠)
    private bool bossDefeated;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;

        if (bossRoot == null)
        {
            Debug.LogError("BossRoomController: bossRoot를 연결해주세요.", this);
            return;
        }

        // 원본을 잠깐 끈 상태에서 복제해야 복제본의 Awake/Start가 실행되지 않습니다.
        bool wasActive = bossRoot.activeSelf;
        bossRoot.SetActive(false);
        bossTemplate = Instantiate(bossRoot, bossRoot.transform.position, bossRoot.transform.rotation, bossRoot.transform.parent);
        bossTemplate.name = bossRoot.name + " (리셋용)";
        bossRoot.SetActive(wasActive);

        boss = bossRoot.GetComponentInChildren<BossController>(true);
    }

    void OnEnable()
    {
        GameFlowManager.OnPlayerRespawn += OnPlayerRespawn;
    }

    void OnDisable()
    {
        GameFlowManager.OnPlayerRespawn -= OnPlayerRespawn;
    }

    void Start()
    {
        if (entranceDoor != null) entranceDoor.SetClosed(false);
        if (exitDoor != null) exitDoor.SetClosed(true);
    }

    void Update()
    {
        if (bossDefeated || boss == null) return;

        if (boss.CurrentState == BossController.BossState.Death)
        {
            bossDefeated = true;
            if (entranceDoor != null) entranceDoor.SetClosed(false);
            if (exitDoor != null) exitDoor.SetClosed(false);
            Debug.Log("보스 처치! 보스방 문이 열렸습니다.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (bossDefeated || entranceDoor == null || entranceDoor.IsClosed) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        entranceDoor.SetClosed(true);
    }

    void OnPlayerRespawn()
    {
        if (bossDefeated) return;

        if (entranceDoor != null) entranceDoor.SetClosed(false);
        ResetBoss();

        if (platforms != null)
        {
            foreach (BreakablePlatform p in platforms)
            {
                if (p != null) p.RestoreNow();
            }
        }
    }

    void ResetBoss()
    {
        if (bossTemplate == null) return;

        if (bossRoot != null) Destroy(bossRoot);

        bossRoot = Instantiate(bossTemplate, bossTemplate.transform.position, bossTemplate.transform.rotation, bossTemplate.transform.parent);
        bossRoot.name = bossTemplate.name.Replace(" (리셋용)", "");
        bossRoot.SetActive(true);
        boss = bossRoot.GetComponentInChildren<BossController>(true);
    }
}
