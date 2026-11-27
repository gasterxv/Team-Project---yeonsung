using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 가운데 큰 글자 연출 (웨이브 알림, 카운트다운, 나중에 보스 처치 "몸이 치료되었다!"에도 사용).
// 씬에 따로 놓을 필요 없음: 처음 부를 때 스스로 캔버스를 만듭니다.
// 사용: ScreenAnnouncer.Show("감염 반응 감지!", "모두 처치하라", 1.6f);
//       ScreenAnnouncer.Count("3");
public class ScreenAnnouncer : MonoBehaviour
{
    static ScreenAnnouncer instance;

    static readonly Color MainColor = new Color(0.55f, 1f, 0.95f);      // GAME OVER 화면과 같은 청록 빛
    static readonly Color GlowColor = new Color(0.1f, 0.85f, 0.8f, 0.6f);
    static readonly Color WarnColor = new Color(1f, 0.55f, 0.85f);      // 감염(보라·분홍) 경고

    Text title;
    Text sub;
    Text count;
    CanvasGroup titleGroup;
    CanvasGroup countGroup;
    Coroutine titleRoutine;
    Coroutine countRoutine;

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

        titleGroup = Group("제목 묶음", new Vector2(0f, 170f));
        title = MakeText(titleGroup.transform, "제목", 96, new Vector2(0f, 30f), MainColor);
        sub = MakeText(titleGroup.transform, "설명", 40, new Vector2(0f, -55f), new Color(1f, 1f, 1f, 0.9f));
        countGroup = Group("카운트 묶음", new Vector2(0f, -90f));   // 제목·설명 아래 (겹치지 않게)
        count = MakeText(countGroup.transform, "숫자", 180, Vector2.zero, WarnColor);
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
        glow.effectColor = GlowColor;
        glow.effectDistance = new Vector2(4f, -4f);
        Shadow shadow = g.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(6f, -6f);
        return t;
    }

    // 큰 제목 + 작은 설명. warn = true면 감염 경고 색(분홍)
    public static void Show(string main, string subText, float duration, bool warn = false)
    {
        ScreenAnnouncer a = Get();
        a.title.text = main;
        a.title.color = warn ? WarnColor : MainColor;
        a.sub.text = subText ?? "";
        if (a.titleRoutine != null) a.StopCoroutine(a.titleRoutine);
        a.titleRoutine = a.StartCoroutine(a.TitleRoutine(duration));
    }

    // 카운트다운 숫자 하나 (크게 나타났다가 작아지며 사라짐)
    public static void Count(string text, float duration = 0.45f)
    {
        ScreenAnnouncer a = Get();
        a.count.text = text;
        if (a.countRoutine != null) a.StopCoroutine(a.countRoutine);
        a.countRoutine = a.StartCoroutine(a.CountRoutine(duration));
    }

    public static void HideAll()
    {
        if (instance == null) return;
        instance.StopAllCoroutines();
        instance.titleRoutine = instance.countRoutine = null;
        instance.titleGroup.alpha = 0f;
        instance.countGroup.alpha = 0f;
    }

    IEnumerator TitleRoutine(float duration)
    {
        RectTransform rt = (RectTransform)titleGroup.transform;
        float inTime = 0.25f, outTime = 0.35f;
        float hold = Mathf.Max(0f, duration - inTime - outTime);
        for (float t = 0f; t < inTime; t += Time.deltaTime)
        {
            float k = t / inTime;
            titleGroup.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, EaseOut(k));   // 크게 들어와서 자리 잡음
            yield return null;
        }
        titleGroup.alpha = 1f;
        rt.localScale = Vector3.one;
        for (float t = 0f; t < hold; t += Time.deltaTime)
        {
            // 살짝 맥박처럼 빛남
            title.color = new Color(title.color.r, title.color.g, title.color.b, 0.85f + 0.15f * Mathf.Sin(t * 12f));
            yield return null;
        }
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

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
