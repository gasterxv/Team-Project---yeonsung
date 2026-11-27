using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 가운데 큰 글자 연출 (웨이브 알림, 카운트다운, 나중에 보스 처치 "몸이 치료되었다!"에도 사용)
// + 화면 테두리 빨간 그라데이션 경보(깜빡임).
// 씬에 따로 놓을 필요 없음: 처음 부를 때 스스로 캔버스를 만듭니다.
// 사용: ScreenAnnouncer.Show("웨이브 1 / 3", "뎅기 3", 1.4f);
//       ScreenAnnouncer.ShowDanger("감염 반응 감지!", "통로가 막혔다", 2f);   ← 빨간 경고
//       ScreenAnnouncer.Count("3");
//       ScreenAnnouncer.SetAlarm(true / false);   ← 테두리 빨간 깜빡임 켜기/끄기
public class ScreenAnnouncer : MonoBehaviour
{
    static ScreenAnnouncer instance;

    static readonly Color MainColor = new Color(0.55f, 1f, 0.95f);      // GAME OVER 화면과 같은 청록 빛
    static readonly Color MainGlow = new Color(0.1f, 0.85f, 0.8f, 0.6f);
    static readonly Color DangerColor = new Color(1f, 0.16f, 0.18f);    // 위험 빨강
    static readonly Color DangerGlow = new Color(0.55f, 0f, 0.02f, 0.9f);
    static readonly Color CountColor = new Color(1f, 0.55f, 0.85f);     // 감염(보라·분홍)
    static readonly Color AlarmColor = new Color(0.9f, 0.02f, 0.05f);

    Text title;
    Text sub;
    Text count;
    Outline titleGlow;
    CanvasGroup titleGroup;
    CanvasGroup countGroup;
    RawImage alarm;
    Coroutine titleRoutine;
    Coroutine countRoutine;
    bool danger;
    bool alarmOn;
    float alarmTime;
    float alarmAlpha;

    [Tooltip("테두리 경보 밝기 (최소~최대 사이를 깜빡임)")]
    public float alarmMin = 0.25f, alarmMax = 0.7f;
    [Tooltip("테두리 경보 깜빡임 속도 (1초에 몇 번)")]
    public float alarmSpeed = 1.2f;

    static ScreenAnnouncer Get()
    {
        if (instance != null) return instance;
        GameObject go = new GameObject("[화면 알림]");
        instance = go.AddComponent<ScreenAnnouncer>();
        instance.Build();
        return instance;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 테두리 경보 (화면 전체, 가운데는 투명하고 가장자리로 갈수록 빨강)
        GameObject a = new GameObject("테두리 경보", typeof(RectTransform));
        a.transform.SetParent(transform, false);
        RectTransform art = (RectTransform)a.transform;
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = art.offsetMax = Vector2.zero;
        alarm = a.AddComponent<RawImage>();
        alarm.texture = EdgeTexture();
        alarm.color = new Color(AlarmColor.r, AlarmColor.g, AlarmColor.b, 0f);
        alarm.raycastTarget = false;

        titleGroup = Group("제목 묶음", new Vector2(0f, 170f));
        title = MakeText(titleGroup.transform, "제목", 96, new Vector2(0f, 30f), MainColor);
        titleGlow = title.GetComponent<Outline>();
        sub = MakeText(titleGroup.transform, "설명", 40, new Vector2(0f, -55f), new Color(1f, 1f, 1f, 0.9f));
        countGroup = Group("카운트 묶음", new Vector2(0f, -90f));   // 제목·설명 아래 (겹치지 않게)
        count = MakeText(countGroup.transform, "숫자", 180, Vector2.zero, CountColor);
        titleGroup.alpha = 0f;
        countGroup.alpha = 0f;
    }

    CanvasGroup Group(string name, Vector2 pos)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)g.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1600f, 300f);
        CanvasGroup cg = g.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        return cg;
    }

    static Text MakeText(Transform parent, string name, int size, Vector2 pos, Color color)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)g.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1600f, size * 1.4f);
        Text t = g.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = color;
        t.raycastTarget = false;
        Outline glow = g.AddComponent<Outline>();
        glow.effectColor = MainGlow;
        glow.effectDistance = new Vector2(4f, -4f);
        Shadow shadow = g.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(6f, -6f);
        return t;
    }

    // 큰 제목 + 작은 설명 (청록)
    public static void Show(string main, string subText, float duration)
    {
        Get().StartTitle(main, subText, duration, false);
    }

    // 위험 경고 (빨강, 더 크게, 떨림)
    public static void ShowDanger(string main, string subText, float duration)
    {
        Get().StartTitle(main, subText, duration, true);
    }

    void StartTitle(string main, string subText, float duration, bool isDanger)
    {
        danger = isDanger;
        title.text = main;
        title.color = isDanger ? DangerColor : MainColor;
        title.fontSize = isDanger ? 120 : 96;
        titleGlow.effectColor = isDanger ? DangerGlow : MainGlow;
        titleGlow.effectDistance = isDanger ? new Vector2(6f, -6f) : new Vector2(4f, -4f);
        sub.text = subText ?? "";
        sub.color = isDanger ? new Color(1f, 0.8f, 0.8f, 0.95f) : new Color(1f, 1f, 1f, 0.9f);
        if (titleRoutine != null) StopCoroutine(titleRoutine);
        titleRoutine = StartCoroutine(TitleRoutine(duration));
    }

    // 카운트다운 숫자 하나 (크게 나타났다가 작아지며 사라짐)
    public static void Count(string text, float duration = 0.45f)
    {
        ScreenAnnouncer a = Get();
        a.count.text = text;
        if (a.countRoutine != null) a.StopCoroutine(a.countRoutine);
        a.countRoutine = a.StartCoroutine(a.CountRoutine(duration));
    }

    // 테두리 빨간 경보 깜빡임 켜기/끄기 (끌 때는 부드럽게 사라짐)
    public static void SetAlarm(bool on)
    {
        if (!on && instance == null) return;
        ScreenAnnouncer a = Get();
        if (on && !a.alarmOn) a.alarmTime = 0f;
        a.alarmOn = on;
    }

    public static void HideAll()
    {
        if (instance == null) return;
        instance.StopAllCoroutines();
        instance.titleRoutine = instance.countRoutine = null;
        instance.titleGroup.alpha = 0f;
        instance.countGroup.alpha = 0f;
        instance.alarmOn = false;
        instance.alarmAlpha = 0f;
        instance.alarm.color = new Color(AlarmColor.r, AlarmColor.g, AlarmColor.b, 0f);
    }

    void Update()
    {
        // 테두리 경보: 켜져 있으면 min~max 사이를 깜빡임, 꺼지면 0으로 서서히
        float target = 0f;
        if (alarmOn)
        {
            alarmTime += Time.deltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(alarmTime * alarmSpeed * Mathf.PI * 2f - Mathf.PI * 0.5f);
            target = Mathf.Lerp(alarmMin, alarmMax, wave);
        }
        alarmAlpha = Mathf.MoveTowards(alarmAlpha, target, Time.deltaTime * (alarmOn ? 3f : 1.5f));
        alarm.color = new Color(AlarmColor.r, AlarmColor.g, AlarmColor.b, alarmAlpha);
    }

    IEnumerator TitleRoutine(float duration)
    {
        RectTransform rt = (RectTransform)titleGroup.transform;
        Vector2 basePos = new Vector2(0f, 170f);
        float inTime = danger ? 0.18f : 0.25f, outTime = 0.35f;
        float hold = Mathf.Max(0f, duration - inTime - outTime);
        for (float t = 0f; t < inTime; t += Time.deltaTime)
        {
            float k = t / inTime;
            titleGroup.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(danger ? 1.7f : 1.35f, 1f, EaseOut(k));   // 크게 들어와서 자리 잡음
            yield return null;
        }
        titleGroup.alpha = 1f;
        rt.localScale = Vector3.one;
        for (float t = 0f; t < hold; t += Time.deltaTime)
        {
            if (danger)
            {
                // 위험: 빠르게 번쩍이며 살짝 떨림 (처음이 가장 세고 점점 약해짐)
                float shake = Mathf.Lerp(9f, 2f, t / Mathf.Max(0.01f, hold));
                rt.anchoredPosition = basePos + new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
                float flash = 0.5f + 0.5f * Mathf.Sin(t * 30f);
                title.color = Color.Lerp(DangerColor, new Color(1f, 0.85f, 0.85f), flash * 0.45f);
            }
            else
            {
                title.color = new Color(title.color.r, title.color.g, title.color.b, 0.85f + 0.15f * Mathf.Sin(t * 12f));
            }
            yield return null;
        }
        rt.anchoredPosition = basePos;
        for (float t = 0f; t < outTime; t += Time.deltaTime)
        {
            titleGroup.alpha = 1f - t / outTime;
            yield return null;
        }
        titleGroup.alpha = 0f;
        titleRoutine = null;
    }

    IEnumerator CountRoutine(float duration)
    {
        RectTransform rt = (RectTransform)countGroup.transform;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            countGroup.alpha = k < 0.15f ? k / 0.15f : 1f - Mathf.InverseLerp(0.6f, 1f, k);
            rt.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.9f, EaseOut(k));
            yield return null;
        }
        countGroup.alpha = 0f;
        countRoutine = null;
    }

    static float EaseOut(float k) { return 1f - (1f - k) * (1f - k); }

    // 가운데 투명 → 가장자리 불투명 (화면 비율에 맞게 늘어남)
    static Texture2D EdgeTexture()
    {
        const int w = 128, h = 72;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Min(x + 0.5f, w - x - 0.5f) / (w * 0.5f);   // 0 = 가장자리, 1 = 가운데
                float dy = Mathf.Min(y + 0.5f, h - y - 0.5f) / (h * 0.5f);
                float edge = Mathf.Min(dx * 1.0f, dy * 0.8f);               // 위아래는 조금 더 두껍게
                float k = Mathf.Clamp01(edge / 0.38f);                       // 가장자리에서 안쪽 약 38%까지 번짐
                float a = 1f - k * k * (3f - 2f * k);                         // 부드럽게 (가운데는 완전 투명)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
