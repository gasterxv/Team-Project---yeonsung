using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class QuitButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(QuitGame);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        // Unity 에디터에서는 플레이 모드를 종료합니다.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드한 게임에서는 프로그램을 종료합니다.
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(QuitGame);
        }
    }
}