using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BreakableFloor : MonoBehaviour
{
    [Header("부서지는 바닥 설정")]
    [SerializeField, Min(1)] private int brokenFloorHP = 2;
    [SerializeField, Min(1)] private int minimumDamageToBreak = 2;

    private bool isBroken;

    // PlayerController의 공격 OverlapBox가 이 함수를 호출합니다.
    public void TakeDamage(int damage)
    {
        if (isBroken || damage < minimumDamageToBreak)
            return;

        brokenFloorHP -= damage;
        if (brokenFloorHP < 0)
        {
            isBroken = true;
            Destroy(gameObject);
        }
    }
}