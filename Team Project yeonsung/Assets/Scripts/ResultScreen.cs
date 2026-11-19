using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultScreen : MonoBehaviour
{
    [Header("글자 연결")]
    public TMP_Text timeValueText;     // TimeValue
    public TMP_Text deathValueText;    // DeathValue

    [Header("버튼 연결")]
    public Button returnButton;        // ReturnButton
    public string titleSceneName = "Title";   // 돌아갈 타이틀 씬 이름

    [Header("테스트용 (화면 확인할 때만 사용)")]
    public bool useTestValue = true;   // 체크하면 아래 값으로 표시 (실제 게임에서는 꼭 끄기)
    public float testSeconds = 6157f;  // 01:42:37
    public int testDeathCount = 23;

    void Start()
    {
        float seconds = useTestValue ? testSeconds : GameStats.playTime;
        int deaths = useTestValue ? testDeathCount : GameStats.deathCount;

        if (timeValueText != null)
        {
            timeValueText.text = GameStats.FormatTime(seconds);
        }

        if (deathValueText != null)
        {
            deathValueText.text = deaths.ToString() + "회";
        }

        if (returnButton != null)
        {
            returnButton.onClick.AddListener(GoToTitle);
        }
    }

    // 타이틀 씬으로 돌아가기
    public void GoToTitle()
    {
        Time.timeScale = 1f;   // 혹시 멈춰 있었다면 풀어줌

        if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Debug.LogError("타이틀 씬 이름과 Build Profiles의 Scene List를 확인해주세요: " + titleSceneName);
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }
}