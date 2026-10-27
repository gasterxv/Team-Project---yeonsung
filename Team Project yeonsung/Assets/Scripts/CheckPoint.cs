using System;
using UnityEngine;

// 체크포인트 오브젝트에 붙이는 스크립트
// - 이 오브젝트에는 Collider2D(Is Trigger 체크)가 있어야 해요 (상호작용 감지용)
// - 오브젝트 태그는 "CheckPoint"로 지정해주세요 (GameFlowManager가 부활 지점을 찾을 때 사용)
// (두 사람 버전이 병합 충돌로 남아 있던 것을 합쳤어요: F키 상호작용 + 닿으면 저장 옵션)
public class CheckPoint : MonoBehaviour
{
    [Header("체크포인트 설정")]
    public bool checkPointSpot = false;     // true인 체크포인트에서 부활합니다.
    public bool activateOnTouch = false;    // 체크하면 F키 없이 닿기만 해도 저장

    [Header("참조")]
    [SerializeField] private CheckPoint[] otherCheckPoints; // 비워두면 씬의 모든 체크포인트를 자동으로 찾음

    private bool CheckPointUseReady = false;

    // 플레이어 스크립트(죽음 처리)에서 부활 지점을 찾을 때 읽는 용도 (읽기 전용)
    public bool IsCheckPointSpot => checkPointSpot;

    // 체크포인트를 사용했을 때 한 번 발동 (부서지는 바닥 등이 += 로 구독)
    public event Action OnCheckPointUsed;

    private void Update()
    {
        // 상호작용 범위 안에서 F키 입력을 감지하면 실행
        if (CheckPointUseReady && Input.GetKeyDown(KeyCode.F))
        {
            Activate();
        }
    }

    // 플레이어가 상호작용 감지 콜라이더에 들어오면 true
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        CheckPointUseReady = true;
        if (activateOnTouch)
        {
            Activate();
        }
    }

    // 범위를 벗어나면 다시 false (벗어났는데도 F로 작동하는 것 방지)
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
        {
            CheckPointUseReady = false;
        }
    }

    // 이 체크포인트만 true로 만들고 나머지는 false로 만듭니다.
    public void Activate()
    {
        // (HP 회복 같은 추가 기능은 나중에 여기에 넣을 예정)
        CheckPoint[] others = (otherCheckPoints != null && otherCheckPoints.Length > 0)
            ? otherCheckPoints
            : FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);

        foreach (CheckPoint cp in others)
        {
            if (cp == null || cp == this) continue;
            cp.checkPointSpot = false;
        }
        checkPointSpot = true;
        Debug.Log("체크포인트 저장!");

        OnCheckPointUsed?.Invoke();
    }

    // 이미지가 없어도 Scene 화면에서 위치를 볼 수 있게 표시합니다. (초록 = 활성, 회색 = 비활성)
    void OnDrawGizmos()
    {
        Gizmos.color = checkPointSpot ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
