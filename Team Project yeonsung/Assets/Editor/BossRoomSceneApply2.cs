using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 보스방 수정 2차: 발판 그림 비율, 천장 높이 원래대로(y 13.16), 출구 문 높이
// 메뉴: Tools > 메인씬 > 보스방 수정 2 (발판 비율·천장)   — Ctrl+Z 한 번으로 되돌릴 수 있어요.
public static class BossRoomSceneApply2
{
    const string WallCeil = "Map_순환계/설계도 지형/벽과 천장";
    const string CeilingArt = "Map_순환계/설계도 지형/땅그림_v2/천장 AI y13.16 x210";
    const float CeilingRaise = 6.14f;

    // 발판: 그림 비율 그대로 가로 3, 밟는 면은 그림 위쪽 띠 (위에서 25%~57%)
    const float PlatformWidth = 3f, PlatformTopY = 3.14f;
    const float WalkTop = 0.25f, WalkBottom = 0.57f, WalkWidth = 0.9f;

    static readonly List<string> log = new List<string>();

    [MenuItem("Tools/메인씬/보스방 수정 2 (발판 비율·천장)")]
    static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "#MainScenes#")
        {
            EditorUtility.DisplayDialog("보스방 수정 2", "#MainScenes# 씬을 열고 실행해주세요.", "확인");
            return;
        }
        Transform roomCeiling = Find(WallCeil + "/지형 03 보스방");
        if (roomCeiling == null)
        {
            EditorUtility.DisplayDialog("보스방 수정 2", "이미 적용되었거나, 1차 수정(보스방·혈류 수정 적용)이 안 되어 있어요.", "확인");
            return;
        }

        log.Clear();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("보스방 수정 2");

        FixPlatforms();
        RestoreCeiling(roomCeiling);
        ShrinkExitDoor();
        // 보스 시작 위치는 사용자가 이미 x 289.6으로 옮겨 둠 → 건드리지 않음 (MoveBoss 미사용)

        CompositeCollider2D comp = Find(WallCeil)?.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.GenerateGeometry();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[보스방 수정 2]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("보스방 수정 2", "적용 완료! Ctrl+S로 저장해주세요.\n(자세한 내용은 Console 로그)", "확인");
    }

    static void FixPlatforms()
    {
        foreach (BreakablePlatform p in Object.FindObjectsByType<BreakablePlatform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            BoxCollider2D col = p.GetComponent<BoxCollider2D>();
            if (sr == null || sr.sprite == null || col == null) { log.Add("!! " + p.name + ": 그림/콜라이더 없음"); continue; }

            Bounds sb = sr.sprite.bounds;                // 그림 로컬 크기 (스케일 1 기준)
            float scale = PlatformWidth / sb.size.x;     // 가로 3에 맞춘 같은 비율 스케일
            float x = p.transform.position.x;

            float top = sb.max.y - sb.size.y * WalkTop;          // 밟는 면 위 (로컬)
            float bottom = sb.max.y - sb.size.y * WalkBottom;    // 밟는 면 아래 (로컬)

            Undo.RecordObject(p.transform, "발판 비율");
            Undo.RecordObject(col, "발판 비율");
            p.transform.localScale = new Vector3(scale, scale, 1f);
            col.size = new Vector2(sb.size.x * WalkWidth, top - bottom);
            col.offset = new Vector2(sb.center.x, (top + bottom) * 0.5f);
            // 밟는 면 윗선이 y 3.14에 오도록
            p.transform.position = new Vector3(x - sb.center.x * scale, PlatformTopY - top * scale, p.transform.position.z);

            log.Add(p.name + ": 그림 비율 유지 " + (sb.size.x * scale).ToString("F2") + " x " + (sb.size.y * scale).ToString("F2")
                + ", 밟는 면 윗선 y " + PlatformTopY);
        }
    }

    static void RestoreCeiling(Transform roomCeiling)
    {
        Transform left = Find(WallCeil + "/지형 03");
        if (left != null)
        {
            SetWidth(left, 209.87f, 303.5f);
            log.Add("지형 03: x 209.87~303.5 (y 13.16~20.45, 원래 높이)");
        }
        Undo.DestroyObjectImmediate(roomCeiling.gameObject);
        log.Add("삭제: 지형 03 보스방");

        Transform tiles = Find(CeilingArt);
        if (tiles == null) return;

        var list = new List<Transform>();
        foreach (Transform c in tiles) list.Add(c);

        // 올렸던 타일을 다시 내리고, 같은 자리에 두 장이 겹치면 하나는 지움
        var seen = new List<Vector3>();
        foreach (Transform c in list)
        {
            if (c.position.y > 18f)
            {
                Undo.RecordObject(c, "천장 그림 내리기");
                c.position += Vector3.down * CeilingRaise;
            }
            bool dup = false;
            foreach (Vector3 s in seen) if ((s - c.position).sqrMagnitude < 0.01f) dup = true;
            if (dup) { Undo.DestroyObjectImmediate(c.gameObject); log.Add("천장 그림 겹친 타일 1장 삭제"); }
            else seen.Add(c.position);
        }
        log.Add("천장 그림: 원래 높이로 내림");
    }

    static void ShrinkExitDoor()
    {
        Transform door = Find("Map_순환계/9_보스방/[보스방] 출구 문");
        if (door == null) { log.Add("!! 출구 문 없음"); return; }
        SpriteRenderer sr = door.GetComponent<SpriteRenderer>();
        Vector2 size = sr != null && sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        Vector2 center = sr != null && sr.sprite != null ? (Vector2)sr.sprite.bounds.center : Vector2.zero;
        float bottom = 0.94f, top = 13.16f;

        Undo.RecordObject(door, "출구 문 높이");
        Vector3 s = door.localScale;
        door.localScale = new Vector3(s.x, (top - bottom) / size.y, 1f);
        door.position = new Vector3(door.position.x, (bottom + top) * 0.5f - center.y * door.localScale.y, door.position.z);
        log.Add("출구 문: 높이 y 0.94~13.16");
    }

    // 블록 가로만 바꿈 (자식 땅마스크/땅바탕 포함)
    static void SetWidth(Transform t, float x0, float x1)
    {
        float w = x1 - x0;
        Undo.RecordObject(t, "블록 크기");
        t.position = new Vector3((x0 + x1) * 0.5f, t.position.y, t.position.z);
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        BoxCollider2D col = t.GetComponent<BoxCollider2D>();
        if (sr != null) { Undo.RecordObject(sr, "블록 크기"); sr.size = new Vector2(w, sr.size.y); }
        if (col != null) { Undo.RecordObject(col, "블록 크기"); col.size = new Vector2(w, col.size.y); col.offset = new Vector2(0f, col.offset.y); }
        foreach (Transform c in t)
        {
            Undo.RecordObject(c, "블록 그림");
            c.localPosition = new Vector3(0f, c.localPosition.y, c.localPosition.z);
            if (c.name.StartsWith("땅마스크")) c.localScale = new Vector3(w, c.localScale.y, c.localScale.z);
            else
            {
                SpriteRenderer csr = c.GetComponent<SpriteRenderer>();
                if (csr != null && csr.drawMode != SpriteDrawMode.Simple) { Undo.RecordObject(csr, "블록 그림"); csr.size = new Vector2(w, csr.size.y); }
            }
        }
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
