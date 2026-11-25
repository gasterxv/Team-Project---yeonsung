using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메인씬 보스방 확장 + 부서지는 발판 + 보스방 문 + x30 혈류 구덩이 수정
// 메뉴: Tools > 메인씬 > 보스방·혈류 수정 적용  (Ctrl+Z 한 번으로 전부 되돌릴 수 있어요)
// 계획: docs/superpowers/plans/2026-11-25-boss-room.md
public static class BossRoomSceneApply
{
    const string Root = "Map_순환계";
    const string Terrain = Root + "/설계도 지형";
    const string WallCeil = Terrain + "/벽과 천장";
    const string ArtV2 = Terrain + "/땅그림_v2";
    const string BossRoom = Root + "/9_보스방";
    const string ManagerName = "보스방 관리";

    const float RoomLeft = 239f, RoomRight = 303.5f;
    const float Floor = 0.94f, OldCeiling = 13.16f, NewCeiling = 19.3f;
    const float CeilingRaise = NewCeiling - OldCeiling;          // 6.14
    const float CeilingThickness = 20.45f - 13.16f;              // 7.29
    const float ShiftRight = 21.5f;

    const string DoorPrefabPath = "Assets/Prefabs/Object/Infection door.prefab";
    const string PlatformSpritePath = "Assets/image/부서지는바닥.png";

    static readonly List<string> log = new List<string>();

    [MenuItem("Tools/메인씬/보스방·혈류 수정 적용")]
    static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "#MainScenes#")
        {
            EditorUtility.DisplayDialog("보스방 수정", "#MainScenes# 씬을 열고 실행해주세요.", "확인");
            return;
        }
        if (Find(BossRoom + "/" + ManagerName) != null)
        {
            EditorUtility.DisplayDialog("보스방 수정", "이미 적용되어 있어요. (9_보스방/보스방 관리 가 있음)", "확인");
            return;
        }

        log.Clear();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("보스방·혈류 수정");

        FixBloodPit();
        RemoveUnusedBlocks();
        ResizeRoomBlocks();
        MoveArt();
        MoveVaccineDoor();
        CreateRoomObjects();

        foreach (string p in new[] { Terrain, WallCeil })
        {
            Transform t = Find(p);
            CompositeCollider2D comp = t != null ? t.GetComponent<CompositeCollider2D>() : null;
            if (comp != null) comp.GenerateGeometry();
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[보스방 수정 적용]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("보스방 수정", "적용 완료! Ctrl+S로 저장해주세요.\n(자세한 내용은 Console 로그)", "확인");
    }

    // ---------------- 1. 혈류 구덩이 (x≈30) ----------------
    static void FixBloodPit()
    {
        Transform blood = Find(Terrain + "/혈류 2 (2)");
        if (blood != null)
        {
            SetBlockRect(blood, 28.2f, 35.72f, -2.35f, -1.55f);
            log.Add("혈류 2 (2): x 28.2~35.72, y -2.35~-1.55 (구덩이 바닥 위로)");
        }
        else log.Add("!! 혈류 2 (2) 없음");

        Destroy(Terrain + "/혈류 2 (1)");
    }

    // ---------------- 2. 안 쓰는 블록/그림 삭제 ----------------
    static void RemoveUnusedBlocks()
    {
        foreach (string n in new[] { "지형 04", "지형 10", "지형 11", "지형 12", "지형 13", "지형 14" })
            Destroy(WallCeil + "/" + n);

        foreach (string n in new[] { "천장 AI y4.94 x239", "천장 AI y6.58 x239", "천장 땅1 y9.87 x239",
                                     "천장 땅2 y8.23 x239", "천장 땅2 y11.52 x239", "천장 땅1 y20.45 x279" })
            Destroy(ArtV2 + "/" + n);
    }

    // ---------------- 3. 천장/바닥 블록 크기 ----------------
    static void ResizeRoomBlocks()
    {
        Transform ceiling = Find(WallCeil + "/지형 03");
        if (ceiling != null)
        {
            GameObject roomCeiling = Object.Instantiate(ceiling.gameObject, ceiling.parent);
            roomCeiling.name = "지형 03 보스방";
            Undo.RegisterCreatedObjectUndo(roomCeiling, "보스방 천장");
            SetBlockRect(roomCeiling.transform, RoomLeft, RoomRight, NewCeiling, NewCeiling + CeilingThickness);
            SetBlockRect(ceiling, 209.87f, RoomLeft, OldCeiling, OldCeiling + CeilingThickness);
            log.Add("지형 03: x 209.87~239 로 줄임 / 지형 03 보스방: x 239~303.5, y 19.3~26.59 새로 만듦");
        }
        else log.Add("!! 지형 03 없음");

        Transform floor = Find(Terrain + "/지형 24");
        if (floor != null)
        {
            SetBlockRect(floor, 208.93f, 323.5f, -4f, Floor);
            log.Add("지형 24: x 208.93~323.5 로 늘림");
        }
        else log.Add("!! 지형 24 없음");
    }

    // ---------------- 4. 천장/바닥 그림 ----------------
    static void MoveArt()
    {
        Transform tiles = Find(ArtV2 + "/천장 AI y13.16 x210");
        if (tiles != null)
        {
            Transform leftEdgeTile = null;
            float maxX = float.MinValue;
            var children = new List<Transform>();
            foreach (Transform c in tiles) children.Add(c);

            foreach (Transform c in children)
            {
                float x = c.position.x;
                if (x >= 249f)
                {
                    Undo.RecordObject(c, "천장 그림 올리기");
                    c.position += Vector3.up * CeilingRaise;
                }
                else if (x > 228f) leftEdgeTile = c;   // 왼쪽 천장과 보스방에 걸친 타일 (x 238.49)
                maxX = Mathf.Max(maxX, x);
            }

            float tileWidth = 21.08f;
            if (leftEdgeTile != null) Duplicate(leftEdgeTile, Vector3.up * CeilingRaise, "천장 그림");

            // 오른쪽 끝(x 303.5)까지 덮도록 맨 오른쪽 타일 하나를 이어 붙임
            Transform last = null;
            foreach (Transform c in children) if (Mathf.Approximately(c.position.x, maxX)) last = c;
            if (last != null && maxX >= 249f) Duplicate(last, Vector3.right * tileWidth, "천장 그림");
            log.Add("천장 그림: 보스방 부분 +" + CeilingRaise.ToString("F2") + " 올림, 타일 2장 추가");
        }

        Transform floorArt = Find(ArtV2 + "/바닥 땅2 y0.94 x209/중경_땅2");
        if (floorArt != null)
        {
            Duplicate(floorArt, Vector3.right * ShiftRight, "바닥 그림");
            log.Add("바닥 그림: 중경_땅2 복제 +" + ShiftRight);
        }
    }

    // ---------------- 5. 백신 출구 문 ----------------
    static void MoveVaccineDoor()
    {
        foreach (VaccineGate gate in Object.FindObjectsByType<VaccineGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform t = gate.transform;
            if (t.position.x < 270f || t.position.x > 290f) continue;
            Undo.RecordObject(t, "백신 문 이동");
            t.position = new Vector3(306f, t.position.y, t.position.z);
            log.Add(gate.name + ": x 306 으로 이동");
        }
    }

    // ---------------- 6. 문, 입장 감지, 발판 ----------------
    static void CreateRoomObjects()
    {
        Transform room = Find(BossRoom);
        if (room == null) { log.Add("!! 9_보스방 없음"); return; }

        BossRoomDoor entrance = CreateDoor(room, "[보스방] 입구 문", 228f, Floor, OldCeiling, false);
        BossRoomDoor exit = CreateDoor(room, "[보스방] 출구 문", 303f, Floor, NewCeiling, true);

        var platforms = new List<BreakablePlatform>();
        foreach (float x in new[] { 260.5f, 282f })
            platforms.Add(CreatePlatform(room, "[보스방] 부서지는 발판 x" + x, x));

        GameObject manager = new GameObject(ManagerName);
        Undo.RegisterCreatedObjectUndo(manager, "보스방 관리");
        manager.transform.SetParent(room, false);
        manager.transform.position = new Vector3(242.5f, (Floor + OldCeiling) * 0.5f, 0f);
        BoxCollider2D zone = manager.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(3f, OldCeiling - Floor);

        BossRoomController ctrl = manager.AddComponent<BossRoomController>();
        ctrl.entranceDoor = entrance;
        ctrl.exitDoor = exit;
        ctrl.platforms = platforms.ToArray();

        BossController boss = Object.FindFirstObjectByType<BossController>();
        if (boss != null)
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(boss.gameObject);
            ctrl.bossRoot = root != null ? root : boss.gameObject;
            log.Add("보스 연결: " + ctrl.bossRoot.name);
        }
        else log.Add("!! 보스(BossController) 없음 - 보스방 관리에 bossRoot를 직접 연결하세요");

        log.Add("입구 문 x228 (처음 열림), 출구 문 x303 (처음 닫힘), 입장 감지 x241~244, 발판 x260.5 / x282 (윗면 y3.14)");
    }

    static BossRoomDoor CreateDoor(Transform parent, string name, float x, float bottom, float top, bool startClosed)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.tag = "BossRoomTacle";

        float height = top - bottom;
        Sprite sprite = LoadDoorSprite();
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 5;
        Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        Vector2 spriteCenter = sprite != null ? (Vector2)sprite.bounds.center : Vector2.zero;

        // 그림 크기를 폭 1 × 문 높이로 맞추고, 그림 가운데가 (x, 문 가운데)에 오게 둡니다.
        go.transform.localScale = new Vector3(1f / spriteSize.x, height / spriteSize.y, 1f);
        go.transform.position = new Vector3(x, (bottom + top) * 0.5f, 0f) - (Vector3)Vector2.Scale(spriteCenter, go.transform.localScale);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = spriteSize;
        col.offset = spriteCenter;

        BossRoomDoor door = go.AddComponent<BossRoomDoor>();
        SerializedObject so = new SerializedObject(door);
        so.FindProperty("startClosed").boolValue = startClosed;
        so.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    static Sprite LoadDoorSprite()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        SpriteRenderer sr = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (sr == null || sr.sprite == null) log.Add("!! 감염 혈관 문 스프라이트를 못 찾음 (문이 안 보일 수 있음)");
        return sr != null ? sr.sprite : null;
    }

    static BreakablePlatform CreatePlatform(Transform parent, string name, float x)
    {
        const float width = 3f, thickness = 0.5f, topY = 3.14f;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.tag = "Ground";
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) go.layer = ground;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
        if (sprite == null) log.Add("!! 발판 그림(" + PlatformSpritePath + ")을 못 찾음");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 5;
        Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        Vector2 spriteCenter = sprite != null ? (Vector2)sprite.bounds.center : Vector2.zero;

        go.transform.localScale = new Vector3(width / spriteSize.x, thickness / spriteSize.y, 1f);
        go.transform.position = new Vector3(x, topY - thickness * 0.5f, 0f) - (Vector3)Vector2.Scale(spriteCenter, go.transform.localScale);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = spriteSize;
        col.offset = spriteCenter;

        return go.AddComponent<BreakablePlatform>();
    }

    // ---------------- 공통 ----------------

    // 블록(회색 사각형 + 콜라이더 + 자식 땅마스크/땅바탕)을 주어진 범위로 옮기고 가로/세로를 맞춥니다.
    static void SetBlockRect(Transform t, float x0, float x1, float y0, float y1)
    {
        float w = x1 - x0, h = y1 - y0;
        BoxCollider2D col = t.GetComponent<BoxCollider2D>();
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        float oldH = col != null ? col.size.y : (sr != null ? sr.size.y : h);

        Undo.RecordObject(t, "블록 크기");
        t.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, t.position.z);

        if (sr != null) { Undo.RecordObject(sr, "블록 크기"); sr.size = new Vector2(w, h); }
        if (col != null) { Undo.RecordObject(col, "블록 크기"); col.size = new Vector2(w, h); col.offset = Vector2.zero; }

        foreach (Transform c in t)
        {
            // 자식 그림은 블록 가운데 기준이라 가로만 맞추면 됩니다 (세로 크기가 바뀌면 같은 비율로 맞춤).
            Undo.RecordObject(c, "블록 그림");
            Vector3 lp = c.localPosition;
            lp.x = 0f;
            if (!Mathf.Approximately(oldH, h) && oldH > 0f) lp.y *= h / oldH;
            c.localPosition = lp;

            if (c.name.StartsWith("땅마스크"))
            {
                Vector3 s = c.localScale;
                c.localScale = new Vector3(w, Mathf.Approximately(oldH, h) ? s.y : s.y * h / oldH, s.z);
            }
            else
            {
                SpriteRenderer csr = c.GetComponent<SpriteRenderer>();
                if (csr != null && csr.drawMode != SpriteDrawMode.Simple)
                {
                    Undo.RecordObject(csr, "블록 그림");
                    csr.size = new Vector2(w, Mathf.Approximately(oldH, h) ? csr.size.y : csr.size.y * h / oldH);
                }
            }
        }
    }

    static Transform Duplicate(Transform src, Vector3 offset, string undoName)
    {
        GameObject copy = Object.Instantiate(src.gameObject, src.parent);
        copy.name = src.name;
        Undo.RegisterCreatedObjectUndo(copy, undoName);
        copy.transform.position = src.position + offset;
        return copy.transform;
    }

    static void Destroy(string path)
    {
        Transform t = Find(path);
        if (t == null) { log.Add("(없음, 건너뜀) " + path); return; }
        Undo.DestroyObjectImmediate(t.gameObject);
        log.Add("삭제: " + path);
    }

    static Transform Find(string path)
    {
        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);
        foreach (GameObject r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name != rootName) continue;
            if (slash < 0) return r.transform;
            Transform found = r.transform.Find(path.Substring(slash + 1));
            if (found != null) return found;
        }
        return null;
    }
}
