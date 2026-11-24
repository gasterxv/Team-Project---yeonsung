using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 투명 블록 · 적혈구 발판 정리 (메인씬)
// - 지형 42~45: 그림만 옮겨져 있어서 충돌 블록을 그림 위치로 맞춤
// - 지형 41: 옮겨진 42와 같은 자리라 삭제
// - 지형 30, 31, 31-2, 31-3, 48: 그림이 없어 투명 → 다른 적혈구와 같은 그림 추가
// 메뉴: Tools > 메인씬 > 투명 블록·적혈구 수정   — Ctrl+Z 한 번으로 되돌릴 수 있어요.
public static class RedCellPlatformFix
{
    const string Terrain = "Map_순환계/설계도 지형";
    static readonly string[] AlignToArt = { "지형 42", "지형 43", "지형 44", "지형 45" };
    static readonly string[] AddArt = { "지형 30", "지형 31", "지형 31-2", "지형 31-3", "지형 48" };
    const string ArtSource = "지형 47";

    [MenuItem("Tools/메인씬/투명 블록·적혈구 수정")]
    static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "#MainScenes#")
        {
            EditorUtility.DisplayDialog("적혈구 수정", "#MainScenes# 씬을 열고 실행해주세요.", "확인");
            return;
        }
        Transform terrain = Find(Terrain);
        if (terrain == null) { EditorUtility.DisplayDialog("적혈구 수정", "설계도 지형을 못 찾았어요.", "확인"); return; }

        var log = new List<string>();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("투명 블록·적혈구 수정");

        // 1) 충돌 블록을 그림 위치로
        foreach (string n in AlignToArt)
        {
            Transform block = terrain.Find(n);
            Transform art = block != null ? FindArt(block) : null;
            if (art == null) { log.Add("(건너뜀) " + n + ": 블록이나 그림 없음"); continue; }
            if ((art.position - block.position).sqrMagnitude < 0.0001f) { log.Add("(이미 맞음) " + n); continue; }

            Vector3 artPos = art.position;
            Undo.RecordObject(block, "적혈구 위치");
            Undo.RecordObject(art, "적혈구 위치");
            block.position = new Vector3(artPos.x, artPos.y, block.position.z);
            art.position = artPos;   // 그림은 제자리
            log.Add(n + ": 충돌 블록을 그림 위치 (" + artPos.x.ToString("F2") + ", " + artPos.y.ToString("F2") + ")로 이동");
        }

        // 2) 41 삭제
        Transform b41 = terrain.Find("지형 41");
        if (b41 != null) { Undo.DestroyObjectImmediate(b41.gameObject); log.Add("삭제: 지형 41"); }

        // 3) 투명 블록에 그림 추가
        Transform srcBlock = terrain.Find(ArtSource);
        Transform srcArt = srcBlock != null ? FindArt(srcBlock) : null;
        if (srcArt == null) log.Add("!! " + ArtSource + " 그림을 못 찾아서 그림 추가를 못 함");
        else
        {
            foreach (string n in AddArt)
            {
                Transform block = terrain.Find(n);
                if (block == null) { log.Add("(없음) " + n); continue; }
                if (FindArt(block) != null) { log.Add("(이미 그림 있음) " + n); continue; }

                GameObject art = Object.Instantiate(srcArt.gameObject, block);
                art.name = "그림";
                Undo.RegisterCreatedObjectUndo(art, "적혈구 그림");
                art.transform.localPosition = Vector3.zero;
                art.transform.localRotation = srcArt.localRotation;
                art.transform.localScale = srcArt.localScale;
                log.Add(n + ": 적혈구 그림 추가");
            }
        }

        CompositeCollider2D comp = terrain.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.GenerateGeometry();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[투명 블록·적혈구 수정]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("적혈구 수정", "적용 완료! Ctrl+S로 저장해주세요.\n(자세한 내용은 Console 로그)", "확인");
    }

    // 바닥에 그냥 놓여 있던 적혈구 (발판 역할 없음) 삭제
    [MenuItem("Tools/메인씬/바닥 적혈구 삭제 (30, 31, 31-2, 31-3)")]
    static void RemoveFloorCells()
    {
        Transform terrain = Find(Terrain);
        if (terrain == null) return;
        var log = new List<string>();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        foreach (string n in new[] { "지형 30", "지형 31", "지형 31-2", "지형 31-3" })
        {
            Transform t = terrain.Find(n);
            if (t == null) { log.Add("(없음) " + n); continue; }
            Undo.DestroyObjectImmediate(t.gameObject);
            log.Add("삭제: " + n);
        }
        CompositeCollider2D comp = terrain.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.GenerateGeometry();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[바닥 적혈구 삭제]\n" + string.Join("\n", log));
    }

    static Transform FindArt(Transform block)
    {
        foreach (Transform c in block)
            if (c.name.StartsWith("그림") && c.GetComponent<SpriteRenderer>() != null) return c;
        return null;
    }

    static Transform Find(string path)
    {
        int slash = path.IndexOf('/');
        foreach (GameObject r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == path.Substring(0, slash)) return r.transform.Find(path.Substring(slash + 1));
        return null;
    }
}
