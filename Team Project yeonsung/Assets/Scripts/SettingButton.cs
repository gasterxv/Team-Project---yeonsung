using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingButton : MonoBehaviour
{
    [Header("Project 창의 SettingPanel 프리팹")]
    [SerializeField] private GameObject settingPanelPrefab;

    [Header("기존 UI 삭제 여부")]
    [SerializeField] private bool deleteSettingUI = true;

    [Header("삭제할 SettingUI - 비우면 부모 Canvas 사용")]
    [SerializeField] private GameObject settingUI;

    private Button button;
    private GameObject openedPanel;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OpenSettingPanel);

        if (settingUI == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas != null)
            {
                settingUI = parentCanvas.gameObject;
            }
        }
    }

    public void OpenSettingPanel()
    {
        // 기존 UI를 유지하더라도 패널이 중복 생성되지 않게 합니다.
        // 열린 패널이 삭제되면 다시 생성할 수 있습니다.
        if (openedPanel != null) return;

        if (settingPanelPrefab == null)
        {
            Debug.LogError("SettingPanel 프리팹을 연결해주세요.", this);
            return;
        }

        if (deleteSettingUI && settingUI == null)
        {
            Debug.LogError("삭제할 SettingUI를 연결해주세요.", this);
            return;
        }

        // 기존 Canvas를 삭제해도 새 패널이 함께 삭제되지 않도록
        // 별도 오브젝트로 생성합니다.
        openedPanel = Instantiate(settingPanelPrefab);
        openedPanel.SetActive(true);

        if (deleteSettingUI)
        {
            settingUI.SetActive(false);
            Destroy(settingUI);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenSettingPanel);
        }
    }
}