using UnityEngine;
using UnityEngine.UI;

// HUD 왼쪽 위 나노 캡슐 아이콘(캡슐 그림)만 숨기고 "×숫자"는 남깁니다.
// 팀원 PlayerHUDDisplay는 고치지 않음: 그 코드가 만든 'NanoRepairCapsuleIcon' 그림만 꺼 둠.
public class HudCapsuleIconHider : MonoBehaviour
{
    const string IconName = "NanoRepairCapsuleIcon";
    Image icon;

    void LateUpdate()
    {
        if (icon == null)
        {
            foreach (Image img in FindObjectsByType<Image>(FindObjectsSortMode.None))
                if (img.name == IconName) { icon = img; break; }
            if (icon == null) return;
        }
        if (icon.enabled) icon.enabled = false;
    }
}
