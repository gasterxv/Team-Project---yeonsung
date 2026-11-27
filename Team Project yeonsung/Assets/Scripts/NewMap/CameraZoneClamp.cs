using System;
using UnityEngine;

// 플레이어가 어떤 구역 안에 있으면, 카메라가 그 구역의 벽 너머를 보여 주지 않게 막습니다.
// (팀원 CameraFollow는 고치지 않음. CameraFollow가 카메라를 옮긴 바로 뒤에 위치만 한 번 더 맞춤)
// 구역에서 나가면 다시 자유롭게 따라가고, 경계가 바뀔 때는 부드럽게 옮겨 갑니다.
[DefaultExecutionOrder(-90)]   // CameraFollow(-100) 다음, 배경(ParallaxTile) 전
public class CameraZoneClamp : MonoBehaviour
{
    [Serializable]
    public class Zone
    {
        public string name;
        [Tooltip("플레이어가 이 네모 안에 있으면 적용")]
        public Rect playerArea;
        [Tooltip("화면 왼쪽 끝이 이 x보다 왼쪽으로 못 감")]
        public bool clampLeft;
        public float minViewX;
        [Tooltip("화면 오른쪽 끝이 이 x보다 오른쪽으로 못 감")]
        public bool clampRight;
        public float maxViewX;
    }

    public Zone[] zones = new Zone[0];
    [Tooltip("구역이 바뀔 때 카메라가 옮겨 가는 빠르기 (클수록 빠름)")]
    public float blendSpeed = 25f;

    Camera cam;
    Transform player;
    Zone current;
    float curMin, curMax;   // 지금 쓰는 카메라 중심 한계 (구역에 막 들어오면 현재 위치에서 시작해 진짜 한계로 옮겨 감)

    void LateUpdate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return;
        if (player == null)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p == null) return;
            player = p.transform;
        }

        float halfW = cam.orthographicSize * cam.aspect;
        Vector3 pos = transform.position;
        float x = pos.x;
        Vector2 pp = player.position;
        Zone zone = null;
        foreach (Zone z in zones)
            if (z != null && z.playerArea.Contains(pp)) { zone = z; break; }
        if (zone == null) { current = null; return; }   // 구역 밖: CameraFollow 그대로

        // 카메라 중심이 갈 수 있는 범위
        float lo = zone.clampLeft ? zone.minViewX + halfW : float.NegativeInfinity;
        float hi = zone.clampRight ? zone.maxViewX - halfW : float.PositiveInfinity;
        if (lo > hi) lo = hi = (zone.minViewX + zone.maxViewX) * 0.5f;   // 구역이 화면보다 좁으면 가운데

        if (zone != current)
        {
            // 막 들어온 순간: 화면이 툭 튀지 않게 지금 위치에서 시작
            current = zone;
            curMin = Mathf.Min(lo, x);
            curMax = Mathf.Max(hi, x);
        }
        float step = blendSpeed * Time.deltaTime;
        curMin = float.IsInfinity(lo) ? lo : Mathf.MoveTowards(curMin, lo, step);
        curMax = float.IsInfinity(hi) ? hi : Mathf.MoveTowards(curMax, hi, step);

        pos.x = Mathf.Clamp(x, curMin, curMax);
        transform.position = pos;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        foreach (Zone z in zones)
        {
            if (z == null) continue;
            Gizmos.DrawWireCube(z.playerArea.center, z.playerArea.size);
        }
    }
}
