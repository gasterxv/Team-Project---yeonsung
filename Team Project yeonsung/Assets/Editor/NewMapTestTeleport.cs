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

    [MenuItem("Tools/새맵/테스트 이동/노란 문 앞 (수직 통로 도착 턱)")]
    static void YellowDoor() { Move(new Vector2(107f, 33.5f)); }

    [MenuItem("Tools/새맵/테스트 이동/감염핵 앞")]
    static void HeartFront() { Move(new Vector2(117.5f, -20f)); }

    [MenuItem("Tools/새맵/테스트 이동/숨은 방")]
    static void HiddenRoom() { Move(new Vector2(14f, -16f)); }

    [MenuItem("Tools/새맵/테스트 이동/6 캡슐 방")]
    static void CapsuleRoom6() { Move(new Vector2(10f, 12.5f)); }

    [MenuItem("Tools/새맵/테스트 이동/5 위쪽 혈류 복도 끝")]
    static void Corridor5End() { Move(new Vector2(155f, 34f)); }

    [MenuItem("Tools/새맵/테스트 이동/숨은 방", true)]
    [MenuItem("Tools/새맵/테스트 이동/6 캡슐 방", true)]
    [MenuItem("Tools/새맵/테스트 이동/5 위쪽 혈류 복도 끝", true)]
    static bool OnlyInPlay4() { return EditorApplication.isPlaying; }

    [MenuItem("Tools/새맵/테스트 이동/아래방 위 (C 내려찍기 시험)")]
    static void AboveLowerRoom() { Move(new Vector2(79.5f, 2f)); }

    [MenuItem("Tools/새맵/테스트 이동/함정 2 B 들어오는 턱")]
    static void Trap2B() { Move(new Vector2(184f, -14.6f)); }

    [MenuItem("Tools/새맵/테스트 이동/아래방 위 (C 내려찍기 시험)", true)]
    [MenuItem("Tools/새맵/테스트 이동/함정 2 B 들어오는 턱", true)]
    static bool OnlyInPlay3() { return EditorApplication.isPlaying; }

    [MenuItem("Tools/새맵/테스트 이동/노란 문 앞 (수직 통로 도착 턱)", true)]
    [MenuItem("Tools/새맵/테스트 이동/감염핵 앞", true)]
    static bool OnlyInPlay2() { return EditorApplication.isPlaying; }

    [MenuItem("Tools/새맵/테스트 이동/웨이브 방 왼쪽 턱")]
    static void WaveRoomLedge() { Move(new Vector2(93.5f, -19f)); }

    // 테스트용: 화면의 살아 있는 몬스터를 모두 한 번에 처치 (진짜 피격·사망 처리를 거침)
    [MenuItem("Tools/새맵/테스트 이동/살아 있는 몬스터 모두 처치")]
    static void KillAll()
    {
        int n = 0;
        foreach (EnemyBase e in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (e == null || e.IsDead) continue;
            e.NotifyPlayerAttack(99999);
            n++;
        }
        Debug.Log("[새맵 테스트] 몬스터 " + n + "마리 처치");
    }

    [MenuItem("Tools/새맵/테스트 이동/살아 있는 몬스터 모두 처치", true)]
    [MenuItem("Tools/새맵/테스트 이동/웨이브 방 왼쪽 턱", true)]
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
