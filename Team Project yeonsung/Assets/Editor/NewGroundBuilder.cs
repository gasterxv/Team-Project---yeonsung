using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 새 맵용 땅 블록을 만드는 도구.
// 모양: AI 땅 무늬(ground_mid)를 가로로만 반복해서 깔고, 양 끝은 원본 둥근 끝 모양 틀(SpriteMask)로 오려낸 뒤
//       원본의 바깥 검은 테두리(ground_end_outline)만 위에 얹는다. 그래서 길이와 상관없이 무늬가 끝까지 이어진다.
// 블록 기준점 = 왼쪽 끝 윗면. 콜라이더 윗면 = 보이는 평평한 윗면.
// 메뉴: Tools > 새맵 > 땅 블록 샘플 놓기   (MainScene_Rework 씬에서만 동작)
public static class NewGroundBuilder
{
    const string Folder = "Assets/Map/NewGround/";
    const string MidPath = Folder + "ground_mid.png";
    const string MaskPath = Folder + "ground_end_mask.png";
    const string OutlinePath = Folder + "ground_end_outline.png";
    const string SquarePath = "Assets/Map/Square.png";
    const string SceneName = "MainScene_Rework";
    const string RootName = "새맵_땅블록";
    const string SortingLayerName = "Ground";

    const float PPU = 200f;
    const float TexH = 724f;            // 그림 세로 픽셀
    const float SurfaceFromTop = 183f;  // 그림 위에서 윗면까지
    const float VisibleBottom = 600f;   // 그림 위에서 아랫면까지
    const float EndPx = 70f;            // 끝 모양 틀 폭

    public const float Thickness = (VisibleBottom - SurfaceFromTop) / PPU; // 약 2.085 유닛
    const float EndW = EndPx / PPU;
    const float RectH = TexH / PPU;
    const float PivotY = (TexH - SurfaceFromTop) / TexH;   // 윗면이 기준점에 오도록

    [MenuItem("Tools/새맵/땅 블록 샘플 놓기")]
    public static void PlaceSample()
    {
        if (EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("새맵", SceneName + " 씬을 먼저 열어 주세요.\n(Assets/Scenes/" + SceneName + ".unity)", "확인");
            return;
        }

        Transform root = GetRoot();
        if (root.Find("샘플_긴땅") != null)
        {
            EditorUtility.DisplayDialog("새맵", "샘플이 이미 있어요. (" + RootName + " 아래)", "확인");
            Selection.activeGameObject = root.gameObject;
            return;
        }

        if (!PrepareTextures())
        {
            return;
        }

        Undo.SetCurrentGroupName("땅 블록 샘플 놓기");
        int group = Undo.GetCurrentGroup();

        // 지금 맵(y -4 ~ 26)과 겹치지 않게 아래쪽 빈 곳에 놓습니다.
        CreateBlock(root, "샘플_긴땅", 0f, 21.08f, -20f);
        CreateBlock(root, "샘플_짧은땅", 24f, 29f, -18f);
        CreateBlock(root, "샘플_중간땅", 32f, 45.5f, -20f);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
        Debug.Log("[새맵] 땅 블록 샘플 3개를 놓았어요 (y -20 근처). 확인 후 Ctrl+S로 저장하세요.");
    }

    // 블록 하나: x0 ~ x1, 윗면 높이 top
    public static GameObject CreateBlock(Transform parent, string name, float x0, float x1, float top)
    {
        float w = Mathf.Max(x1 - x0, EndW * 2f + 0.1f);
        int baseOrder = NextBaseOrder(parent);
        int layerId = SortingLayer.NameToID(SortingLayerName);

        Sprite mid = AssetDatabase.LoadAssetAtPath<Sprite>(MidPath);
        Sprite mask = AssetDatabase.LoadAssetAtPath<Sprite>(MaskPath);
        Sprite outline = AssetDatabase.LoadAssetAtPath<Sprite>(OutlinePath);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "땅 블록");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x0, top, 0f);
        go.tag = "Ground";
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            go.layer = groundLayer;
        }

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(w, Thickness);
        col.offset = new Vector2(w * 0.5f, -Thickness * 0.5f);

        // 무늬: 가로로만 반복 (세로는 그림 한 장 높이 그대로)
        GameObject pattern = Child(go, "무늬", new Vector3(w * 0.5f, 0f, 0f), Vector3.one);
        SpriteRenderer sr = pattern.AddComponent<SpriteRenderer>();
        sr.sprite = mid;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(w, RectH);
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = baseOrder + 1;

        // 모양 틀: 왼끝, 가운데, 오른끝(왼끝을 좌우 반전)
        AddMask(go, "틀_왼끝", mask, new Vector3(0f, 0f, 0f), Vector3.one, layerId, baseOrder);
        AddMask(go, "틀_오른끝", mask, new Vector3(w, 0f, 0f), new Vector3(-1f, 1f, 1f), layerId, baseOrder);
        float midW = w - EndW * 2f;
        float rectTop = (1f - PivotY) * RectH;
        float rectBottom = -PivotY * RectH;
        AddMask(go, "틀_가운데", square, new Vector3(w * 0.5f, (rectTop + rectBottom) * 0.5f, 0f),
            new Vector3(midW + 0.002f, RectH + 0.2f, 1f), layerId, baseOrder);

        // 바깥 검은 테두리
        AddOutline(go, "테두리_왼끝", outline, new Vector3(0f, 0f, 0f), Vector3.one, layerId, baseOrder + 2);
        AddOutline(go, "테두리_오른끝", outline, new Vector3(w, 0f, 0f), new Vector3(-1f, 1f, 1f), layerId, baseOrder + 2);

        return go;
    }

    static GameObject Child(GameObject parent, string name, Vector3 localPos, Vector3 localScale)
    {
        GameObject c = new GameObject(name);
        c.transform.SetParent(parent.transform, false);
        c.transform.localPosition = localPos;
        c.transform.localScale = localScale;
        c.layer = parent.layer;
        return c;
    }

    static void AddMask(GameObject parent, string name, Sprite sprite, Vector3 pos, Vector3 scale, int layerId, int baseOrder)
    {
        GameObject c = Child(parent, name, pos, scale);
        SpriteMask m = c.AddComponent<SpriteMask>();
        m.sprite = sprite;
        m.alphaCutoff = 0.5f;
        // 이 블록의 무늬(baseOrder+1)에만 영향을 주도록 범위를 좁힘
        m.isCustomRangeActive = true;
        m.frontSortingLayerID = layerId;
        m.backSortingLayerID = layerId;
        m.frontSortingOrder = baseOrder + 1;
        m.backSortingOrder = baseOrder;
    }

    static void AddOutline(GameObject parent, string name, Sprite sprite, Vector3 pos, Vector3 scale, int layerId, int order)
    {
        GameObject c = Child(parent, name, pos, scale);
        SpriteRenderer sr = c.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
    }

    // 블록마다 정렬 순서 3칸씩 따로 써서, 한 블록의 틀이 다른 블록 무늬를 오려내지 않게 함
    static int NextBaseOrder(Transform parent)
    {
        int count = 0;
        foreach (Transform t in parent)
        {
            if (t.GetComponent<BoxCollider2D>() != null)
            {
                count++;
            }
        }
        return -3000 + count * 3;
    }

    static Transform GetRoot()
    {
        GameObject r = GameObject.Find(RootName);
        if (r == null)
        {
            r = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(r, "새맵 땅 묶음");
        }
        return r.transform;
    }

    // 그림 가져오기 설정: 1유닛 = 200픽셀, 기준점 = 윗면
    static bool PrepareTextures()
    {
        bool ok = SetupSprite(MidPath, new Vector2(0.5f, PivotY), 4096);
        ok &= SetupSprite(MaskPath, new Vector2(0f, PivotY), 1024);
        ok &= SetupSprite(OutlinePath, new Vector2(0f, PivotY), 1024);
        if (AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath) == null)
        {
            Debug.LogError("[새맵] " + SquarePath + " 스프라이트가 없어요.");
            ok = false;
        }
        return ok;
    }

    static bool SetupSprite(string path, Vector2 pivot, int maxSize)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError("[새맵] 그림을 찾지 못했어요: " + path);
            return false;
        }

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        bool same = ti.textureType == TextureImporterType.Sprite
            && ti.spriteImportMode == SpriteImportMode.Single
            && Mathf.Approximately(ti.spritePixelsPerUnit, PPU)
            && s.spriteMeshType == SpriteMeshType.FullRect
            && s.spriteAlignment == (int)SpriteAlignment.Custom
            && Vector2.Distance(s.spritePivot, pivot) < 0.0001f
            && ti.maxTextureSize == maxSize
            && !ti.mipmapEnabled;
        if (same)
        {
            return true;
        }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = maxSize;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
        return true;
    }
}
