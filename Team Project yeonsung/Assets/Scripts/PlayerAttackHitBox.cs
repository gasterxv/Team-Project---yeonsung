using UnityEngine;

public class PlayerHitbox : MonoBehaviour
{
    public enum PlayerAttackType
    {
        Common,
        Falling
    }

    [SerializeField] private PlayerAttackType playerattackType;
    [SerializeField] private int CommonAttack = 10;
    [SerializeField] private int FallingAttack = 30;

    public int GiveDamage()
    {
        if (playerattackType == PlayerAttackType.Falling)
            return FallingAttack;

        return CommonAttack;
    }
}