using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }
    public static event System.Action OnPlayerRespawn;

    [Header("플레이어 부활")]
    public GameObject playerPrefab;
    [SerializeField] private string checkPointTag = "CheckPoint";
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 0.5f);

    [Header("패배 Canvas 프리팹")]
    [SerializeField] private Canvas defeatCanvasPrefab;

    [Header("패배 Canvas 안의 버튼 이름")]
    [SerializeField] private string restartButtonName = "RetryButton";
    [SerializeField] private string titleButtonName = "ExitButton";

    [Header("사망 연출 대기 시간")]
    [SerializeField] private float deathAnimTime = 1f;

    [Header("타이틀 씬 이름")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("ESC 설정 Canvas 프리팹")]
    [SerializeField] private Canvas settingCanvasPrefab;

    private Canvas defeatCanvasInstance;
    private Canvas settingCanvasInstance;
    private Button restartButton;
    private Button titleButton;

    private PlayerController deadPlayer;
    private PlayerController pausedPlayer;
    private bool playerWasEnabled;
    private bool settingsOpen;
    private float timeScaleBeforeSettings = 1f;

    private Vector3 initialSpawnPosition;
    private bool isProcessing;
    private bool isChoosing;
    private bool isChanging;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        PlayerController player =
            FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            initialSpawnPosition = player.transform.position;
        }
    }

    private void Update()
    {
        if (Instance != this) return;
        if (isProcessing || isChoosing || isChanging) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsOpen)
                CloseSettings();
            else
                OpenSettings();
        }
    }

    public void OpenSettings()
    {
        if (settingsOpen || isProcessing || isChoosing || isChanging)
            return;

        if (settingCanvasPrefab == null)
        {
            Debug.LogError("Setting Canvas Prefab을 연결해주세요.", this);
            return;
        }

        settingCanvasInstance = Instantiate(settingCanvasPrefab);
        PrepareCanvas(settingCanvasInstance, 900);

        // 설정창이 열려 있는 동안 플레이어의 입력 처리를 중단합니다.
        pausedPlayer = FindFirstObjectByType<PlayerController>();

        if (pausedPlayer != null)
        {
            playerWasEnabled = pausedPlayer.enabled;
            pausedPlayer.enabled = false;
        }

        timeScaleBeforeSettings = Time.timeScale;
        settingsOpen = true;
        Time.timeScale = 0f;
    }

    public void CloseSettings()
    {
        if (!settingsOpen) return;

        if (settingCanvasInstance != null)
        {
            settingCanvasInstance.gameObject.SetActive(false);
            Destroy(settingCanvasInstance.gameObject);
            settingCanvasInstance = null;
        }

        if (pausedPlayer != null)
        {
            pausedPlayer.enabled = playerWasEnabled;
        }

        pausedPlayer = null;
        settingsOpen = false;
        Time.timeScale = timeScaleBeforeSettings;
    }

    // 기존 PlayerController.PlayerDeath()에서 호출합니다.
    public void BeginDeathSequence(PlayerController player)
    {
        if (isProcessing || player == null) return;

        CloseSettings();

        isProcessing = true;
        deadPlayer = player;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, deathAnimTime));

        if (!CreateDefeatUI())
        {
            isProcessing = false;
            yield break;
        }

        isChoosing = true;
        Time.timeScale = 0f;
    }

    private bool CreateDefeatUI()
    {
        if (defeatCanvasPrefab == null)
        {
            Debug.LogError("Defeat Canvas Prefab을 연결해주세요.", this);
            return false;
        }

        defeatCanvasInstance = Instantiate(defeatCanvasPrefab);
        PrepareCanvas(defeatCanvasInstance, 1000);

        restartButton = FindButton(restartButtonName);
        titleButton = FindButton(titleButtonName);

        if (restartButton == null ||
            titleButton == null ||
            restartButton == titleButton)
        {
            Debug.LogError(
                $"패배 UI 버튼 이름을 확인해주세요: " +
                $"{restartButtonName}, {titleButtonName}", this);

            RemoveDefeatUI();
            return false;
        }

        
        titleButton.onClick.AddListener(GoToTitle);

        SetButtonsInteractable(true);
        return true;
    }

    private void PrepareCanvas(Canvas canvas, int order)
    {
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        canvas.transform.localScale = Vector3.one;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private Button FindButton(string buttonName)
    {
        Button[] buttons =
            defeatCanvasInstance.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.gameObject.name == buttonName)
                return button;
        }

        return null;
    }

    public void RestartFromCheckPoint()
    {
        if (!isChoosing || isChanging) return;

        if (playerPrefab == null ||
            !playerPrefab.activeSelf ||
            !playerPrefab.TryGetComponent<PlayerController>(
                out var prefabController) ||
            !prefabController.enabled)
        {
            Debug.LogError(
                "루트에 활성 PlayerController가 있는 활성 플레이어 " +
                "프리팹을 Player Prefab에 연결해주세요.", this);
            return;
        }

        isChanging = true;
        SetButtonsInteractable(false);

        Vector3 spawnPosition = GetRespawnPosition();

        if (deadPlayer != null)
        {
            deadPlayer.gameObject.SetActive(false);
            Destroy(deadPlayer.gameObject);
            deadPlayer = null;
        }

        GameObject newPlayer = Instantiate(
            playerPrefab, spawnPosition, Quaternion.identity);

        PlayerController controller =
            newPlayer.GetComponent<PlayerController>();

        controller.RestoreFull();

        RemoveDefeatUI();

        isChoosing = false;
        isProcessing = false;
        Time.timeScale = 1f;

        try
        {
            OnPlayerRespawn?.Invoke();
        }
        finally
        {
            isChanging = false;
        }
    }

    // 패배 UI 또는 설정창이 열린 상태에서 사용할 수 있습니다.
    public void GoToTitle()
    {
        if (isChanging || (!isChoosing && !settingsOpen)) return;

        if (string.IsNullOrWhiteSpace(titleSceneName) ||
            !Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Debug.LogError(
                "타이틀 씬 이름과 Build Profiles의 Scene List를 확인해주세요.",
                this);
            return;
        }

        isChanging = true;
        SetButtonsInteractable(false);
        CloseSettings();

        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    private Vector3 GetRespawnPosition()
    {
        try
        {
            GameObject[] spots =
                GameObject.FindGameObjectsWithTag(checkPointTag);

            foreach (GameObject spot in spots)
            {
                CheckPoint checkPoint = spot.GetComponent<CheckPoint>();

                if (checkPoint != null && checkPoint.checkPointSpot)
                {
                    return spot.transform.position + (Vector3)spawnOffset;
                }
            }
        }
        catch (UnityException)
        {
            Debug.LogWarning(
                "체크포인트 태그가 없어 최초 위치에서 부활합니다.", this);
        }

        return initialSpawnPosition;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (restartButton != null)
            restartButton.interactable = interactable;

        if (titleButton != null)
            titleButton.interactable = interactable;
    }

    private void RemoveDefeatUI()
    {
        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartFromCheckPoint);

        if (titleButton != null)
            titleButton.onClick.RemoveListener(GoToTitle);

        if (defeatCanvasInstance != null)
        {
            defeatCanvasInstance.gameObject.SetActive(false);
            Destroy(defeatCanvasInstance.gameObject);
        }

        restartButton = null;
        titleButton = null;
        defeatCanvasInstance = null;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        CloseSettings();
        RemoveDefeatUI();

        if (isChoosing)
            Time.timeScale = 1f;

        Instance = null;
    }
}