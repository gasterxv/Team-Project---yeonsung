using UnityEngine;

public sealed class BreakableFloorRestorer : MonoBehaviour
{
    [Header("부서지는 바닥 복구")]
    [SerializeField] private GameObject brokenFloorPrefab;
    [SerializeField] private GameObject currentFloor;
    [SerializeField] private Transform restorePoint;
    [SerializeField] private CheckPoint checkPoint;

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
        if (checkPoint == null)
            checkPoint = FindFirstObjectByType<CheckPoint>();

        if (checkPoint != null)
            checkPoint.OnCheckPointUsed += Restore;

        if (currentFloor == null)
            Restore();
    }

    private void OnDestroy()
    {
        if (checkPoint != null)
            checkPoint.OnCheckPointUsed -= Restore;
    }

    public void Restore()
    {
        if (currentFloor != null || brokenFloorPrefab == null)
            return;

        currentFloor = Instantiate(brokenFloorPrefab, restorePosition, restoreRotation);
    }
}
