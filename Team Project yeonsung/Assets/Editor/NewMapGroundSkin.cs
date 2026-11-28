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
    // 줄 끝에서 끝 조각 안쪽 끝까지 거리 (기준점이 옆면이므로 기준점~안쪽 끝)
    const float CapLW = (664f - 34f) / PPU, CapRW = 602f / PPU;
    // 끝 조각의 옆면 위치 (몸통 높이에서 실제 그림이 끝나는 곳: 왼끝 약 30px, 오른끝 약 600px)
    const float CapLPivotX = 34f / 664f, CapRPivotX = 602f / 708f;

    // ---- 바닥 = 중경 땅 (중경_땅1·2를 밟는 면 맞춰 약 2칸씩 자른 조각 A00~A14, B00~B14 + 양 끝) ----
    const string FloorDir = "Assets/Map/NewMapArt/땅/바닥/";
    const float FPPU = 400f, FTexH = 1040f, FTop = 140f;
    const float FPivotY = (FTexH - FTop) / FTexH;
    const float FDepth = (FTexH - FTop) / FPPU;          // 밟는 면 아래 ≈ 2.25
    const float FAbove = FTop / FPPU + 0.05f;
    const float FCapLWpx = 520f, FCapLEdgePx = 80f;      // 왼끝 조각 폭, 옆면 위치
    const float FCapRWpx = 400f, FCapREdgePx = 223f;     // 오른끝 조각 폭, 옆면 위치
    const int SegPerStrip = 15;
    static Sprite[] floorSegs;
    static Sprite floorCapL, floorCapR;

    const float Cell = 0.1f;
    static readonly Color FillColor = new Color(0.11f, 0.035f, 0.04f);   // 띠 아래쪽 어두운 색
    // 그리는 순서: 메인씬 원경(배경)이 Default 층 -1000·-1001이라 그보다 앞(-900~)에 둠
    // 땅 안쪽 -900 < 벽 -850~ < 천장 -700~ < 바닥 -600~ (줄마다 2칸씩: 벽 최대 75줄, 천장 50줄, 바닥 이후 여유)
    const int FillOrder = -900, WallOrder = -850, CeilingOrder = -700, FloorOrder = -600;

    static bool[,] solid;
    static float minX, minY;
    static int gw, gh;
    static Transform skinRoot;
    static Sprite mid, capL, capR, square;
    static int pieces, masks, corners;
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
        floorSegs = new Sprite[SegPerStrip * 2];
        for (int k = 0; k < SegPerStrip; k++)
        {
            floorSegs[k] = AssetDatabase.LoadAssetAtPath<Sprite>(FloorDir + "중경땅_A_" + k.ToString("00") + ".png");
            floorSegs[SegPerStrip + k] = AssetDatabase.LoadAssetAtPath<Sprite>(FloorDir + "중경땅_B_" + k.ToString("00") + ".png");
        }
        floorCapL = AssetDatabase.LoadAssetAtPath<Sprite>(FloorDir + "중경땅_왼끝.png");
        floorCapR = AssetDatabase.LoadAssetAtPath<Sprite>(FloorDir + "중경땅_오른끝.png");
        foreach (Sprite fs in floorSegs) if (fs == null) { floorSegs = null; break; }
        if (floorSegs == null || floorCapL == null || floorCapR == null)
            Debug.LogWarning("[새맵] 중경 땅 바닥 조각을 못 찾아서 바닥도 벽 그림으로 깔아요: " + FloorDir);

        Undo.SetCurrentGroupName("새맵 땅 그림");
        int group = Undo.GetCurrentGroup();

        Transform old = root.transform.Find(SkinName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject s = new GameObject(SkinName);
        Undo.RegisterCreatedObjectUndo(s, SkinName);
        s.transform.SetParent(root.transform, false);
        skinRoot = s.transform;
        pieces = masks = corners = 0;
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
        if (nextWall > CeilingOrder || nextCeiling > FloorOrder || nextFloor > -10)
            Debug.LogWarning("[새맵] 땅 그림: 줄이 너무 많아 그리는 순서 범위가 겹쳐요 (벽 " + nextWall + ", 천장 " + nextCeiling + ", 바닥 " + nextFloor + "). 순서 범위를 넓혀 주세요.");
        Debug.Log("[새맵] 땅 그림: 바닥 " + floors + "줄, 천장 " + ceilings + "줄, 벽 " + walls + "줄, 그림 조각 " + pieces
                  + "개, 얇은 곳 자르기 " + masks + "개, 안쪽 모서리 채움 " + corners + "개. Ctrl+S로 저장해 주세요.");
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
            // 얇은 기둥 끝(폭 1칸 미만, 양쪽이 다 빈 공간): 띠 한 장을 찌그러뜨려 깔면 무늬 없는 분홍 막대가 됨 → 양옆 벽 띠만 보이게
            if (x1 - x0 < 1f && outerL && outerR) continue;
            // 안쪽 모서리(줄 끝 너머가 벽으로 막힘): 어느 띠도 덮지 않아 검은 네모가 보이던 곳 → 띠를 벽 속으로 조금 늘려 덮음
            float extL = InnerCornerLength(start - 1, j, -1, 0, 0, floor ? 1 : -1);
            float extR = InnerCornerLength(end, j, 1, 0, 0, floor ? 1 : -1);
            float sL = CornerDepthScale(start - 1, j, 0, inward), sR = CornerDepthScale(end, j, 0, inward);
            Transform run;
            if (floor && floorSegs != null)
            {
                run = MakeFloorRun(new Vector3(x0, y), x1 - x0, depths, outerL, outerR);
                CornerFill(run, floorSegs[0], -extL, extL, sL * Depth / FDepth > 1f ? 1f : sL * Depth / FDepth);
                CornerFill(run, floorSegs[SegPerStrip - 1], x1 - x0, extR, sR * Depth / FDepth > 1f ? 1f : sR * Depth / FDepth);
            }
            else if (floor)
            {
                run = MakeRun(new Vector3(x0, y), 0f, x1 - x0, depths, outerL, outerR, ref nextFloor, "바닥");
                CornerFill(run, mid, -extL, extL, sL);
                CornerFill(run, mid, x1 - x0, extR, sR);
            }
            else
            {
                depths.Reverse();
                run = MakeRun(new Vector3(x1, y), 180f, x1 - x0, depths, outerR, outerL, ref nextCeiling, "천장");
                CornerFill(run, mid, -extR, extR, sR);        // 천장은 뒤집혀 있어서 오른쪽이 줄의 시작
                CornerFill(run, mid, x1 - x0, extL, sL);
            }
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
            // 안쪽 모서리 채우기 (위 ScanRow 설명과 같음)
            // 벽 띠는 늘리지 않음 (바닥·천장 띠가 벽 밑으로 이어지며 모서리를 덮음. 벽 띠를 늘리면 바닥 아래로 밝은 토막이 삐져나옴)
            int airDi = leftFacing ? -1 : 1;
            float extB = 0f * InnerCornerLength(i, start - 1, 0, -1, airDi, 0), extT = 0f * InnerCornerLength(i, end, 0, 1, airDi, 0);
            float sB = CornerDepthScale(i, start - 1, inward, 0), sT = CornerDepthScale(i, end, inward, 0);
            if (leftFacing)
            {
                Transform run = MakeRun(new Vector3(x, y0), 90f, y1 - y0, depths, false, false, ref nextWall, "벽");
                CornerFill(run, mid, -extB, extB, sB);
                CornerFill(run, mid, y1 - y0, extT, sT);
            }
            else
            {
                depths.Reverse();
                Transform run = MakeRun(new Vector3(x, y1), -90f, y1 - y0, depths, false, false, ref nextWall, "벽");
                CornerFill(run, mid, -extT, extT, sT);
                CornerFill(run, mid, y1 - y0, extB, sB);
            }
            count++;
        }
        return count;
    }

    // ---------------- 안쪽 모서리 채우기 ----------------

    // (ci,cj)부터 (si,sj) 방향으로, 칸도 땅이고 빈 공간 쪽 옆칸(airDi,airDj)도 땅인 칸이 몇 칸 이어지는지 (최대 띠 깊이만큼)
    static float InnerCornerLength(int ci, int cj, int si, int sj, int airDi, int airDj)
    {
        int max = Mathf.CeilToInt(Depth / Cell), n = 0;
        while (n < max)
        {
            int a = ci + si * n, b = cj + sj * n;
            if (a < 0 || b < 0 || a >= gw || b >= gh) break;   // 맵 바깥은 채우지 않음
            if (!S(a, b) || !S(a + airDi, b + airDj)) break;
            n++;
        }
        return n * Cell;
    }

    // 모서리 칸에서 안쪽으로 보여 줄 깊이 비율 (얇으면 그림을 눌러서 반대쪽으로 삐져나가지 않게)
    static float CornerDepthScale(int ci, int cj, int di, int dj)
    {
        if (!S(ci, cj)) return 1f;
        return Mathf.Clamp01(DepthAt(ci, cj, di, dj) / Depth);
    }

    // 띠를 줄 끝 너머(벽 속)로 len만큼 늘린 조각. 모든 띠보다 뒤에 그려서(땅 안쪽 바로 위) 혹·끝이 다른 띠를 가리지 않음
    static void CornerFill(Transform run, Sprite sprite, float x, float len, float depthScale)
    {
        if (run == null || sprite == null || len < 0.15f) return;
        float natural = sprite.bounds.size.x;
        const float Overlap = 0.03f;
        GameObject g = new GameObject("모서리 채움");
        g.transform.SetParent(run, false);
        bool atStart = x < 0f;
        g.transform.localPosition = new Vector3(atStart ? x : x - Overlap, 0f, 0f);
        g.transform.localScale = new Vector3((len + Overlap) / natural, Mathf.Max(0.05f, depthScale), 1f);
        SpriteRenderer r = g.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = FillOrder + 1;
        corners++;
        pieces++;
    }

    // ---------------- 그림 조각 ----------------

    // origin에서 로컬 +x 방향으로 길이 L, 로컬 +y = 빈 공간 쪽. depths = 칸마다 보여 줄 깊이 (로컬 +x 순서)
    static Transform MakeRun(Vector3 origin, float rotZ, float L, List<float> depths, bool capStart, bool capEnd, ref int order, string kind)
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

        if (thin) AddMasks(run.transform, depths, o, AboveSurface);
        return run.transform;
    }

    // 바닥 한 줄: 중경 땅 조각을 이어서 깔기. 조각 순서는 A00→…→A14→B00→…→B14→A00 (끝끼리 섞어 둬서 이음매가 안 보임)
    static Transform MakeFloorRun(Vector3 origin, float L, List<float> depths, bool capStart, bool capEnd)
    {
        int o = nextFloor;
        nextFloor += 2;
        GameObject run = new GameObject("바닥 (" + origin.x.ToString("0.#") + ", " + origin.y.ToString("0.#") + ")");
        run.transform.SetParent(skinRoot, false);
        run.transform.position = origin;

        List<float> d2 = new List<float>();
        bool thin = false;
        foreach (float d in depths) { float v = Mathf.Min(d, FDepth); d2.Add(v); if (v < FDepth - 0.001f) thin = true; }

        float capLW = capStart ? (FCapLWpx - FCapLEdgePx) / FPPU : 0f;
        float capRW = capEnd ? FCapREdgePx / FPPU : 0f;
        if (capLW + capRW > L * 0.8f) { capLW = capRW = 0f; capStart = capEnd = false; }
        float midLen = L - capLW - capRW;

        int N = floorSegs.Length, half = N / 2;
        float avg = 0f;
        foreach (Sprite s in floorSegs) avg += s.bounds.size.x;
        avg /= N;
        int m = Mathf.Max(1, Mathf.RoundToInt(midLen / avg));
        int hash = Mathf.Abs(Mathf.RoundToInt(origin.x * 7.3f + origin.y * 13.1f));
        int start;
        if (capStart) start = 0;                                  // 왼끝 조각 다음은 A00
        else if (capEnd)                                          // 오른끝 조각 앞은 A14 또는 B14로 끝나게
        {
            int endIdx = hash % 2 == 0 ? half - 1 : N - 1;
            start = ((endIdx - m + 1) % N + N) % N;
        }
        else start = hash % N;                                    // 줄마다 다른 곳에서 시작 (반복 티 안 나게)

        float sum = 0f;
        for (int k = 0; k < m; k++) sum += floorSegs[(start + k) % N].bounds.size.x;
        float scale = midLen / sum;
        const float Overlap = 0.02f;
        float x = capLW;
        for (int k = 0; k < m; k++)
        {
            Sprite s = floorSegs[(start + k) % N];
            float natural = s.bounds.size.x, w = natural * scale;
            Piece(run.transform, s, x - Overlap, (w + Overlap * 2f) / natural, o, thin);
            x += w;
        }
        if (capStart) Piece(run.transform, floorCapL, 0f, 1f, o + 1, thin);
        if (capEnd) Piece(run.transform, floorCapR, L, 1f, o + 1, thin);

        if (thin) AddMasks(run.transform, d2, o, FAbove);
        return run.transform;
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
    static void AddMasks(Transform run, List<float> depths, int order, float aboveSurface)
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
            m.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (aboveSurface - d) * 0.5f, 0f);
            m.transform.localScale = new Vector3(x1 - x0, aboveSurface + d, 1f);
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
        Setup(MidPath, new Vector2(0f, PivotY), PPU);
        Setup(CapLPath, new Vector2(CapLPivotX, PivotY), PPU);
        Setup(CapRPath, new Vector2(CapRPivotX, PivotY), PPU);
        for (int k = 0; k < SegPerStrip; k++)
        {
            Setup(FloorDir + "중경땅_A_" + k.ToString("00") + ".png", new Vector2(0f, FPivotY), FPPU);
            Setup(FloorDir + "중경땅_B_" + k.ToString("00") + ".png", new Vector2(0f, FPivotY), FPPU);
        }
        Setup(FloorDir + "중경땅_왼끝.png", new Vector2(FCapLEdgePx / FCapLWpx, FPivotY), FPPU);
        Setup(FloorDir + "중경땅_오른끝.png", new Vector2(FCapREdgePx / FCapRWpx, FPivotY), FPPU);
    }

    static void Setup(string path, Vector2 pivot, float ppu)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        TextureImporterSettings st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        bool changed = ti.textureType != TextureImporterType.Sprite || st.spriteAlignment != (int)SpriteAlignment.Custom
                       || Vector2.Distance(st.spritePivot, pivot) > 0.0005f || Mathf.Abs(ti.spritePixelsPerUnit - ppu) > 0.01f
                       || ti.maxTextureSize < 4096;
        if (!changed) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = pivot;
        st.spritePixelsPerUnit = ppu;
        st.spriteMeshType = SpriteMeshType.FullRect;
        st.alphaIsTransparency = true;
        st.mipmapEnabled = false;
        st.wrapMode = TextureWrapMode.Clamp;
        ti.SetTextureSettings(st);
        ti.maxTextureSize = 4096;
        ti.SaveAndReimport();
    }
}
