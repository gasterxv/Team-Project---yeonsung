using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossRoomController : MonoBehaviour
{
    public BossRoomDoor entranceDoor;
    public BossRoomDoor exitDoor;
    public GameObject bossRoot;
    public BreakablePlatform[] platforms;

    private BossController boss;
    private GameObject bossTemplate;
    private bool bossDefeated;
    private bool playerEntered;
    private string originalBossName;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (bossRoot == null || bossRoot.GetComponentInChildren<BossController>(true) == null)
        {
            Debug.LogError("BossRoomController: BossController가 포함된 Boss Root를 연결하세요.", this);
            enabled = false;
            return;
        }

        originalBossName = bossRoot.name;
        bool wasActive = bossRoot.activeSelf;
        bossRoot.SetActive(false);
        bossTemplate = Instantiate(bossRoot, bossRoot.transform.position,
            bossRoot.transform.rotation, bossRoot.transform.parent);
        bossTemplate.name = originalBossName + " (리셋용)";
        BindBoss();
        bossRoot.SetActive(wasActive);
    }

    private void BindBoss()
    {
        if (boss != null) boss.Defeated -= OnBossDefeated;
        boss = bossRoot.GetComponentInChildren<BossController>(true);
        if (boss == null) return;
        boss.SetDeathRoot(bossRoot);
        boss.Defeated += OnBossDefeated;
    }

    private void OnEnable()
    {
        GameFlowManager.OnPlayerRespawn += OnPlayerRespawn;
        if (boss != null)
        {
            boss.Defeated -= OnBossDefeated;
            boss.Defeated += OnBossDefeated;
        }
    }

    private void OnDisable()
    {
        GameFlowManager.OnPlayerRespawn -= OnPlayerRespawn;
        if (boss != null) boss.Defeated -= OnBossDefeated;
    }

    private void Start()
    {
        if (entranceDoor != null) entranceDoor.SetClosed(false);
        if (exitDoor != null) exitDoor.SetClosed(!bossDefeated);
    }

    private void Update()
    {
        // 재활성화 등으로 이벤트를 놓친 경우에도 상태를 확인합니다.
        if (!bossDefeated && boss != null && boss.CurrentState == BossController.BossState.Death)
            OnBossDefeated();
    }

    private void OnBossDefeated()
    {
        if (bossDefeated) return;
        bossDefeated = true;
        if (entranceDoor != null) entranceDoor.SetClosed(false);
        if (exitDoor != null) exitDoor.SetClosed(false);
        Debug.Log("보스 처치! 문을 열고, 사망 애니메이션이 끝나면 보스를 삭제합니다.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled || bossDefeated || playerEntered || boss == null) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;
        playerEntered = true;
        if (entranceDoor != null) entranceDoor.SetClosed(true);
        if (exitDoor != null) exitDoor.SetClosed(true);
        // 입장 시에는 문만 닫습니다. 보스는 3회 피격 후 스스로 전투를 시작합니다.
    }

    private void OnPlayerRespawn()
    {
        // 같은 프레임에 사망과 부활이 겹친 경우에도 처치를 우선합니다.
        if (boss != null && boss.CurrentState == BossController.BossState.Death)
            OnBossDefeated();
        if (bossDefeated) return;
        playerEntered = false;
        if (entranceDoor != null) entranceDoor.SetClosed(false);
        if (exitDoor != null) exitDoor.SetClosed(true);
        ResetBoss();
        if (platforms == null) return;
        foreach (BreakablePlatform platform in platforms)
            if (platform != null) platform.RestoreNow();
    }

    private void ResetBoss()
    {
        if (bossTemplate == null) return;
        if (boss != null) boss.Defeated -= OnBossDefeated;
        if (bossRoot != null)
        {
            bossRoot.SetActive(false);
            Destroy(bossRoot);
        }
        bossRoot = Instantiate(bossTemplate, bossTemplate.transform.position,
            bossTemplate.transform.rotation, bossTemplate.transform.parent);
        bossRoot.name = originalBossName;
        BindBoss();
        bossRoot.SetActive(true);
    }

    private void OnDestroy()
    {
        if (bossTemplate != null) Destroy(bossTemplate);
    }
}
