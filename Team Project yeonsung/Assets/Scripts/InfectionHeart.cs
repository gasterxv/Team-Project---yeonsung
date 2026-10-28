using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public sealed class InfectionHeart : MonoBehaviour
{
    [Header("감염 핵 설정")]
    [SerializeField, Min(1)] private int infectionHeartHP = 3;
    [SerializeField] private AnimationClip brokenInfectionHeart;
    [SerializeField] private GameObject bloodVesselDoor;
    [SerializeField] private UnityEvent onHeartBroken;
    [SerializeField] private string attackTag = "PlayerAttack";
    [SerializeField, Min(0.05f)] private float fallbackBreakDuration = 0.35f;

    private Collider2D[] hitColliders;
    private SpriteRenderer[] spriteRenderers;
    private bool isBreaking;
    private bool isBroken;
    private Vector3 startingScale;
    private Color[] startingColors;

    private void Awake()
    {
        startingScale = transform.localScale;
        hitColliders = GetComponentsInChildren<Collider2D>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        startingColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            startingColors[i] = spriteRenderers[i].color;
    }

    // PlayerController의 공격 OverlapBox가 보내는 실제 피해 메시지를 받습니다.
    public void TakeDamage(int damage)
    {
        ApplyDamage(damage);
    }

    // PlayerAttack 콜라이더 방식도 사용할 수 있도록 유지합니다.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBreaking || isBroken || !other.CompareTag(attackTag))
            return;

        PlayerHitbox hitbox = other.GetComponent<PlayerHitbox>();
        if (hitbox == null)
            hitbox = other.GetComponentInParent<PlayerHitbox>();
        if (hitbox == null)
            return;

        ApplyDamage(hitbox.GiveDamage());
    }

    private void ApplyDamage(int damage)
    {
        if (isBreaking || isBroken || damage <= 0)
            return;

        infectionHeartHP -= damage;
        if (infectionHeartHP <= 0)
            StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        isBreaking = true;
        SetCollidersEnabled(false);

        AnimationClip clip = brokenInfectionHeart;
        Animation legacyAnimation = GetComponent<Animation>();
        if (clip != null)
        {
            if (legacyAnimation == null)
                legacyAnimation = gameObject.AddComponent<Animation>();
            legacyAnimation.AddClip(clip, "BrokenInfectionHeart");
            legacyAnimation.Play("BrokenInfectionHeart");
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            yield return PlayFallbackBreakAnimation();
        }

        isBroken = true;
        if (bloodVesselDoor != null)
            bloodVesselDoor.SetActive(false);
        onHeartBroken?.Invoke();
        Destroy(gameObject);
    }

    private IEnumerator PlayFallbackBreakAnimation()
    {
        float elapsed = 0f;
        while (elapsed < fallbackBreakDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fallbackBreakDuration);
            transform.localScale = Vector3.Lerp(startingScale, Vector3.zero, progress);
            SetAlpha(1f - progress);
            yield return null;
        }
    }

    private void SetAlpha(float alpha)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            Color color = startingColors[i];
            color.a *= alpha;
            spriteRenderers[i].color = color;
        }
    }

    private void SetCollidersEnabled(bool enabledState)
    {
        for (int i = 0; i < hitColliders.Length; i++)
            hitColliders[i].enabled = enabledState;
    }
}