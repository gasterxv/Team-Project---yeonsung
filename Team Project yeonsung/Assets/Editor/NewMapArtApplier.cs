using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 그림 넣기 (메인씬 테스트). 메뉴: Tools > 새맵 > 8. 그림 넣기 (항체·나노 캡슐·HUD)
// - C 튕김 항체: 작은 것(O1·O2) = 발판1, 큰 것 = 발판2. 튕기는 순간 눌린 그림(발판1-2 / 발판2-1)
//   충돌 크기(1 × 1)는 그대로, 그림만 자식으로 붙여 크기를 맞춤 (그림 윗면 = 밟는 면)
// - 나노 캡슐: [나노 캡슐 N] 표시 → 팀원 'Nano capsule' 프리팹(회복 그림)으로
// - HUD 왼쪽 위 캡슐 그림 숨기기 (숫자만 남김)
// 여러 번 눌러도 됨 (이미 된 것은 건너뜀). 지우는 것 없음.
public static class NewMapArtApplier
{
    const string ArtDir = "Assets/Map/NewMapArt";
    const string NanoCapsulePath = "Assets/Prefabs/Enemy/Item/Nano capsule.prefab";
    const float AntibodyVisualWidth = 1.8f;   // 그림 가로 (충돌은 1)

    static readonly List<string> report = new List<string>();

    [MenuItem("Tools/새맵/8. 그림 넣기 (항체·나노 캡슐·HUD)")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != "메인씬 테스트")
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끄고 '메인씬 테스트' 씬에서 실행해 주세요.", "확인");
            return;
        }
        report.Clear();
        SetupImporters(ArtDir + "/항체", 200);

        Undo.SetCurrentGroupName("새맵 그림 넣기");
        int group = Undo.GetCurrentGroup();

        ApplyAntibodies();
        PlaceNanoCapsules();
        AddHudHider();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 그림 넣기 (" + report.Count + "건). Ctrl+S로 저장해 주세요.\n- " + string.Join("\n- ", report));
    }

    // 폴더 안 PNG를 스프라이트로 (투명 배경, 한 장짜리)
    public static void SetupImporters(string folder, int ppu)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single
                           || Mathf.Abs(ti.spritePixelsPerUnit - ppu) > 0.01f || !ti.alphaIsTransparency;
            if (!changed) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
    }

    static Sprite Load(string path) { return AssetDatabase.LoadAssetAtPath<Sprite>(path); }

    // prefix + "0.png", "1.png", "2.png" 세 장이 다 있으면 배열, 아니면 null
    static Sprite[] Frames(string prefix)
    {
        Sprite[] f = { Load(prefix + "0.png"), Load(prefix + "1.png"), Load(prefix + "2.png") };
        return f[0] != null && f[1] != null && f[2] != null ? f : null;
    }

    static void ApplyAntibodies()
    {
        // 밟고 뛰는 애니메이션(0·1·2)이 있으면 그걸 씀: 작은 것 = foothold, 큰 것 = foothold2. 없으면 예전 한 장 그림
        Sprite[] smallFrames = Frames(ArtDir + "/항체/애니/foothold_");
        Sprite[] bigFrames = Frames(ArtDir + "/항체/애니/foothold2_");
        Sprite small = smallFrames != null ? smallFrames[0] : Load(ArtDir + "/항체/발판1.png");
        Sprite big = bigFrames != null ? bigFrames[0] : Load(ArtDir + "/항체/발판2.png");
        Sprite smallPressed = smallFrames != null ? null : Load(ArtDir + "/항체/발판1-2.png");
        Sprite bigPressed = bigFrames != null ? null : Load(ArtDir + "/항체/발판2-1.png");
        if (small == null || big == null) { report.Add("항체 그림을 못 찾음 (" + ArtDir + "/항체)"); return; }

        foreach (BounceAntibody b in Object.FindObjectsByType<BounceAntibody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isBig = b.name.Contains("큰");
            Sprite normal = isBig ? big : small, pressed = isBig ? bigPressed : smallPressed;
            BoxCollider2D col = b.GetComponent<BoxCollider2D>();
            SpriteRenderer box = b.GetComponent<SpriteRenderer>();

            Transform vt = b.transform.Find("그림");
            if (vt == null)
            {
                GameObject v = new GameObject("그림");
                Undo.RegisterCreatedObjectUndo(v, "항체 그림");
                v.transform.SetParent(b.transform, false);
                vt = v.transform;
            }
            SpriteRenderer vr = vt.GetComponent<SpriteRenderer>();
            if (vr == null) vr = Undo.AddComponent<SpriteRenderer>(vt.gameObject);
            Undo.RecordObject(vr, "항체 그림");
            vr.sprite = normal;
            vr.drawMode = SpriteDrawMode.Simple;
            vr.color = Color.white;
            vr.sortingOrder = box != null ? box.sortingOrder : 9;

            // 크기: 가로 AntibodyVisualWidth, 그림 윗면 = 충돌 윗면 (부모 크기조절 고려)
            Vector3 parentScale = b.transform.lossyScale;
            Vector2 spriteSize = normal.bounds.size;
            float s = AntibodyVisualWidth / spriteSize.x;
            Undo.RecordObject(vt, "항체 그림 크기");
            vt.localScale = new Vector3(s / Mathf.Abs(parentScale.x), s / Mathf.Abs(parentScale.y), 1f);
            float colTopLocal = (col != null ? col.offset.y + col.size.y * 0.5f : 0.5f);
            float visualHalfH = spriteSize.y * s * 0.5f / Mathf.Abs(parentScale.y);
            float spriteCenterOffset = (normal.bounds.center.y) * s / Mathf.Abs(parentScale.y);
            vt.localPosition = new Vector3(0f, colTopLocal - visualHalfH - spriteCenterOffset + 0.05f, 0f);

            if (box != null) { Undo.RecordObject(box, "네모 숨김"); box.enabled = false; }
            Undo.RecordObject(b, "항체 그림 연결");
            b.visual = vr;
            b.pressedSprite = pressed;
            b.bounceFrames = isBig ? bigFrames : smallFrames;
            report.Add(b.name + ": " + normal.name + (b.bounceFrames != null ? " (+ 밟고 뛰는 애니메이션 0→1→2→0)" : pressed != null ? " (+ 눌린 그림 " + pressed.name + ")" : ""));
        }
    }

    static void PlaceNanoCapsules()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NanoCapsulePath);
        if (prefab == null) { report.Add("나노 캡슐 프리팹을 못 찾음: " + NanoCapsulePath); return; }
        GameObject root = GameObject.Find("새맵_회색상자");
        if (root == null) return;

        Transform parent = root.transform.Find("나노 캡슐");
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("[나노 캡슐") || !t.gameObject.activeSelf) continue;
            if (parent == null)
            {
                GameObject g = new GameObject("나노 캡슐");
                Undo.RegisterCreatedObjectUndo(g, "나노 캡슐 묶음");
                g.transform.SetParent(root.transform, false);
                parent = g.transform;
            }
            SpriteRenderer marker = t.GetComponent<SpriteRenderer>();
            Vector3 pos = marker != null ? marker.bounds.center : t.position;
            GameObject cap = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(cap, "나노 캡슐");
            cap.transform.position = new Vector3(pos.x, pos.y, cap.transform.position.z);
            cap.name = "Nano capsule " + t.name.Replace("[나노 캡슐", "").Replace("]", "").Trim();
            Undo.RecordObject(t.gameObject, "표시 끄기");
            t.gameObject.SetActive(false);
            report.Add("나노 캡슐 배치: " + t.name + " (" + pos.x.ToString("0.#") + ", " + pos.y.ToString("0.#") + ")");
        }
    }

    static void AddHudHider()
    {
        CheckpointResetManager rules = Object.FindFirstObjectByType<CheckpointResetManager>(FindObjectsInactive.Include);
        if (rules == null || rules.GetComponent<HudCapsuleIconHider>() != null) return;
        Undo.AddComponent<HudCapsuleIconHider>(rules.gameObject);
        report.Add("HUD 캡슐 그림 숨기기 (숫자만 남김)");
    }
}
