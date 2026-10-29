using UnityEngine;

// 카메라가 플레이어를 부드럽게 따라갑니다.
// 플레이어가 죽고 새로 생성돼도 자동으로 다시 찾습니다.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                            // 비워두면 PlayerController를 자동으로 찾음
    public Vector2 offset = new Vector2(2f, 1.5f);      // 플레이어 기준 카메라 위치 보정
    public float smoothTime = 0.15f;                    // 따라가는 부드러움 (작을수록 빠름)

    private Vector3 velocity;

    void LateUpdate()
    {
        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                return;
            }
            target = player.transform;
        }

        Vector3 goal = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}
