using System.Collections;
using UnityEngine;

public class VaccineGate : MonoBehaviour
{
    [SerializeField] private AnimationClip OpenGate;

    private bool playerNearby;
    private bool opening;

    private void Update()
    {
        if (!playerNearby || opening || !Input.GetKeyDown(KeyCode.F))
        {
            return;
        }

        if (!VaccineController.ConsumeVaccine())
        {
            Debug.Log("백신이 필요합니다.");
            return;
        }

        StartCoroutine(OpenAndRemove());
    }

    private IEnumerator OpenAndRemove()
    {
        opening = true;
        Debug.Log("백신을 사용했습니다. 감염된 문이 열립니다.");

        // Disable both the blocking collider and the interaction range.
        foreach (Collider2D doorCollider in GetComponents<Collider2D>())
        {
            doorCollider.enabled = false;
        }

        if (OpenGate != null)
        {
            Animation doorAnimation = GetComponent<Animation>();
            if (doorAnimation == null)
            {
                doorAnimation = gameObject.AddComponent<Animation>();
            }

            doorAnimation.AddClip(OpenGate, "OpenGate");
            doorAnimation.Play("OpenGate");
            yield return new WaitForSeconds(OpenGate.length);
        }
        else
        {
            // Temporary built-in animation until the team's door animation is ready.
            Vector3 startScale = transform.localScale;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(
                    startScale,
                    new Vector3(startScale.x, 0.02f, startScale.z),
                    progress
                );
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerNearby = true;
            Debug.Log("감염된 문입니다. 백신이 있으면 F를 누르세요.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerNearby = false;
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player");
    }
}