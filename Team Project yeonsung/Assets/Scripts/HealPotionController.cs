using System.Collections;
using UnityEngine;

public interface IHealTarget
{
    bool TryHeal(int amount);
}

public class HealPotionController : MonoBehaviour
{
    [Header("회복 아이템 보유량")]
    [SerializeField] private int MaxRemainingHealPotion = 5;
    [SerializeField] private int RemainingHealPotion = 5;
    [SerializeField] private bool HealPotion = false;

    [Header("회복 설정")]
    [SerializeField] private float HpRecoveryTiming = 1f;
    [SerializeField] private int HpRecoveryValue = 1;

    [Header("플레이어 연결")]
    [SerializeField] private MonoBehaviour playerHealthComponent;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private MonoBehaviour[] controlsToDisable;

    private Coroutine healRoutine;
    private bool[] controlsWereEnabled;

    // 플레이어 조작 스크립트에서 F 키를 누르면 이 함수를 호출하세요.
    public void HealPotionUse()
    {
        if (HealPotion)
            return;

        if (RemainingHealPotion < 1)
        {
            HealPotion = false;
            Debug.Log("회복 아이템이 없습니다.");
            return;
        }

        if (!(playerHealthComponent is IHealTarget))
        {
            Debug.LogError("플레이어 체력 스크립트 연결이 필요합니다.");
            return;
        }

        HealPotion = true;
        DisablePlayerControls();

        if (playerAnimator != null)
            playerAnimator.SetTrigger("Heal");

        healRoutine = StartCoroutine(RecoverAfterDelay());
    }

    private IEnumerator RecoverAfterDelay()
    {
        yield return new WaitForSeconds(HpRecoveryTiming);

        if (!HealPotion)
            yield break;

        IHealTarget target = playerHealthComponent as IHealTarget;

        // HP가 실제로 회복된 경우에만 아이템을 1개 사용합니다.
        if (target != null && target.TryHeal(HpRecoveryValue))
        {
            RemainingHealPotion--;
            Debug.Log("회복 성공. 남은 개수: " + RemainingHealPotion);
        }
        else
        {
            Debug.Log("회복되지 않아 아이템을 사용하지 않았습니다.");
        }

        healRoutine = null;
    }

    // 회복 애니메이션 마지막 프레임의 Animation Event에서 호출하세요.
    public void HealAnimationFinished()
    {
        EndHealing();
    }

    // 피격 스크립트에서 회복을 취소할 때 호출하세요.
    public void CancelHealPotion()
    {
        if (!HealPotion)
            return;

        if (healRoutine != null)
        {
            StopCoroutine(healRoutine);
            healRoutine = null;
        }

        if (playerAnimator != null)
            playerAnimator.ResetTrigger("Heal");

        EndHealing();
    }

    private void EndHealing()
    {
        HealPotion = false;
        RestorePlayerControls();
    }

    private void DisablePlayerControls()
    {
        if (controlsToDisable == null)
            return;

        controlsWereEnabled = new bool[controlsToDisable.Length];

        for (int i = 0; i < controlsToDisable.Length; i++)
        {
            if (controlsToDisable[i] == null)
                continue;

            controlsWereEnabled[i] = controlsToDisable[i].enabled;
            controlsToDisable[i].enabled = false;
        }
    }

    private void RestorePlayerControls()
    {
        if (controlsToDisable == null || controlsWereEnabled == null)
            return;

        for (int i = 0; i < controlsToDisable.Length; i++)
        {
            if (controlsToDisable[i] != null)
                controlsToDisable[i].enabled = controlsWereEnabled[i];
        }

        controlsWereEnabled = null;
    }
    public void RefillAtCheckpoint()
    {
        RemainingHealPotion = MaxRemainingHealPotion;
        Debug.Log("회복 아이템을 " + RemainingHealPotion + "개로 보충했습니다.");
    }
}