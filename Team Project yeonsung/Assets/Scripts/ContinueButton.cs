using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class Continue : MonoBehaviour
{
    public enum ContinueType
    {
        ResumeGame,
        RespawnAtCheckPoint
    }

    [Header("이 버튼의 역할")]
    [SerializeField] private ContinueType continueType;

    private Button button;
    private GameObject parentCanvasObject;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnContinueClicked);

        Canvas parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas != null)
        {
            parentCanvasObject = parentCanvas.rootCanvas.gameObject;
        }
    }

    private void OnContinueClicked()
    {
        GameFlowManager manager = GameFlowManager.Instance;

        if (manager == null)
        {
            Debug.LogError("씬에 GameFlowManager가 없습니다.", this);
            return;
        }

        switch (continueType)
        {
            case ContinueType.ResumeGame:
                // 게임매니저의 일시정지 상태와 플레이어 조작 복구
                manager.CloseSettings();

                // 별도로 생성한 SettingPanel Canvas도 삭제
                RemoveParentCanvas();
                break;

            case ContinueType.RespawnAtCheckPoint:
                // 부활 성공 시 게임매니저가 패배 Canvas를 삭제합니다.
                manager.RestartFromCheckPoint();
                break;
        }
    }

    private void RemoveParentCanvas()
    {
        if (parentCanvasObject == null) return;

        parentCanvasObject.SetActive(false);
        Destroy(parentCanvasObject);
        parentCanvasObject = null;
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnContinueClicked);
        }
    }
}