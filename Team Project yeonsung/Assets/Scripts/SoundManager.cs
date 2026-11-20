using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private AudioSource audioSource;

    private void Awake()
    {
        // 중복 사운드 매니저 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 씬이 바뀌어도 유지
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
    }

    // 공격, 피격, 버튼 클릭 등의 효과음
    public void PlaySE(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning(
                "SoundManager: 효과음이 연결되지 않았습니다.", this);
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    // 클리어, 게임오버 등의 일회성 음악
    public void PlayME(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning(
                "SoundManager: 음악이 연결되지 않았습니다.", this);
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.Play();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
