using System.Collections;
using UnityEngine;

// 몬스터가 나올 자리 바닥에 생기는 보라색 감염 얼룩 (소환 예고).
// 점점 커지며 깜빡이고, 몬스터가 나오면 Pop()으로 터지듯 사라집니다.
// 5번 복도의 '감염된 혈소판'과 같은 보라 얼룩 느낌. 그림이 생기면 sprite만 바꾸면 됨.
public class SpawnWarningBlob : MonoBehaviour
{
    static Sprite circle;

    SpriteRenderer sr;
    SpriteRenderer core;
    float growTime;
    float age;
    bool popping;
    Vector3 fullScale;

    static readonly Color Purple = new Color(0.62f, 0.22f, 0.85f, 0.85f);
    static readonly Color Light = new Color(0.95f, 0.6f, 1f, 0.9f);

    // floorPos = 바닥 윗면 위치, growTime = 몬스터가 나올 때까지 걸리는 시간
    public static SpawnWarningBlob Create(Vector2 floorPos, float growTime)
    {
        GameObject go = new GameObject("[소환 예고 얼룩]");
        go.transform.position = new Vector3(floorPos.x, floorPos.y + 0.08f, 0f);
        SpawnWarningBlob b = go.AddComponent<SpawnWarningBlob>();
        b.growTime = Mathf.Max(0.1f, growTime);
        b.fullScale = new Vector3(2.2f, 0.45f, 1f);   // 바닥에 납작하게 퍼진 얼룩
        b.sr = go.AddComponent<SpriteRenderer>();
        b.sr.sprite = Circle();
        b.sr.color = Purple;
        b.sr.sortingOrder = 6;

        GameObject inner = new GameObject("빛");
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        b.core = inner.AddComponent<SpriteRenderer>();
        b.core.sprite = Circle();
        b.core.color = Light;
        b.core.sortingOrder = 7;

        go.transform.localScale = Vector3.zero;
        return b;
    }

    void Update()
    {
        if (popping) return;
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / growTime);
        float wobble = 1f + 0.08f * Mathf.Sin(age * 18f);                 // 꿈틀거림
        transform.localScale = new Vector3(fullScale.x * k * wobble, fullScale.y * k / wobble, 1f);
        float blink = 0.5f + 0.5f * Mathf.Sin(age * Mathf.Lerp(6f, 22f, k));   // 나올 때가 다가올수록 빨리 깜빡임
        core.color = new Color(Light.r, Light.g, Light.b, Light.a * blink);
    }

    // 몬스터가 나오는 순간: 확 퍼졌다가 사라짐
    public void Pop()
    {
        if (popping) return;
        popping = true;
        StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        Vector3 start = transform.localScale;
        Color c0 = sr.color;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            transform.localScale = new Vector3(start.x * (1f + 0.6f * k), start.y * (1f + 1.5f * k), 1f);
            sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k));
            core.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }

    static Sprite Circle()
    {
        if (circle != null) return circle;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01((1f - d) * 3f);   // 가장자리가 부드러운 원
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circle;
    }
}
