using UnityEngine;

public class JabMob1 : EnemyBase
{
    protected override void Death()
    {
        gameObject.SetActive(false);
    }

    protected override void GetDamage()
    {
    }

    protected override void Attack()
    {
    }

    protected override void Walk()
    {
    }

    protected override void Idle()
    {
    }
}
