using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 사망 후의 연출(게임오버 UI -> 플레이어 제거 -> 암전 -> 체크포인트 부활)을 맡는 씬 관리 스크립트
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("플레이어 부활 설정")]
    public GameObject playerPrefab;                     // 부활할 때 생성할 플레이어 프리팹
    public string checkPointTag = "CheckPoint";         // 체크포인트 태그
    public Vector2 spawnOffset = new Vector2(0f, 0.5f); // 체크포인트 위치에서 얼마나 위에 생성할지 (땅에 끼지 않게)

    [Header("사망 연출 시간")]
    public float deathAnimTime = 1f;        // 죽음 애니메이션을 기다리는 시간(초)
    public float gameOverShowTime = 5f;     // T: GameOverUI가 떠 있는 시간(초)
    public float fadeOutTime = 1f;          // N: 암전 시간(투명도 0 -> 1)
    public float blackHoldTime = 0.5f;      // 완전히 검은 상태로 유지하는 시간(초)
    public float fadeInTime = 1f;           // 부활 후 암전이 걷히는 시간(투명도 1 -> 0)

    [Header("게임오버 UI")]
    public GameObject gameOverUIPrefab;     // 비워두면 기본 "GAME OVER" 글자를 만들어 씁니다. (나중에 직접 만든 UI를 넣을 수 있어요)
    public string gameOverText = "GAME OVER";
    public int gameOverFontSize = 80;
    public Color gameOverTextColor = Color.red;

    private Image fadeImage;                // 화면 전체를 덮는 검은색 이미지
    private bool isProcessing;              // 사망 연출이 진행 중인지
    private Vector3 initialSpawnPosition;   // 체크포인트가 없을 때 쓸 처음 플레이어 위치

    void Awake()
    {
        Instance = this;
        CreateFadeUI();
    }

    void Start()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            initialSpawnPosition = player.transform.position;
        }
    }

    // PlayerController.PlayerDeath()가 호출합니다.
    public void BeginDeathSequence(PlayerController player)
    {
        if (isProcessing)
        {
            return;
        }

        isProcessing = true;
        StartCoroutine(DeathRoutine(player));
    }

    IEnumerator DeathRoutine(PlayerController player)
    {
        // 1) 죽음 애니메이션이 재생되는 시간
        yield return new WaitForSeconds(deathAnimTime);

        // 2) GameOverUI를 만들고 T초 뒤에 지웁니다.
        GameObject gameOverUI = CreateGameOverUI();
        yield return new WaitForSeconds(gameOverShowTime);
        Destroy(gameOverUI);

        // 3) 플레이어 캐릭터 제거
        if (player != null)
        {
            Destroy(player.gameObject);
        }

        // 4) 암전: N초 동안 투명도 0 -> 1
        yield return StartCoroutine(Fade(0f, 1f, fadeOutTime));

        // 5) 체크포인트에 플레이어 생성 + 체력/회복약 최대로
        if (playerPrefab != null)
        {
            Vector3 spawnPosition = GetCheckPointPosition() + (Vector3)spawnOffset;
            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

            PlayerController controller = newPlayer.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.RestoreFull();
            }
        }
        else
        {
            Debug.LogError("GameFlowManager의 Player Prefab 칸이 비어 있어서 플레이어를 만들 수 없어요!");
        }

        yield return new WaitForSeconds(blackHoldTime);

        // 6) 암전 해제 (투명도 1 -> 0)
        yield return StartCoroutine(Fade(1f, 0f, fadeInTime));

        isProcessing = false;
    }

    // 태그가 체크포인트이고 Check Point Spot이 true인 오브젝트의 위치를 찾습니다.
    Vector3 GetCheckPointPosition()
    {
        try
        {
            GameObject[] spots = GameObject.FindGameObjectsWithTag(checkPointTag);
            foreach (GameObject spot in spots)
            {
                CheckPoint checkPoint = spot.GetComponent<CheckPoint>();
                if (checkPoint != null && checkPoint.checkPointSpot)
                {
                    return spot.transform.position;
                }
            }
        }
        catch (UnityException)
        {
            Debug.LogWarning("'" + checkPointTag + "' 태그가 아직 만들어지지 않았어요. 처음 위치에서 부활합니다.");
            return initialSpawnPosition;
        }

        Debug.LogWarning("Check Point Spot이 켜진 체크포인트가 없어서 처음 위치에서 부활합니다.");
        return initialSpawnPosition;
    }

    // ---------------- UI ----------------

    // 암전용 검은색 이미지를 만듭니다. (평소에는 투명)
    void CreateFadeUI()
    {
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // 게임오버 UI보다 위에 그려집니다.

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;

        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    GameObject CreateGameOverUI()
    {
        // 직접 만든 UI 프리팹이 있으면 그걸 씁니다.
        if (gameOverUIPrefab != null)
        {
            return Instantiate(gameOverUIPrefab);
        }

        GameObject root = new GameObject("GameOverUI");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        GameObject textObj = new GameObject("GameOverText");
        textObj.transform.SetParent(root.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = gameOverText;
        text.fontSize = gameOverFontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = gameOverTextColor;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return root;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (t < safeDuration)
        {
            t += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(from, to, t / safeDuration));
            yield return null;
        }

        SetFadeAlpha(to);
    }

    void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}