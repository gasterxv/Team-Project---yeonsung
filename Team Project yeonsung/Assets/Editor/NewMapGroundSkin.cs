using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 땅 그림 입히기 (메인씬 테스트). 메뉴: Tools > 새맵 > 9. 땅 그림 입히기 (다시 누르면 새로 만듦)
// - 새맵_회색상자/땅 아래 땅 블록들의 충돌 범위를 0.1칸 격자로 읽어서, 땅과 빈 공간의 경계마다 땅 띠 그림을 깔아 줌
//   바닥 = 그대로, 천장 = 뒤집어서, 벽 = 옆으로 돌려서. 바닥·천장이 끝나 아래(위)로 꺾이는 모서리에는 둥근 끝 조각
// - 그림 크기는 어디서나 똑같음. 얇은 땅(바닥과 아래 천장 사이가 좁은 곳)은 그림을 줄이지 않고,
//   두께의 절반까지만 보이게 잘라 냄(SpriteMask) → 반대쪽 띠와 겹치지 않음
// - 띠 그림은 아래로 갈수록 어두워지므로, 땅 블록 자체(안쪽)는 그 어두운 색으로 칠함
// - 그리는 순서: 땅 안쪽 < 벽 < 천장 < 바닥 (안쪽 모서리에서 바닥이 벽 위에 보이게)
// - 충돌은 전혀 안 바꿈. 그림만 '땅 그림' 묶음에 새로 만듦 (다시 누르면 이 묶음만 지우고 새로 만듦)
public static class NewMapGroundSkin
{
    const string Dir = "Assets/Map/NewMapArt/땅/가공/";
    const string MidPath = Dir + "땅_가운데_반복.png";
    const string CapLPath = Dir + "땅_왼끝.png";
    const string CapRPath = Dir + "땅_오른끝.png";
    const string SquarePath = "Assets/Map/Square.png";
    const string RootName = "새맵_회색상자";
    const string SkinName = "땅 그림";

    const float PPU = 500f;
    const float TexH = 1327f;
    const float SurfaceFromTop = 117f;              // 그림 위에서 밟는 면까지 (px)
    const float PivotY = (TexH - SurfaceFromTop) / TexH;
    const float Depth = (TexH - SurfaceFromTop) / PPU; // 밟는 면 아래로 그림 깊이 ≈ 2.42
    const float AboveSurface = SurfaceFromTop / PPU + 0.05f; // 밟는 면 위로 튀어나온 혹 높이
    const float MidW = 2408f / PPU;                    // 가운데 타일 폭 ≈ 4.82
    const float CapLW = 664f / PPU, CapRW = 708f / PPU;
    const float CapLPivotX = 40f / 664f, CapRPivotX = 664f / 708f;   // 끝 조각의 옆면 위치

    const float Cell = 0.1f;
    static readonly Color FillColor = new Color(0.11f, 0.035f, 0.04f);   // 띠 아래쪽 어두운 색
    const int FillOrder = -4000, WallOrder = -3000, CeilingOrder = -2000, FloorOrder = -1000;

    static bool[,] solid;
    static float minX, minY;
    static int gw, gh;
    static Transform skinRoot;
    static Sprite mid, capL, capR, square;
    static int pieces, masks;
    static int nextWall, nextCeiling, nextFloor;

    [MenuItem("Tools/새맵/9. 땅 그림 입히기 (다시 누르면 새로 만듦)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != "메인씬 테스트")
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끄고 '메인씬 테스트' 씬에서 실행해 주세요.", "확인");
            return;
        }
        GameObject root = GameObject.Find(RootName);
        Transform groundGroup = root != null ? root.transform.Find("땅") : null;
        if (groundGroup == null) { Debug.LogWarning("[새맵] '" + RootName + "/땅' 묶음이 없어요."); return; }

        SetupImporters();
        mid = AssetDatabase.LoadAssetAtPath<Sprite>(MidPath);
        capL = AssetDatabase.LoadAssetAtPath<Sprite>(CapLPath);
        capR = AssetDatabase.LoadAssetAtPath<Sprite>(CapRPath);
        square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (mid == null || capL == null || capR == null || square == null) { Debug.LogWarning("[새맵] 땅 그림을 못 찾았어요: " + Dir); return; }

        Undo.SetCurrentGroupName("새맵 땅 그림");
        int group = Undo.GetCurrentGroup();

        Transform old = root.transform.Find(SkinName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject s = new GameObject(SkinName);
        Undo.RegisterCreatedObjectUndo(s, SkinName);
        s.transform.SetParent(root.transform, false);
        skinRoot = s.transform;
        pieces = masks = 0;
        nextWall = WallOrder; nextCeiling = CeilingOrder; nextFloor = FloorOrder;

        List<BoxCollider2D> blocks = new List<BoxCollider2D>();
        foreach (BoxCollider2D c in groundGroup.GetComponentsInChildren<BoxCollider2D>(true))
            if (!c.isTrigger && c.gameObject.activeInHierarchy) blocks.Add(c);
        BuildGrid(blocks);

        int floors = 0, ceilings = 0, walls = 0;
        for (int j = 0; j < gh; j++)
        {
            floors += ScanRow(j, true);
            ceilings += ScanRow(j, false);
        }
        for (int i = 0; i < gw; i++)
        {
            walls += ScanColumn(i, true);
            walls += ScanColumn(i, false);
        }

        // 땅 블록 안쪽 = 띠 아래쪽 어두운 색, 맨 뒤에 그림
        foreach (BoxCollider2D c in blocks)
        {
            SpriteRenderer r = c.GetComponent<SpriteRenderer>();
            if (r == null) continue;
            Undo.RecordObject(r, "땅 색");
            r.color = FillColor;
            r.sortingOrder = FillOrder;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 땅 그림: 바닥 " + floors + "줄, 천장 " + ceilings + "줄, 벽 " + walls + "줄, 그림 조각 " + pieces
                  + "개, 얇은 곳 자르기 " + masks + "개. Ctrl+S로 저장해 주세요.");
    }

    // ---------------- 격자 ----------------

    static void BuildGrid(List<BoxCollider2D> blocks)
    {
        Bounds b = blocks[0].bounds;
        foreach (BoxCollider2D c in blocks) b.Encapsulate(c.bounds);
        minX = Mathf.Floor(b.min.x) - 1f; minY = Mathf.Floor(b.min.y) - 1f;
        gw = Mathf.CeilToInt((b.max.x + 1f - minX) / Cell);
        gh = Mathf.CeilToInt((b.max.y + 1f - minY) / Cell);
        solid = new bool[gw, gh];
        foreach (BoxCollider2D c in blocks)
        {
            Bounds cb = c.bounds;
            int x0 = Mathf.RoundToInt((cb.min.x - minX) / Cell), x1 = Mathf.RoundToInt((cb.max.x - minX) / Cell);
            int y0 = Mathf.RoundToInt((cb.min.y - minY) / Cell), y1 = Mathf.RoundToInt((cb.max.y - minY) / Cell);
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(gw, x1); x++)
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(gh, y1); y++)
                    solid[x, y] = true;
        }
        FillSlivers();
    }

    // 블록 끝이 딱 안 맞아서 생긴 아주 좁은 빈틈(0.3칸 이하)은 땅으로 메움 → 그 틈을 따라 벽 그림(검은 세로줄)이 생기지 않게.
    // 그림용 격자만 바꾸고 충돌은 그대로. 실제 통로·방은 이보다 훨씬 넓어서 영향 없음.
    const int MaxSliver = 3;   // 칸 수 (0.1 × 3 = 0.3)
    static void FillSlivers()
    {
        int filled = 0;
        for (int j = 0; j < gh; j++)
        {
            int i = 0;
            while (i < gw)
            {
                if (solid[i, j]) { i++; continue; }
                int s = i;
                while (i < gw && !solid[i, j]) i++;
                if (s > 0 && i < gw && i - s <= MaxSliver)
                    for (int k = s; k < i; k++) { solid[k, j] = true; filled++; }
            }
        }
        for (int i = 0; i < gw; i++)
        {
            int j = 0;
            while (j < gh)
            {
                if (solid[i, j]) { j++; continue; }
                int s = j;
                while (j < gh && !solid[i, j]) j++;
                if (s > 0 && j < gh && j - s <= MaxSliver)
                    for (int k = s; k < j; k++) { solid[i, k] = true; filled++; }
            }
        }
        if (filled > 0) Debug.Log("[새맵] 땅 그림: 블록 사이 좁은 빈틈 " + filled + "칸을 그림에서는 땅으로 메움");
    }

    // 격자 밖은 땅으로 봄
    static bool S(int i, int j) { return i < 0 || j < 0 || i >= gw || j >= gh || solid[i, j]; }

    // 이 칸에서 안쪽으로 땅이 몇 칸 이어지는지 → 보여 줄 깊이 (두꺼우면 그림 전체, 얇으면 두께의 절반)
    static float DepthAt(int i, int j, int di, int dj)
    {
        int max = Mathf.CeilToInt(Depth * 2f / Cell), n = 0;
        while (n < max && S(i + di * n, j + dj * n)) n++;
        if (n >= max) return Depth;
        return Mathf.Round(n * Cell * 0.5f * 20f) / 20f;   // 0.05 단위
    }

    // 가로 경계: floor = 윗면(위가 빈 공간), 아니면 아랫면(천장)
    static int ScanRow(int j, bool floor)
    {
        int count = 0, i = 0;
        int airJ = floor ? j + 1 : j - 1;
        int inward = floor ? -1 : 1;
        while (i < gw)
        {
            if (!(S(i, j) && !S(i, airJ))) { i++; continue; }
            int start = i;
            List<float> depths = new List<float>();
            while (i < gw && S(i, j) && !S(i, airJ)) { depths.Add(DepthAt(i, j, 0, inward)); i++; }
            int end = i;
            float x0 = minX + start * Cell, x1 = minX + end * Cell;
            float y = minY + (floor ? j + 1 : j) * Cell;
            bool outerL = !S(start - 1, j) && !S(start - 1, airJ);
            bool outerR = !S(end, j) && !S(end, airJ);
            if (x1 - x0 < 0.25f) continue;
            if (floor) MakeRun(new Vector3(x0, y), 0f, x1 - x0, depths, outerL, outerR, ref nextFloor, "바닥");
            else { depths.Reverse(); MakeRun(new Vector3(x1, y), 180f, x1 - x0, depths, outerR, outerL, ref nextCeiling, "천장"); }
            count++;
        }
        return count;
    }

    // 세로 경계: leftFacing = 왼쪽이 빈 공간인 벽
    static int ScanColumn(int i, bool leftFacing)
    {
        int count = 0, j = 0;
        int airI = leftFacing ? i - 1 : i + 1;
        int inward = leftFacing ? 1 : -1;
        while (j < gh)
        {
            if (!(S(i, j) && !S(airI, j))) { j++; continue; }
            int start = j;
            List<float> depths = new List<float>();
            while (j < gh && S(i, j) && !S(airI, j)) { depths.Add(DepthAt(i, j, inward, 0)); j++; }
            int end = j;
            float y0 = minY + start * Cell, y1 = minY + end * Cell;
            float x = minX + (leftFacing ? i : i + 1) * Cell;
            if (y1 - y0 < 0.25f) continue;
            if (leftFacing) MakeRun(new Vector3(x, y0), 90f, y1 - y0, depths, false, false, ref nextWall, "벽");
            else { depths.Reverse(); MakeRun(new Vector3(x, y1), -90f, y1 - y0, depths, false, false, ref nextWall, "벽"); }
            count++;
        }
        return count;
    }

    // ---------------- 그림 조각 ----------------

    // origin에서 로컬 +x 방향으로 길이 L, 로컬 +y = 빈 공간 쪽. depths = 칸마다 보여 줄 깊이 (로컬 +x 순서)
    static void MakeRun(Vector3 origin, float rotZ, float L, List<float> depths, bool capStart, bool capEnd, ref int order, string kind)
    {
        int o = order;
        order += 2;   // 이 줄만 쓰는 그리기 순서 2칸 (가운데, 끝 조각)

        GameObject run = new GameObject(kind + " (" + origin.x.ToString("0.#") + ", " + origin.y.ToString("0.#") + ")");
        run.transform.SetParent(skinRoot, false);
        run.transform.position = origin;
        run.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);

        bool thin = false;
        foreach (float d in depths) if (d < Depth - 0.001f) { thin = true; break; }

        float capLW = capStart ? CapLW : 0f, capRW = capEnd ? CapRW : 0f;
        if (capLW + capRW > L * 0.8f) { capLW = capRW = 0f; capStart = capEnd = false; }   // 너무 짧으면 끝 조각 없이

        float midLen = L - capLW - capRW;
        int n = Mathf.Max(1, Mathf.RoundToInt(midLen / MidW));
        float w = midLen / n;
        // 조각 사이에 아주 작은 틈이 생겨 어두운 세로줄이 보이지 않게, 조각을 양옆으로 조금씩(0.02) 겹침
        const float Overlap = 0.02f;
        for (int k = 0; k < n; k++)
            Piece(run.transform, mid, capLW + k * w - Overlap, (w + Overlap * 2f) / MidW, o, thin);
        if (capStart) Piece(run.transform, capL, 0f, 1f, o + 1, thin);
        if (capEnd) Piece(run.transform, capR, L, 1f, o + 1, thin);

        if (thin) AddMasks(run.transform, depths, o);
    }

    static void Piece(Transform parent, Sprite sprite, float x, float sx, int order, bool masked)
    {
        GameObject g = new GameObject(sprite.name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(x, 0f, 0f);
        g.transform.localScale = new Vector3(sx, 1f, 1f);
        SpriteRenderer r = g.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        if (masked) r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        pieces++;
    }

    // 얇은 곳: 같은 깊이끼리 묶어서 '보이는 범위' 틀을 만듦 (이 줄의 그리기 순서에만 적용)
    static void AddMasks(Transform run, List<float> depths, int order)
    {
        int a = 0;
        while (a < depths.Count)
        {
            int b = a;
            while (b < depths.Count && Mathf.Approximately(depths[b], depths[a])) b++;
            float x0 = a * Cell - (a == 0 ? 0.3f : 0.01f);
            float x1 = b * Cell + (b == depths.Count ? 0.3f : 0.01f);
            float d = depths[a];
            GameObject m = new GameObject("보이는 범위 (깊이 " + d.ToString("0.##") + ")");
            m.transform.SetParent(run, false);
            m.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (AboveSurface - d) * 0.5f, 0f);
            m.transform.localScale = new Vector3(x1 - x0, AboveSurface + d, 1f);
            SpriteMask sm = m.AddComponent<SpriteMask>();
            sm.sprite = square;
            sm.isCustomRangeActive = true;
            int layer = SortingLayer.NameToID("Default");
            sm.frontSortingLayerID = layer; sm.backSortingLayerID = layer;
            sm.frontSortingOrder = order + 1;   // 범위: order-1 초과 ~ order+1 이하 = 이 줄의 가운데·끝 조각만
            sm.backSortingOrder = order - 1;
            masks++;
            a = b;
        }
    }

    // ---------------- 그림 설정 ----------------

    static void SetupImporters()
    {
        Setup(MidPath, new Vector2(0f, PivotY));
        Setup(CapLPath, new Vector2(CapLPivotX, PivotY));
        Setup(CapRPath, new Vector2(CapRPivotX, PivotY));
    }

    static void Setup(string path, Vector2 pivot)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        TextureImporterSettings st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        bool changed = ti.textureType != TextureImporterType.Sprite || st.spriteAlignment != (int)SpriteAlignment.Custom
                       || Vector2.Distance(st.spritePivot, pivot) > 0.0005f || Mathf.Abs(ti.spritePixelsPerUnit - PPU) > 0.01f
                       || ti.maxTextureSize < 4096;
        if (!changed) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = pivot;
        st.spritePixelsPerUnit = PPU;
        st.spriteMeshType = SpriteMeshType.FullRect;
        st.alphaIsTransparency = true;
        st.mipmapEnabled = false;
        st.wrapMode = TextureWrapMode.Clamp;
        ti.SetTextureSettings(st);
        ti.maxTextureSize = 4096;
        ti.SaveAndReimport();
    }
}
