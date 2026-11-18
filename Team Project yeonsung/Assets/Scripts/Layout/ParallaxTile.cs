using UnityEngine;

// 원경(배경) 타일을 카메라보다 느리게 움직여 멀리 있는 느낌을 줍니다.
// 같은 period로 묶인 타일들이 번갈아 가며 화면을 계속 채웁니다. (끝이 보이지 않게 반복)
public class ParallaxTile : MonoBehaviour
{
    [Range(0f, 1f)]
    public float parallax = 0.6f;       // 1이면 카메라에 붙어 다님, 0이면 월드에 고정
    public float period = 38.4f;        // 반복 간격 (타일 너비 x 타일 개수)
    public bool followCameraY = true;   // 세로는 카메라를 그대로 따라감 (위아래 빈틈 방지)

    private Transform cam;
    private Vector3 basePos;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        basePos = transform.position;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            return;
        }

        float x = basePos.x + cam.position.x * parallax;
        // 카메라에서 가장 가까운 반복 위치로 옮깁니다.
        x += period * Mathf.Round((cam.position.x - x) / period);

        float y = followCameraY ? cam.position.y + basePos.y : basePos.y;
        transform.position = new Vector3(x, y, basePos.z);
    }
}
