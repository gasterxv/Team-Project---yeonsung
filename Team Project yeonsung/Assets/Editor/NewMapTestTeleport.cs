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

    // 테스트용: 프로젝트 Temp/teleport.txt 에 적힌 "x y" 위치로 이동
    [MenuItem("Tools/새맵/테스트 이동/파일 위치로 (Temp-teleport.txt)")]
    static void FromFile()
    {
        string file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Temp", "teleport.txt");
        if (!System.IO.File.Exists(file)) { Debug.LogWarning("[새맵 테스트] teleport.txt 없음"); return; }
        string[] p = System.IO.File.ReadAllText(file).Trim().Split(' ');
        Move(new Vector2(float.Parse(p[0]), float.Parse(p[1])));
    }

    [MenuItem("Tools/새맵/테스트 이동/파일 위치로 (Temp-teleport.txt)", true)]
    static bool OnlyInPlay5() { return EditorApplication.isPlaying; }

    // 테스트용: 플레이어 땅·내려찍기 상태와 지금 닿아 있는 것들을 적기
    [MenuItem("Tools/새맵/테스트 이동/플레이어 상태 적기")]
    static void DumpPlayer()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        string s = "[새맵 테스트] " + PlayerStateReader.DebugState(player) + " 위치 " + player.transform.position.ToString("0.##") + " 속도 " + rb.linearVelocity.ToString("0.##") + "\n";
        ContactPoint2D[] cs = new ContactPoint2D[16];
        int n = rb.GetContacts(cs);
        for (int i = 0; i < n; i++)
            s += "  닿음: " + (cs[i].collider != null ? cs[i].collider.name + "(" + cs[i].collider.tag + ")" : "-") + " / " + (cs[i].otherCollider != null ? cs[i].otherCollider.name : "-") + " 법선 " + cs[i].normal.ToString("0.##") + " 점 " + cs[i].point.ToString("0.##") + "\n";
        Debug.Log(s);
    }

    [MenuItem("Tools/새맵/테스트 이동/플레이어 상태 적기", true)]
    static bool OnlyInPlay6() { return EditorApplication.isPlaying; }

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
