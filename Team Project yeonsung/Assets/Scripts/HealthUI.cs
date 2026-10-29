using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 체력 칸(하트)과 회복약 개수를 화면에 보여주는 UI.
// 플레이어가 죽었다가 다시 생겨도 이 오브젝트는 씬에 계속 남아 있어서 새 플레이어를 자동으로 찾아 연결합니다.
public class HealthUI : MonoBehaviour
{
    [Header("체력 칸 이미지 (나중에 이미지를 넣는 칸)")]
    public Sprite fullHeartSprite;      // 남은 체력 칸 이미지 (비워두면 색 네모)
    public Sprite emptyHeartSprite;     // 사라진 체력 칸 이미지 (비워두면 어두운 색)

    [Header("임시 색 (이미지가 없을 때)")]
    public Color fullColor = new Color(0.9f, 0.15f, 0.2f);          // 남은 칸 색
    public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);    // 빈 칸 색

    [Header("위치 / 크기")]
    public Vector2 position = new Vector2(10f, 150f);   // 첫 번째 칸 위치 (화면 왼쪽 위 기준)
    public Vector2 heartSize = new Vector2(36f, 36f);   // 칸 하나의 크기
    public float heartSpacing = 8f;                     // 칸 사이 간격

    [Header("회복약 글자")]
    public bool showPotionText = true;                  // 체력 칸 아래에 회복약 개수 표시
    public int potionFontSize = 20;
    public Color potionTextColor = Color.white;

    private PlayerController player;
    private readonly List<Image> hearts = new List<Image>();
    private RectTransform root;
    private Text potionText;
    private int builtMaxHearts = -1;
    private int lastMaxHearts = 4;

    void Awake()
    {
        CreateCanvas();
    }

    void Update()
    {
        // 플레이어가 없거나(죽어서 지워짐) 새로 생기면 다시 찾습니다.
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        int maxHearts = (player != null) ? player.MaxHealth : lastMaxHearts;
        int current = (player != null) ? player.CurrentHealth : 0;
        lastMaxHearts = maxHearts;

        if (maxHearts != builtMaxHearts)
        {
            BuildHearts(maxHearts);
        }

        for (int i = 0; i < hearts.Count; i++)
        {
            ApplyHeartLook(hearts[i], i < current);
        }

        // 위치/크기를 Inspector에서 바꿔도 바로 반영됩니다.
        UpdateLayout();

        if (potionText != null)
        {
            potionText.gameObject.SetActive(showPotionText);

            if (player != null)
            {
                potionText.text = "Potion " + player.RemainingHealPotion + " / " + player.MaxRemainingHealPotion;
            }
        }
    }

    void CreateCanvas()
    {
        GameObject canvasObj = new GameObject("HealthCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 게임오버, 암전 UI보다 아래에 그려집니다.

        GameObject rootObj = new GameObject("HeartRoot");
        rootObj.transform.SetParent(canvasObj.transform, false);
        root = rootObj.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);

        GameObject textObj = new GameObject("PotionText");
        textObj.transform.SetParent(rootObj.transform, false);
        potionText = textObj.AddComponent<Text>();
        potionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        potionText.fontSize = potionFontSize;
        potionText.color = potionTextColor;
        potionText.alignment = TextAnchor.UpperLeft;
        potionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        potionText.verticalOverflow = VerticalWrapMode.Overflow;
        potionText.raycastTarget = false;

        RectTransform textRect = potionText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
    }

    void BuildHearts(int count)
    {
        foreach (Image heart in hearts)
        {
            if (heart != null)
            {
                Destroy(heart.gameObject);
            }
        }
        hearts.Clear();

        for (int i = 0; i < count; i++)
        {
            GameObject heartObj = new GameObject("Heart" + (i + 1));
            heartObj.transform.SetParent(root, false);

            Image image = heartObj.AddComponent<Image>();
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            hearts.Add(image);
        }

        builtMaxHearts = count;
    }

    void UpdateLayout()
    {
        if (root == null)
        {
            return;
        }

        root.anchoredPosition = new Vector2(position.x, -position.y);

        for (int i = 0; i < hearts.Count; i++)
        {
            RectTransform rect = hearts[i].rectTransform;
            rect.sizeDelta = heartSize;
            rect.anchoredPosition = new Vector2(i * (heartSize.x + heartSpacing), 0f);
        }

        if (potionText != null)
        {
            potionText.fontSize = potionFontSize;
            potionText.color = potionTextColor;
            potionText.rectTransform.anchoredPosition = new Vector2(0f, -(heartSize.y + 6f));
        }
    }

    // 이미지가 있으면 이미지를, 없으면 색 네모를 보여줍니다.
    void ApplyHeartLook(Image image, bool filled)
    {
        if (filled)
        {
            image.sprite = fullHeartSprite;
            image.color = (fullHeartSprite != null) ? Color.white : fullColor;
        }
        else
        {
            // 빈 칸 이미지가 없으면 남은 칸 이미지를 어둡게 해서 씁니다.
            Sprite emptySprite = (emptyHeartSprite != null) ? emptyHeartSprite : fullHeartSprite;
            image.sprite = emptySprite;
            image.color = (emptyHeartSprite != null) ? Color.white : emptyColor;
        }
    }
}