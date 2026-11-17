using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingButton : MonoBehaviour
{
    [Header("Project 창의 SettingPanel 프리팹")]
    [SerializeField] private GameObject settingPanelPrefab;

    [Header("삭제할 SettingUI - 비우면 부모 Canvas를 사용")]
    [SerializeField] private GameObject settingUI;

    private Button button;
    private bool isOpening;

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
        if (isOpening) return;

        if (settingPanelPrefab == null)
        {
            Debug.LogError("SettingPanel 프리팹을 연결해주세요.", this);
            return;
        }

        if (settingUI == null)
        {
            Debug.LogError("삭제할 SettingUI를 연결해주세요.", this);
            return;
        }

        isOpening = true;

        // 기존 UI의 자식으로 만들지 않고 별도로 생성합니다.
        GameObject newPanel = Instantiate(settingPanelPrefab);
        newPanel.SetActive(true);

        // 새 UI 생성 후 기존 SettingUI를 삭제합니다.
        settingUI.SetActive(false);
        Destroy(settingUI);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenSettingPanel);
        }
    }
}