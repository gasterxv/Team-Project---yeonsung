using UnityEngine;

// 보스방 입구/출구 문. 닫혀 있으면 막고(보스 돌진도 여기서 멈춤), 열리면 보이지도 않고 통과됩니다.
// 태그는 BossRoomTacle 이어야 보스 돌진이 부딪혀 멈춥니다.
public class BossRoomDoor : MonoBehaviour
{
    [SerializeField] private bool startClosed = true;

    private Collider2D[] colliders;
    private SpriteRenderer[] renderers;

    public bool IsClosed { get; private set; }

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>(true);
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SetClosed(startClosed);
    }

    public void SetClosed(bool closed)
    {
        if (colliders == null) Awake();

        IsClosed = closed;
        foreach (Collider2D c in colliders) c.enabled = closed;
        foreach (SpriteRenderer r in renderers) r.enabled = closed;
    }
}
