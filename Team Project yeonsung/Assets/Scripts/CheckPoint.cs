using System;
using UnityEngine;

// 체크포인트 오브젝트에 붙이는 스크립트
// - 이 오브젝트에는 Collider2D(Is Trigger 체크)가 있어야 해요 (상호작용 감지용)
// - 오브젝트 태그는 "체크포인트"로 지정해주세요 (플레이어 부활 지점 탐색용)
public class CheckPoint : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CheckPoint[] otherCheckPoints; // 이 오브젝트를 제외한 모든 체크포인트

    private bool CheckPointUseReady = false;
    private bool CheckPointSpot = false;

    // 플레이어 스크립트(죽음 처리)에서 부활 지점을 찾을 때 읽는 용도 (읽기 전용)
    public bool IsCheckPointSpot => CheckPointSpot;

    // 체크포인트를 사용했을 때 한 번 발동 (부서지는 바닥 등이 += 로 구독)
    public event Action OnCheckPointUsed;

    private void Update()
    {
        // 3. 상호작용 범위 안에서 F키 입력을 감지하면 실행
        if (CheckPointUseReady && Input.GetKeyDown(KeyCode.F))
        {
            UseCheckPoint();
        }
    }

    // 2. 플레이어가 상호작용 감지 콜라이더에 들어오면 true
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CheckPointUseReady = true;
        }
    }

    // 범위를 벗어나면 다시 false (벗어났는데도 F로 작동하는 것 방지)
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CheckPointUseReady = false;
        }
    }

    private void UseCheckPoint()
    {
        // (HP 회복 같은 추가 기능은 나중에 여기에 넣을 예정)

        // 5. 다른 체크포인트는 전부 false, 이 체크포인트만 true
        foreach (CheckPoint cp in otherCheckPoints)
        {
            if (cp == null || cp == this) continue;
            cp.CheckPointSpot = false;
        }
        CheckPointSpot = true;

        OnCheckPointUsed?.Invoke();
    }
}
