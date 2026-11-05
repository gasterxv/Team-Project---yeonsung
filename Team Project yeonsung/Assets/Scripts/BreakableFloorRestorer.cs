using UnityEngine;

public sealed class BreakableFloorRestorer : MonoBehaviour
{
    [Header("부서지는 바닥 복구")]
    [SerializeField] private GameObject brokenFloorPrefab;
    [SerializeField] private GameObject currentFloor;
    [SerializeField] private Transform restorePoint;

    private Vector3 restorePosition;
    private Quaternion restoreRotation;

    private void Awake()
    {
        if (currentFloor != null)
        {
            restorePosition = currentFloor.transform.position;
            restoreRotation = currentFloor.transform.rotation;
        }
        else if (restorePoint != null)
        {
            restorePosition = restorePoint.position;
            restoreRotation = restorePoint.rotation;
        }
        else
        {
            restorePosition = transform.position;
            restoreRotation = transform.rotation;
        }
    }

    private void Start()
    {
        // 플레이어가 체크포인트에서 부활할 때마다 바닥을 복구합니다.
        GameFlowManager.OnPlayerRespawn += Restore;

        if (currentFloor == null)
            Restore();
    }

    private void OnDestroy()
    {
        GameFlowManager.OnPlayerRespawn -= Restore;
    }

    public void Restore()
    {
        if (currentFloor != null || brokenFloorPrefab == null)
            return;

        currentFloor = Instantiate(brokenFloorPrefab, restorePosition, restoreRotation);
    }
}