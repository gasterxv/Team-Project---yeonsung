using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// '메인씬 테스트'에 이미 있는 새맵_회색상자를 그대로 두고(위치·크기·팀원이 고친 것 유지),
// 표시만 해 둔 기믹 오브젝트에 실제 동작 스크립트를 붙입니다.
// 메뉴: Tools > 새맵 > 2. 지금 씬에 기믹 연결 (위치 유지)
//
// '1. 회색 상자 맵 만들기'는 묶음을 지우고 새로 만들어서 직접 고친 것이 사라지므로, 이미 만든 씬에는 이 메뉴를 씁니다.
// 여러 번 실행해도 됩니다. (이미 붙어 있는 건 건너뜀, 지우는 것 없음)
public static class NewMapGimmickLinker
{
    const string SceneName = "메인씬 테스트";
    const string RootName = "새맵_회색상자";
    const string RulesGroupName = "규칙 (초기화·더블점프)";

    static readonly List<string> report = new List<string>();

    [MenuItem("Tools/새맵/2. 지금 씬에 기믹 연결 (위치 유지)")]
    public static void Link()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끈 상태에서 실행해 주세요.", "확인");
            return;
        }
        if (EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("새맵", "'" + SceneName + "' 씬을 먼저 열어 주세요.", "확인");
            return;
        }
        GameObject rootGo = GameObject.Find(RootName);
        if (rootGo == null)
        {
            EditorUtility.DisplayDialog("새맵", "씬에 '" + RootName + "' 묶음이 없어요.", "확인");
            return;
        }

        report.Clear();
        Undo.SetCurrentGroupName("새맵 기믹 연결");
        int group = Undo.GetCurrentGroup();

        Transform root = rootGo.transform;
        EnsureRules(root);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            string n = go.name;
            if (n.StartsWith("[안내]")) { UndoHintMistake(go); continue; }   // 안내 글자는 기믹이 아님
            if (n.StartsWith("[X 백혈구 벽]")) LinkWhiteCellWall(go);
            else if (n.StartsWith("[C 백혈구 바닥]")) LinkWhiteCellFloor(go);
            else if (n.StartsWith("[항체 = 더블점프]")) LinkAntibody(go);
            else if (n.Contains("C 튕김 항체") && n.StartsWith("[")) LinkBounce(go);
            else if (n == "부서지는 발판" || n.StartsWith("[보스방] 부서지는 발판")) LinkBreakable(go);

            if (go.GetComponent<CheckPoint>() != null && go.GetComponent<CheckpointSaveTrigger>() == null)
            {
                Undo.AddComponent<CheckpointSaveTrigger>(go);
                report.Add("체크포인트 저장 연결: " + n);
            }
        }

        EnsureWaveRoom(root);
        TurnOffStrayOldMap();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 기믹 연결 완료 (" + report.Count + "건). Ctrl+S로 저장해 주세요.\n- " + string.Join("\n- ", report));
    }

    // 팀원 GameFlowManager의 '부활 때 씬 다시 불러오기'를 이 씬에서만 끕니다. (코드는 안 고치고 Inspector 값만)
    // 끄면 예전 방식으로 부활 → OnPlayerRespawn → CheckpointResetManager가 '마지막 체크포인트 이후'만 되돌림.
    [MenuItem("Tools/새맵/3. 부활 방식: 체크포인트 이후만 되돌리기")]
    public static void UseCheckpointReset()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끄고 '" + SceneName + "' 씬에서 실행해 주세요.", "확인");
            return;
        }
        GameFlowManager gfm = Object.FindFirstObjectByType<GameFlowManager>(FindObjectsInactive.Include);
        if (gfm == null) { Debug.LogWarning("[새맵] 씬에 GameFlowManager가 없어요."); return; }

        SerializedObject so = new SerializedObject(gfm);
        SerializedProperty reload = so.FindProperty("reloadSceneOnRespawn");
        if (reload == null) { Debug.LogWarning("[새맵] GameFlowManager에 reloadSceneOnRespawn 값이 없어요."); return; }
        reload.boolValue = false;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 이 씬에서 '부활 때 씬 다시 불러오기'를 껐어요. 죽으면 마지막 체크포인트 이후만 되돌아가요. Ctrl+S로 저장해 주세요.");
    }

    // Rect 도구로 블록 그림 크기만 바꾸면 콜라이더는 그대로라서, 캐릭터가 땅 속에 잠겨 보이거나 떠 보입니다.
    // 새맵_회색상자 안의 모든 블록(움직이는 발판 제외)의 충돌 크기를 그림 크기에 맞춥니다.
    // + 웨이브 방 바닥(땅 (1))을 감염핵 방 바닥(-21)과 같은 높이로 맞춰 턱을 없앱니다.
    [MenuItem("Tools/새맵/4. 블록 충돌 크기를 그림에 맞추기")]
    public static void FitCollidersToSprites()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끄고 '" + SceneName + "' 씬에서 실행해 주세요.", "확인");
            return;
        }
        GameObject rootGo = GameObject.Find(RootName);
        if (rootGo == null) return;

        report.Clear();
        Undo.SetCurrentGroupName("새맵 충돌 크기 맞추기");
        int group = Undo.GetCurrentGroup();

        // 1) 웨이브 방 바닥 높이를 감염핵 방 바닥과 맞춤 (그림 윗면을 -21로)
        const float FloorTop = -21f;
        foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "땅 (1)") continue;
            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            if (sr == null || sr.drawMode == SpriteDrawMode.Simple) continue;
            float top = sr.bounds.max.y;
            float diff = top - FloorTop;
            if (Mathf.Abs(diff) < 0.01f) continue;
            Undo.RecordObject(sr, "바닥 높이");
            Undo.RecordObject(t, "바닥 높이");
            sr.size = new Vector2(sr.size.x, sr.size.y - diff / Mathf.Abs(t.lossyScale.y));   // 아래쪽은 그대로, 윗면만 내림
            t.position = new Vector3(t.position.x, t.position.y - diff * 0.5f, t.position.z);
            report.Add("웨이브 방 바닥 윗면 " + top.ToString("0.##") + " → " + FloorTop + " (감염핵 방과 같은 높이)");
        }

        // 2) 충돌 크기 = 그림 크기
        foreach (BoxCollider2D c in rootGo.GetComponentsInChildren<BoxCollider2D>(true))
        {
            SpriteRenderer s = c.GetComponent<SpriteRenderer>();
            if (s == null || c.isTrigger || s.drawMode == SpriteDrawMode.Simple) continue;
            if (c.GetComponent<MovingPlatform>() != null) continue;   // 움직이는 발판은 Play 중 위치가 달라서 제외
            Vector2 want = s.size;
            Vector2 wantOffset = Vector2.zero;   // 슬라이스 그림은 가운데 기준
            if (Vector2.Distance(c.size, want) < 0.01f && Vector2.Distance(c.offset, wantOffset) < 0.01f) continue;
            Undo.RecordObject(c, "충돌 크기");
            report.Add(c.name + ": 충돌 " + c.size.ToString("0.##") + " → " + want.ToString("0.##")
                + " (위치 " + c.transform.position.x.ToString("0.#") + ", " + c.transform.position.y.ToString("0.#") + ")");
            c.size = want;
            c.offset = wantOffset;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 충돌 크기 맞추기 (" + report.Count + "건). Ctrl+S로 저장해 주세요.\n- " + string.Join("\n- ", report));
    }

    // 웨이브 감염벽 두 개를 같은 크기로: 충돌 가로 0.5 × 세로 7.4, 그림 가로 0.6 × 세로 7.4 (천장 -13.6 ~ 바닥 -21).
    // 가로 위치(x)는 그대로 두고 높이만 맞춤.
    [MenuItem("Tools/새맵/5. 웨이브 감염벽 두 개 크기 같게")]
    public static void EqualizeWaveWalls()
    {
        if (EditorApplication.isPlaying) return;
        WaveRoom room = Object.FindFirstObjectByType<WaveRoom>(FindObjectsInactive.Include);
        if (room == null || room.barriers == null) return;
        const float top = -13.6f, bottom = -21f;
        foreach (GameObject b in room.barriers)
        {
            if (b == null) continue;
            Undo.RecordObject(b.transform, "감염벽 크기");
            b.transform.position = new Vector3(b.transform.position.x, (top + bottom) * 0.5f, b.transform.position.z);
            BoxCollider2D c = b.GetComponent<BoxCollider2D>();
            if (c != null) { Undo.RecordObject(c, "감염벽 크기"); c.size = new Vector2(0.5f, top - bottom); c.offset = Vector2.zero; }
            SpriteRenderer s = b.GetComponent<SpriteRenderer>();
            if (s != null) { Undo.RecordObject(s, "감염벽 크기"); s.size = new Vector2(0.6f, top - bottom); }
            Debug.Log("[새맵] " + b.name + ": 충돌 0.5 × 7.4, 그림 0.6 × 7.4 (x " + b.transform.position.x.ToString("0.##") + ")");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ---------------- 규칙 (초기화·더블점프) ----------------

    static void EnsureRules(Transform root)
    {
        if (Object.FindFirstObjectByType<CheckpointResetManager>(FindObjectsInactive.Include) != null
            && Object.FindFirstObjectByType<DoubleJumpAbility>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        Transform groupT = root.Find(RulesGroupName);
        if (groupT == null)
        {
            groupT = new GameObject(RulesGroupName).transform;
            Undo.RegisterCreatedObjectUndo(groupT.gameObject, "규칙 묶음");
            Undo.SetTransformParent(groupT, root, "규칙 묶음");
            groupT.localPosition = Vector3.zero;
        }
        GameObject rules = new GameObject("규칙");
        Undo.RegisterCreatedObjectUndo(rules, "규칙");
        Undo.SetTransformParent(rules.transform, groupT, "규칙");
        rules.transform.localPosition = Vector3.zero;

        if (Object.FindFirstObjectByType<CheckpointResetManager>(FindObjectsInactive.Include) == null)
            Undo.AddComponent<CheckpointResetManager>(rules);
        if (Object.FindFirstObjectByType<DoubleJumpAbility>(FindObjectsInactive.Include) == null)
            Undo.AddComponent<DoubleJumpAbility>(rules);
        report.Add("규칙 오브젝트 추가 (사망 초기화 + 더블점프)");
    }

    // ---------------- 웨이브 방 (92~109, 바닥 -31.6) ----------------

    const string DenguePath = "Assets/Prefabs/Enemy/JabMob/DengueMonster.prefab";
    const string HepatitisPath = "Assets/Prefabs/Enemy/JabMob/HepatitisMonster.prefab";
    const string SquarePath = "Assets/Map/Square.png";

    static void EnsureWaveRoom(Transform root)
    {
        if (Object.FindFirstObjectByType<WaveRoom>(FindObjectsInactive.Include) != null) return;

        Transform parent = root;
        foreach (Transform t in root) if (t.name.StartsWith("기믹")) { parent = t; break; }

        // 방 바닥 영역에 내려서면 잠김
        GameObject roomGo = new GameObject("[웨이브 방]");
        Undo.RegisterCreatedObjectUndo(roomGo, "웨이브 방");
        Undo.SetTransformParent(roomGo.transform, parent, "웨이브 방");
        roomGo.transform.position = new Vector3(100.5f, -30.6f, 0f);
        BoxCollider2D zone = roomGo.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(10f, 2f);   // x 95.5~105.5, y -31.6~-29.6

        // 막: 바닥에서 양쪽 턱 위로 다시 못 올라가게 턱 안쪽 끝에 세움 (천장 -13.6까지)
        GameObject left = Barrier(roomGo.transform, "[웨이브 막] 왼쪽", 95f, 95.5f, -20.1f, -13.6f);
        GameObject right = Barrier(roomGo.transform, "[웨이브 막] 오른쪽", 105.5f, 106f, -21f, -13.6f);

        WaveRoom room = roomGo.AddComponent<WaveRoom>();
        room.denguePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DenguePath)?.GetComponent<EnemyBase>();
        room.hepatitisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HepatitisPath)?.GetComponent<EnemyBase>();
        room.spawnPoints = new[] { new Vector2(96f, -30.8f), new Vector2(100.5f, -30.8f), new Vector2(105f, -30.8f) };
        room.barriers = new[] { left, right };
        if (room.denguePrefab == null || room.hepatitisPrefab == null)
            Debug.LogWarning("[새맵] 웨이브 방: 몬스터 프리팹을 못 찾았어요. Inspector에서 넣어 주세요.");

        // 예전 표시 네모는 이제 필요 없으니 꺼 둠 (지우지 않음)
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "[웨이브 소환 3곳]" || !t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, "웨이브 표시 끄기");
            t.gameObject.SetActive(false);
        }
        report.Add("웨이브 방 추가 (3웨이브: 뎅기3 → 간염3 → 뎅기2+간염2, 바닥에 내려서면 잠김)");
    }

    static GameObject Barrier(Transform parent, string name, float x0, float x1, float y0, float y1)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        Vector2 size = new Vector2(x1 - x0, y1 - y0);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = new Color(0.93f, 0.58f, 0.69f, 0.75f);
        sr.sortingOrder = 4;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        return go;
    }

    // ---------------- 기믹 ----------------

    static void LinkWhiteCellWall(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "X벽");
        col.isTrigger = false;   // 몸으로는 못 지나감
        if (go.GetComponent<WhiteCellWall>() == null)
        {
            Undo.AddComponent<WhiteCellWall>(go);
            report.Add("X 백혈구 벽 동작: " + go.name);
        }
    }

    static void LinkWhiteCellFloor(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "C바닥");
        col.isTrigger = false;
        MakeGround(go);
        if (go.GetComponent<WhiteCellFloor>() == null)
        {
            Undo.AddComponent<WhiteCellFloor>(go);
            report.Add("C 백혈구 바닥 동작: " + go.name);
        }
    }

    static void LinkAntibody(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "항체");
        col.isTrigger = true;
        if (go.GetComponent<AntibodyPickup>() == null)
        {
            Undo.AddComponent<AntibodyPickup>(go);
            report.Add("항체(더블점프) 동작: " + go.name);
        }
    }

    static void LinkBounce(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "튕김 항체");
        col.isTrigger = false;
        MakeGround(go);   // 낙하공격이 '착지'로 끝나야 튕길 수 있음

        BounceAntibody b = go.GetComponent<BounceAntibody>();
        if (b != null) return;   // 이미 있으면 값은 그대로 둠

        b = Undo.AddComponent<BounceAntibody>(go);
        Match m = Regex.Match(go.name, @"힘\s*(\d+(\.\d+)?)");
        float speed = m.Success ? float.Parse(m.Groups[1].Value) : (go.name.Contains("큰") ? 27f : 13f);
        b.bounceSpeed = speed;
        b.respawnTime = 2f;
        report.Add("C 튕김 항체 동작 (힘 " + speed + "): " + go.name);
    }

    static void LinkBreakable(GameObject go)
    {
        if (go.GetComponent<BreakablePlatform>() != null) return;   // 이미 있으면 값 그대로
        EnsureBox(go);
        MakeGround(go);
        BreakablePlatform bp = Undo.AddComponent<BreakablePlatform>(go);
        bp.breakDelay = 1f;
        report.Add("부서지는 발판 동작 추가: " + go.name + " (" + go.transform.position.x.ToString("0.#") + ", " + go.transform.position.y.ToString("0.#") + ")");
    }

    // 첫 버전 메뉴가 '[안내] C 튕김 항체 — …' 글자에 실수로 튕김 항체·콜라이더·Ground 태그를 붙였던 것을 되돌립니다.
    static void UndoHintMistake(GameObject go)
    {
        BounceAntibody b = go.GetComponent<BounceAntibody>();
        if (b == null) return;
        Undo.DestroyObjectImmediate(b);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) Undo.DestroyObjectImmediate(col);
        Undo.RecordObject(go, "안내 글자 되돌리기");
        go.tag = "Untagged";
        go.layer = 0;
        report.Add("안내 글자에 잘못 붙은 튕김 항체 제거: " + go.name);
    }

    // ---------------- 예전 맵에서 켜져 남은 몬스터·보스 끄기 ----------------

    static void TurnOffStrayOldMap()
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.activeSelf || go.name == RootName) continue;
            if (go.GetComponentInChildren<PlayerController>(true) != null) continue;
            if (go.GetComponentInChildren<Camera>(true) != null) continue;

            bool oldMonster = go.GetComponentInChildren<EnemySpawnPoint>(true) != null
                || go.GetComponentInChildren<EnemyBase>(true) != null
                || go.GetComponentInChildren<BossController>(true) != null;
            if (!oldMonster) continue;

            Undo.RecordObject(go, "예전 몬스터 끄기");
            go.SetActive(false);
            report.Add("예전 맵 몬스터·보스 꺼 둠 (지우지 않음): " + go.name);
        }
    }

    // ---------------- 공용 ----------------

    static BoxCollider2D EnsureBox(GameObject go)
    {
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) return col;

        col = Undo.AddComponent<BoxCollider2D>(go);
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Undo.RecordObject(col, "콜라이더 크기");
            col.size = sr.drawMode != SpriteDrawMode.Simple ? sr.size
                : (sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one);
            col.offset = Vector2.zero;
        }
        report.Add("콜라이더 추가: " + go.name);
        return col;
    }

    static void MakeGround(GameObject go)
    {
        Undo.RecordObject(go, "땅 태그");
        go.tag = "Ground";
        int layer = LayerMask.NameToLayer("Ground");
        if (layer >= 0) go.layer = layer;
    }
}
