using UnityEngine;

// 사망 시 초기화 규칙:
// - 게임 시작할 때, 그리고 체크포인트를 밟을 때 모든 ResettableBehaviour 상태를 저장
// - 플레이어가 부활하면(GameFlowManager.OnPlayerRespawn) 저장한 상태로 되돌림
// 씬에 하나만 둡니다. (새맵 회색 상자 메뉴가 '규칙' 오브젝트에 붙여 줌)
public class CheckpointResetManager : MonoBehaviour
{
    public static CheckpointResetManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        GameFlowManager.OnPlayerRespawn += RestoreAll;
    }

    void OnDisable()
    {
        GameFlowManager.OnPlayerRespawn -= RestoreAll;
    }

    // 씬에 놓인 플레이어 크기 (새 맵은 2.44). GameFlowManager가 부활 때 프리팹 원래 크기로 새 플레이어를 만들기 때문에
    // GameFlowManager는 고치지 않고 여기서 부활 직후 크기를 되돌립니다.
    Vector3 playerScale;
    bool hasPlayerScale;

    void Start()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerScale = player.transform.localScale;
            hasPlayerScale = true;
        }
        SaveAll();
    }

    void KeepPlayerScale()
    {
        if (!hasPlayerScale) return;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        Vector3 s = player.transform.localScale;
        float facing = Mathf.Sign(s.x) == 0f ? 1f : Mathf.Sign(s.x);
        player.transform.localScale = new Vector3(facing * Mathf.Abs(playerScale.x), playerScale.y, playerScale.z);
    }

    public void SaveAll()
    {
        foreach (ResettableBehaviour r in ResettableBehaviour.All)
        {
            if (r != null) r.SaveState();
        }
    }

    public void RestoreAll()
    {
        KeepPlayerScale();
        foreach (ResettableBehaviour r in ResettableBehaviour.All)
        {
            if (r != null) r.RestoreState();
        }
        Debug.Log("[새맵] 부활: 마지막 체크포인트 상태로 되돌렸어요.");
    }
}
