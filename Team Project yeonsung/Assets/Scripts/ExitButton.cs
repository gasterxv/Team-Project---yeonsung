using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TitleSceneButton : MonoBehaviour
{
    [SerializeField] private string titleSceneName = "Title";
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(GoToTitle);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(GoToTitle);
    }

    private void GoToTitle()
    {
        // 일시정지 상태에서 눌러도 타이틀에서는 정상 속도로 동작
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }
}