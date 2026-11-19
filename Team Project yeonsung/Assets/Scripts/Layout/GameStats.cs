using UnityEngine;
using UnityEngine.SceneManagement;

// 한 판 동안의 기록(플레이 시간, 사망 횟수)을 저장하는 곳
// 씬이 바뀌어도 값이 유지됩니다. (static 이라서 오브젝트에 붙일 필요 없음)
public static class GameStats
{
    public static int deathCount = 0;        // 사망 횟수
    public static float playTime = 0f;       // 클리어까지 걸린 시간(초)
    public static string resultSceneName = "ResultScene";   // 결과 화면 씬 이름

    private static float startRealTime = 0f; // 시작한 시각

    // 게임을 켤 때 자동으로 한 번 실행됩니다.
    // PlayerController 가 죽을 때 알려주는 이벤트를 받아서 사망 횟수를 셉니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        deathCount = 0;
        playTime = 0f;
        startRealTime = Time.realtimeSinceStartup;   // 타이틀에서 시작을 안 눌러도 대비

        PlayerController.OnPlayerDeath -= OnPlayerDeath;   // 중복 등록 방지
        PlayerController.OnPlayerDeath += OnPlayerDeath;
    }

    private static void OnPlayerDeath()
    {
        deathCount++;
    }

    // 새 게임을 시작할 때 호출 (기록 초기화 + 시간 재기 시작)
    public static void StartRun()
    {
        deathCount = 0;
        playTime = 0f;
        startRealTime = Time.realtimeSinceStartup;
    }

    // 클리어했을 때 호출 : 시간 재기를 끝내고 결과 화면으로 이동
    public static void ClearGame()
    {
        playTime = Time.realtimeSinceStartup - startRealTime;

        Time.timeScale = 1f;

        if (!Application.CanStreamedLevelBeLoaded(resultSceneName))
        {
            Debug.LogError("결과 씬 이름과 Build Profiles의 Scene List를 확인해주세요: " + resultSceneName);
            return;
        }

        SceneManager.LoadScene(resultSceneName);
    }

    // 초를 "00:00:00" 글자로 바꿔줌
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
    }
}