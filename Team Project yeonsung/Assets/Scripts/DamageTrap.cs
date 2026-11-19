using UnityEngine;

// 플레이어가 닿고 있는 동안 일정 간격으로 대미지를 주는 함정
public class DamageTrap : MonoBehaviour
{
    [Header("함정 설정")]
    public int damage = 10;             // 한 번에 주는 대미지
    public float damageInterval = 1f;   // 닿고 있을 때 대미지를 주는 간격(초)

    private float nextDamageTime;

    void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < nextDamageTime)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead || player.IsInvincible)
        {
            return;
        }

        player.TakeDamage(damage);
        nextDamageTime = Time.time + damageInterval;
        Debug.Log("함정에 닿았어요! 대미지: " + damage);
    }

    // Scene 화면에서 함정 범위를 빨간 테두리로 보여줍니다.
    void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}