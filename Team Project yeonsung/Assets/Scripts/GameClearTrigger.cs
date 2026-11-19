using UnityEngine;

// 게임 클리어 처리 : 시간을 멈추고 결과 화면(ResultScene)으로 이동합니다.
// 시험용 키를 누르거나, 플레이어가 이 오브젝트의 영역에 닿으면 실행됩니다.
// 다른 스크립트에서는 GameStats.ClearGame(); 한 줄로 똑같이 쓸 수 있습니다.
public class GameClearTrigger : MonoBehaviour
{
    [Header("시험용 키 (확인 끝나면 끄기)")]
    public bool useTestKey = true;
    public KeyCode testKey = KeyCode.F9;

    [Header("플레이어가 닿으면 클리어 (Collider2D의 Is Trigger를 켜야 함)")]
    public bool clearOnPlayerTouch = false;

    private bool isCleared = false;   // 한 번만 실행

    void Update()
    {
        if (useTestKey && Input.GetKeyDown(testKey))
        {
            Clear();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!clearOnPlayerTouch)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        Clear();
    }

    public void Clear()
    {
        if (isCleared)
        {
            return;
        }

        isCleared = true;
        Debug.Log("게임 클리어!");
        GameStats.ClearGame();
    }
}