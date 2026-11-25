using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerHUDDisplay : MonoBehaviour
{
    private const int HealthSlotCount = 3;

    [Header("화면 왼쪽 위 HUD")]
    [SerializeField] private Vector2 topLeftOffset = new Vector2(28f, 24f);

    private Canvas hudCanvas;
    private Image capsuleIcon;
    private Text capsuleCountText;
    private readonly Image[] healthSlots = new Image[HealthSlotCount];
    private Image vaccineIcon;
    private PlayerController player;
    private NanoCapsuleUse capsuleInventory;
    private Sprite hpSprite;
    private Sprite hpBarSprite;
    private Sprite recoverySprite;
    private Sprite vaccineMissingSprite;
    private Sprite vaccineAcquiredSprite;

    private void OnEnable()
    {
        LoadSprites();
        EnsureHudObjects();
        RefreshDisplay();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        if (capsuleInventory == null)
        {
            capsuleInventory = player != null
                ? player.GetComponentInChildren<NanoCapsuleUse>(true)
                : FindAnyObjectByType<NanoCapsuleUse>();
        }

        RefreshDisplay();
    }

    private void LoadSprites()
    {
        hpSprite = Resources.Load<Sprite>("image/UI/hp");
        hpBarSprite = Resources.Load<Sprite>("image/UI/hp창");
        recoverySprite = Resources.Load<Sprite>("image/아이템/회복");
        vaccineMissingSprite = Resources.Load<Sprite>("image/UI/백신미획득");
        vaccineAcquiredSprite = Resources.Load<Sprite>("image/UI/백신획득");
    }

    private void EnsureHudObjects()
    {
        Transform canvasTransform = transform.Find("PlayerHUDCanvas");
        if (canvasTransform == null)
        {
            GameObject canvasObject = new GameObject(
                "PlayerHUDCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasTransform = canvasObject.transform;
        }

        hudCanvas = canvasTransform.GetComponent<Canvas>();
        if (hudCanvas == null)
            hudCanvas = canvasTransform.gameObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasTransform.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = GetOrCreateRect(canvasTransform, "HUDRoot");
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(topLeftOffset.x, -topLeftOffset.y);
        root.sizeDelta = new Vector2(510f, 88f);

        Image background = GetOrCreateImage(root, "HPBarBackground");
        background.sprite = hpBarSprite;
        background.preserveAspect = false;
        SetRect(background.rectTransform, new Vector2(420f, 80f), Vector2.zero);

        capsuleIcon = GetOrCreateImage(root, "NanoRepairCapsuleIcon");
        capsuleIcon.sprite = recoverySprite;
        SetRect(capsuleIcon.rectTransform, new Vector2(42f, 42f), new Vector2(11f, -19f));

        capsuleCountText = GetOrCreateText(root, "NanoRepairCapsuleCount");
        SetRect(capsuleCountText.rectTransform, new Vector2(34f, 30f), new Vector2(43f, -26f));
        capsuleCountText.fontSize = 22;
        capsuleCountText.fontStyle = FontStyle.Bold;
        capsuleCountText.alignment = TextAnchor.MiddleCenter;
        capsuleCountText.color = new Color(0.05f, 0.24f, 0.25f, 1f);
        capsuleCountText.raycastTarget = false;

        for (int i = 0; i < HealthSlotCount; i++)
        {
            string slotName = "HP" + (i + 1);
            bool isNewSlot = root.Find(slotName) == null;
            healthSlots[i] = GetOrCreateImage(root, slotName);
            healthSlots[i].sprite = hpSprite;

            // Keep manually adjusted RectTransforms when entering Play Mode.
            if (isNewSlot)
            {
                float defaultX = i == 0 ? 127.1f : i == 1 ? 223.9f : 320.2f;
                SetRect(healthSlots[i].rectTransform, new Vector2(50f, 50f), new Vector2(defaultX, -20.3f));
            }
        }

        vaccineIcon = GetOrCreateImage(root, "VaccineStatus");
        SetRect(vaccineIcon.rectTransform, new Vector2(58f, 58f), new Vector2(433f, -10f));
    }

    private RectTransform GetOrCreateRect(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        if (child != null)
            return child as RectTransform;

        GameObject childObject = new GameObject(objectName, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject.GetComponent<RectTransform>();
    }

    private Image GetOrCreateImage(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            image = imageObject.GetComponent<Image>();
        }

        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private Text GetOrCreateText(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        Text text = child != null ? child.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<Text>();
        }

        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.raycastTarget = false;
        return text;
    }

    private void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void RefreshDisplay()
    {
        if (capsuleCountText != null)
        {
            int capsuleCount = 3;
            if (Application.isPlaying)
            {
                if (capsuleInventory != null)
                    capsuleCount = capsuleInventory.RemainingCount;
                else if (player != null)
                    capsuleCount = player.RemainingHealPotion;
            }
            capsuleCountText.text = "×" + Mathf.Max(0, capsuleCount);
        }

        int currentHealth = Application.isPlaying && player != null ? player.CurrentHealth : HealthSlotCount;
        for (int i = 0; i < healthSlots.Length; i++)
        {
            if (healthSlots[i] == null)
                continue;

            healthSlots[i].sprite = hpSprite;
            healthSlots[i].enabled = i < currentHealth && hpSprite != null;
        }

        if (vaccineIcon != null)
        {
            vaccineIcon.sprite = VaccineController.IsVaccine ? vaccineAcquiredSprite : vaccineMissingSprite;
            vaccineIcon.enabled = vaccineIcon.sprite != null;
        }
    }

    // InfectionHeart의 onHeartBroken UnityEvent에 연결합니다.
    public void OnInfectionHeartBroken()
    {
        VaccineController.AcquireVaccine();
        RefreshDisplay();
    }
}






