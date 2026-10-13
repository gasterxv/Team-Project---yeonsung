using System.Collections.Generic;
using UnityEngine;

public class EnemySensor : MonoBehaviour
{
    public enum SensorType
    {
        PlayerRange, // 플레이어 인식 또는 공격 범위
        Hurtbox      // 플레이어 공격을 받는 범위
    }

    [SerializeField] private EnemyBase owner;
    [SerializeField] private SensorType sensorType;

    // 프로젝트에 실제로 등록한 공격 태그 이름으로 설정
    [SerializeField] private string playerAttackTag = "PlayerAttack";

    // 플레이어에게 콜라이더가 여러 개 있어도 구별해서 기록
    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();
    public bool HasPlayer
    {
        get
        {
            // 삭제되거나 비활성화된 콜라이더는 기록에서 제거
            playerColliders.RemoveWhere(
                collider => collider == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
            );

            return playerColliders.Count > 0;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (sensorType == SensorType.PlayerRange)
        {
            if (other.CompareTag("Player"))
            {
                playerColliders.Add(other);
            }
        }
        else if (other.CompareTag(playerAttackTag))
        {
            owner.NotifyPlayerAttack();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
    }
}