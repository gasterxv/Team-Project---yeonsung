using UnityEditor;
using UnityEngine;

// 테스트용: Play 중에 플레이어를 기믹 근처로 바로 옮깁니다. (씬은 바뀌지 않음, Play를 끄면 원래대로)
// 메뉴: Tools > 새맵 > 테스트 이동 > ...
public static class NewMapTestTeleport
{
    [MenuItem("Tools/새맵/테스트 이동/큰 C 튕김 항체 위 (보스 전)")]
    static void BigOrb() { Move(new Vector2(162f, 7f)); }

    [MenuItem("Tools/새맵/테스트 이동/C 튕김 항체 O1 위 (중후반)")]
    static void OrbO1() { Move(new Vector2(124.5f, 13f)); }

    [MenuItem("Tools/새맵/테스트 이동/항체 칸 (더블점프 항체)")]
    static void AntibodyRoom() { Move(new Vector2(115f, 1f)); }

    [MenuItem("Tools/새맵/테스트 이동/수직 통로 아래")]
    static void Vertical() { Move(new Vector2(98f, 1f)); }

    [MenuItem("Tools/새맵/테스트 이동/큰 C 튕김 항체 위 (보스 전)", true)]
    [MenuItem("Tools/새맵/테스트 이동/C 튕김 항체 O1 위 (중후반)", true)]
    [MenuItem("Tools/새맵/테스트 이동/항체 칸 (더블점프 항체)", true)]
    [MenuItem("Tools/새맵/테스트 이동/수직 통로 아래", true)]
    static bool OnlyInPlay() { return EditorApplication.isPlaying; }

    static void Move(Vector2 pos)
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) { Debug.LogWarning("[새맵 테스트] 플레이어가 없어요."); return; }
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        player.transform.position = pos;
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
        Physics2D.SyncTransforms();
        CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>();
        if (follow != null) follow.SnapToTarget();
        Debug.Log("[새맵 테스트] 플레이어 이동: " + pos);
    }
}
