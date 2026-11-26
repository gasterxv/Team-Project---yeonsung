using UnityEngine;

// 카메라가 플레이어를 부드럽게 따라갑니다.
// 플레이어가 죽고 새로 생성돼도 자동으로 다시 찾습니다.
// DefaultExecutionOrder(-100): 같은 LateUpdate 안에서 카메라가 가장 먼저 움직이게 합니다.
// (배경 ParallaxTile이 카메라보다 늦게 따라와서 생기던 화면 공백을 막습니다.)
[DefaultExecutionOrder(-100)]
public class CameraFollow : MonoBehaviour
{
    public Transform target;                            // 비워두면 PlayerController를 자동으로 찾음
    public Vector2 offset = new Vector2(2f, 1.5f);      // 플레이어 기준 카메라 위치 보정
    public float smoothTime = 0.15f;                    // 따라가는 부드러움 (작을수록 빠름)

    [Header("카메라 이동 범위 (벽 바깥이 안 보이게)")]
    public bool useBounds = false;
    public Vector2 boundsMin = new Vector2(-2f, -8f);   // 화면 왼쪽 아래 끝이 넘지 않을 위치
    public Vector2 boundsMax = new Vector2(130f, 9f);   // 화면 오른쪽 위 끝이 넘지 않을 위치

    private Vector3 velocity;
    private Camera cam;

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

        if (useBounds)
        {
            ClampToBounds();
        }
    }

    // 부활처럼 플레이어가 멀리 순간이동했을 때, 카메라도 바로 그 옆으로 옮깁니다.
    public void SnapToTarget()
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

        velocity = Vector3.zero;
        transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);

        if (useBounds)
        {
            ClampToBounds();
        }
    }

    void ClampToBounds()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 p = transform.position;
        // 범위가 화면보다 좁으면 가운데에 고정
        p.x = (boundsMax.x - boundsMin.x < halfW * 2f) ? (boundsMin.x + boundsMax.x) * 0.5f : Mathf.Clamp(p.x, boundsMin.x + halfW, boundsMax.x - halfW);
        p.y = (boundsMax.y - boundsMin.y < halfH * 2f) ? (boundsMin.y + boundsMax.y) * 0.5f : Mathf.Clamp(p.y, boundsMin.y + halfH, boundsMax.y - halfH);
        transform.position = p;
    }
}