using System.Reflection;
using UnityEngine;

// PlayerController / PlayerHitbox의 내부 상태를 읽기만 합니다. (팀원 파일은 고치지 않음)
// BreakableFloorSlamReceiver가 isDiving을 읽는 방식과 같습니다.
public static class PlayerStateReader
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    static readonly FieldInfo GroundedField = typeof(PlayerController).GetField("isGrounded", Flags);
    static readonly FieldInfo DivingField = typeof(PlayerController).GetField("isDiving", Flags);
    static readonly FieldInfo SpecialField = typeof(PlayerController).GetField("isSpecialAttacking", Flags);
    static readonly FieldInfo DashingField = typeof(PlayerController).GetField("isDashing", Flags);
    static readonly FieldInfo AttackingField = typeof(PlayerController).GetField("isAttacking", Flags);
    static readonly FieldInfo KnockedField = typeof(PlayerController).GetField("isKnockedBack", Flags);
    static readonly FieldInfo HitboxTypeField = typeof(PlayerHitbox).GetField("playerattackType", Flags);

    static bool Read(FieldInfo field, PlayerController player)
    {
        return field != null && player != null && (bool)field.GetValue(player);
    }

    public static bool IsGrounded(PlayerController p) { return Read(GroundedField, p); }
    public static bool IsDiving(PlayerController p) { return Read(DivingField, p); }
    public static bool IsSpecialAttacking(PlayerController p) { return Read(SpecialField, p); }

    // 대쉬·공격·특수공격·넉백 중이면 true (이때는 더블점프 안 받음)
    public static bool IsBusy(PlayerController p)
    {
        return Read(DashingField, p) || Read(AttackingField, p) || Read(SpecialField, p) || Read(KnockedField, p);
    }

    // C 낙하공격 히트박스인지 (아니면 X 일반공격)
    public static bool IsFallingHitbox(PlayerHitbox hitbox)
    {
        if (hitbox == null) return false;
        if (HitboxTypeField != null)
        {
            return (PlayerHitbox.PlayerAttackType)HitboxTypeField.GetValue(hitbox) == PlayerHitbox.PlayerAttackType.Falling;
        }
        return hitbox.GiveDamage() >= 30;
    }

    static readonly FieldInfo PhaseField = typeof(PlayerController).GetField("specialPhase", Flags);
    static readonly FieldInfo LandingRemainingField = typeof(PlayerController).GetField("landingRemaining", Flags);

    // C 낙하공격이 착지 후 멈춰 있는 단계(Landing)인지
    public static bool IsSpecialLanding(PlayerController p)
    {
        return PhaseField != null && p != null && PhaseField.GetValue(p).ToString() == "Landing";
    }

    // 착지 후 멈춤(프리팹 fallingHitboxLingerTime, 지금 1초)을 바로 끝내게 합니다.
    // PlayerController가 다음 FixedUpdate에서 스스로 정상 종료(중력 복구, 히트박스 정리)를 합니다. 파일은 안 고침.
    public static bool EndSpecialLandingNow(PlayerController p)
    {
        if (LandingRemainingField == null || !IsSpecialLanding(p)) return false;
        LandingRemainingField.SetValue(p, 0f);
        return true;
    }

    // 원인 찾기용: 땅·내려찍기 관련 내부 상태를 한 줄로
    public static string DebugState(PlayerController p)
    {
        if (p == null) return "(플레이어 없음)";
        FieldInfo landed = typeof(PlayerController).GetField("diveLanded", Flags);
        FieldInfo phase = typeof(PlayerController).GetField("specialPhase", Flags);
        return "땅=" + IsGrounded(p) + " 내려찍기=" + IsDiving(p) + " 착지=" + (landed != null ? landed.GetValue(p) : "?")
            + " 단계=" + (phase != null ? phase.GetValue(p) : "?") + " 특수공격중=" + IsSpecialAttacking(p);
    }

    public static PlayerController FindPlayer(Collider2D c)
    {
        if (c == null) return null;
        PlayerController p = c.GetComponentInParent<PlayerController>();
        if (p == null && c.attachedRigidbody != null) p = c.attachedRigidbody.GetComponent<PlayerController>();
        return p;
    }

    // 위에서 밟았는지 (콜백을 받는 쪽 기준, MovingPlatform과 같은 기준)
    public static bool IsFromTop(Collision2D collision, Collider2D self, float tolerance = 0.15f)
    {
        if (collision.collider.bounds.min.y < self.bounds.max.y - tolerance) return false;
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) return true;
        }
        return false;
    }
}
