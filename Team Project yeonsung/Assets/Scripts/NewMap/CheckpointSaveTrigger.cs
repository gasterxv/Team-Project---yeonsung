using UnityEngine;

// 체크포인트 오브젝트에 같이 붙여서, 플레이어가 닿으면 맵 상태를 저장합니다.
// (부활 위치는 기존 CheckPoint 스크립트가 그대로 처리)
[RequireComponent(typeof(Collider2D))]
public class CheckpointSaveTrigger : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (PlayerStateReader.FindPlayer(other) == null) return;
        if (CheckpointResetManager.Instance == null) return;
        CheckpointResetManager.Instance.SaveAll();
        Debug.Log("[새맵] 체크포인트 저장: " + name);
    }
}
