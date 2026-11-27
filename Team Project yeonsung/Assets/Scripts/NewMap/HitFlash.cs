using System.Collections;
using UnityEngine;

// 맞을 때마다 잠깐 빨갛게 반짝이고 살짝 움찔합니다. (감염핵에 붙임. 팀원 InfectionHeart는 고치지 않음)
// InfectionHeart와 같은 오브젝트에 붙어서 같은 '맞음' 신호(플레이어 공격 닿음 / TakeDamage)를 같이 받습니다.
public class HitFlash : MonoBehaviour
{
    public Color flashColor = new Color(1f, 0.15f, 0.15f);
    [Tooltip("빨갛게 보이는 시간(초)")]
    public float flashTime = 0.12f;
    [Tooltip("반짝이는 횟수")]
    public int flashCount = 2;
    [Tooltip("움찔하는 크기 (0이면 안 함)")]
    public float punchScale = 0.06f;
    public string attackTag = "PlayerAttack";

    SpriteRenderer[] renderers;
    Color[] baseColors;
    Vector3 baseScale;
    Coroutine running;
    float lastHitTime = -1f;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
        baseScale = transform.localScale;
    }

    // 예전 방식 공격(SendMessage)으로 맞을 때
    public void TakeDamage(int damage) { Flash(); }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(attackTag)) return;
        if (other.GetComponentInParent<PlayerHitbox>() == null) return;
        Flash();
    }

    public void Flash()
    {
        if (!isActiveAndEnabled || Time.time - lastHitTime < 0.05f) return;
        lastHitTime = Time.time;
        if (running != null) { StopCoroutine(running); Restore(); }
        running = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        for (int k = 0; k < Mathf.Max(1, flashCount); k++)
        {
            SetTint(true);
            if (k == 0 && punchScale > 0f) transform.localScale = baseScale * (1f + punchScale);
            yield return new WaitForSeconds(flashTime);
            SetTint(false);
            if (transform.localScale == baseScale * (1f + punchScale)) transform.localScale = baseScale;   // 부서지며 작아지는 중이면 건드리지 않음
            yield return new WaitForSeconds(flashTime * 0.6f);
        }
        running = null;
    }

    // 투명도는 건드리지 않음 (부서질 때 InfectionHeart가 흐려지게 하는 것과 겹치지 않게)
    void SetTint(bool red)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = red ? flashColor : baseColors[i];
            c.a = renderers[i].color.a;
            renderers[i].color = c;
        }
    }

    void Restore()
    {
        SetTint(false);
        transform.localScale = baseScale;
    }

    void OnDisable()
    {
        if (renderers != null) Restore();
        running = null;
    }
}
