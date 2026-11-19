using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 타이틀 화면 버튼 기능: Game Start / Setting / Quit
public class TitleMenu : MonoBehaviour
{
    public string gameSceneName = "Tutorial_Circulation";
    public string gameScenePath = "Assets/Scenes/Tutorial_Circulation.unity";   // 에디터에서 Build Settings에 없을 때 사용
    public GameObject settingPanel;

    void Start()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
    }

    public void OnGameStart()
    {
        GameStats.StartRun();   // 플레이 시간과 사망 횟수 기록 시작

        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

#if UNITY_EDITOR
        // Build Settings에 씬이 아직 없어도 에디터에서는 테스트할 수 있게 경로로 엽니다.
        Debug.LogWarning(gameSceneName + " 씬이 Build Settings에 없어서 에디터 전용 방식으로 열어요.");
        EditorSceneManager.LoadSceneInPlayMode(gameScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError(gameSceneName + " 씬을 Build Settings에 추가해주세요.");
#endif
    }

    public void OnSetting()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(!settingPanel.activeSelf);
        }
    }

    public void OnCloseSetting()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}