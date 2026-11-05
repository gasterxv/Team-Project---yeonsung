using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    [Header("체크포인트 설정")]
    public bool checkPointSpot = false;     // true인 체크포인트에서 부활합니다.
    public bool activateOnTouch = true;     // 플레이어가 닿으면 이 체크포인트를 새 부활 지점으로 삼음

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!activateOnTouch)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        Activate();
    }

    // 이 체크포인트만 true로 만들고 나머지는 false로 만듭니다.
    public void Activate()
    {
        CheckPoint[] all = FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);
        foreach (CheckPoint checkPoint in all)
        {
            checkPoint.checkPointSpot = false;
        }

        checkPointSpot = true;
        Debug.Log("체크포인트 저장!");
    }

    // 이미지가 없어도 Scene 화면에서 위치를 볼 수 있게 표시합니다. (초록 = 활성, 회색 = 비활성)
    void OnDrawGizmos()
    {
        Gizmos.color = checkPointSpot ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}