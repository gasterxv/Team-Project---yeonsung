
using System;
using UnityEngine;

// 체크포인트 오브젝트에 붙이는 스크립트
public class CheckPoint : MonoBehaviour
{
    [Header("체크포인트 설정")]
    public bool checkPointSpot = false;
    public bool activateOnTouch = true;

    private bool checkPointUseReady = false;

    // 기존 스크립트와의 호환성을 위한 읽기 전용 속성
    public bool IsCheckPointSpot => checkPointSpot;

    // 체크포인트 활성화 이벤트
    public event Action OnCheckPointUsed;

    private void Update()
    {
        // 자동 활성화를 끈 경우 F키로 체크포인트 활성화
        if (!activateOnTouch &&
            checkPointUseReady &&
            Input.GetKeyDown(KeyCode.F))
        {
            Activate();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        checkPointUseReady = true;

        if (activateOnTouch)
        {
            Activate();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            checkPointUseReady = false;
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player") ||
               other.GetComponentInParent<PlayerController>() != null;
    }

    public void Activate()
    {
        // 다른 체크포인트는 비활성화
        CheckPoint[] all =
            FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);

        foreach (CheckPoint cp in all)
        {
            cp.checkPointSpot = false;
        }

        // 현재 체크포인트 활성화
        checkPointSpot = true;

        Debug.Log("체크포인트 저장!");
        OnCheckPointUsed?.Invoke();
    }

    // Scene 화면에서 체크포인트 상태 표시
    private void OnDrawGizmos()
    {
        Gizmos.color = checkPointSpot ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
