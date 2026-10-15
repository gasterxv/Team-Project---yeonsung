using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    public enum EnemyAttackType
    {
        Pattern1,
        Pattern2,
        Pattern3,
        Pattern4
    }

    [SerializeField] private EnemyAttackType enemyAttackType;
    [SerializeField] private int Pattern1 = 10;
    [SerializeField] private int Pattern2 = 10;
    [SerializeField] private int Pattern3 = 10;
    [SerializeField] private int Pattern4 = 10;

    public int GiveDamage()
    {
        switch (enemyAttackType)
        {
            case EnemyAttackType.Pattern1:
                return Pattern1;

            case EnemyAttackType.Pattern2:
                return Pattern2;

            case EnemyAttackType.Pattern3:
                return Pattern3;

            case EnemyAttackType.Pattern4:
                return Pattern4;

            default:
                return 0;
        }
    }
}