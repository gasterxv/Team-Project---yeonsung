using System.Collections;
using UnityEngine;

public class NanoCapsuleUse : MonoBehaviour
{
    [Header("나노 수리 캡슐 인벤토리")]
    [SerializeField] private int MaxRemainingHealPotion = 5;
    [SerializeField] private int RemainingHealPotion = 5;
    [SerializeField] private bool HealPotion = false;

    [Header("회복 설정")]
    [SerializeField] private float HpRecoveryTiming = 1f;
    [SerializeField] private int HpRecoveryValue = 1;
    [SerializeField] private KeyCode useKey = KeyCode.F;

    private PlayerController player;
    private Coroutine healRoutine;

    public int RemainingCount => RemainingHealPotion;

    private void Update()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        if (Input.GetKeyDown(useKey))
            HealPotionUse();
    }

    public void HealPotionUse()
    {
        if (HealPotion)
            return;

        if (RemainingHealPotion < 1)
        {
            Debug.Log("나노 수리 캡슐이 없습니다.");
            return;
        }

        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning("플레이어가 실행 중인 씬에 없습니다.");
            return;
        }

        HealPotion = true;
        healRoutine = StartCoroutine(RecoverAfterDelay());
    }

    private IEnumerator RecoverAfterDelay()
    {
        int healthBefore = player.CurrentHealth;
        yield return new WaitForSeconds(HpRecoveryTiming);

        if (player == null || player.CurrentHealth < healthBefore)
        {
            HealPotion = false;
            healRoutine = null;
            Debug.Log("피격되어 나노 수리 캡슐 사용이 취소되었습니다.");
            yield break;
        }

        player.Heal(HpRecoveryValue);

        if (player.CurrentHealth > healthBefore)
        {
            RemainingHealPotion--;
            Debug.Log("HP를 회복했습니다. 남은 나노 수리 캡슐: " + RemainingHealPotion);
        }
        else
        {
            Debug.Log("체력이 최대이거나 회복할 수 없는 상태라 캡슐을 사용하지 않았습니다.");
        }

        HealPotion = false;
        healRoutine = null;
    }

    public void RefillAtCheckpoint()
    {
        RemainingHealPotion = MaxRemainingHealPotion;
        Debug.Log("체크포인트에서 나노 수리 캡슐을 " + RemainingHealPotion + "개로 충전했습니다.");
    }
}