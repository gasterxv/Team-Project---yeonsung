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
        // 벽·천장 띠 그림은 안쪽 끝에 까만 테두리가 있고 안쪽도 땅 색보다 밝아서, 두꺼운 땅 속에 선·경계가 보였음
        // → 안쪽으로 갈수록 땅 안쪽 색으로 서서히 바뀌는 사본(가공/페이드/)을 만들어 씀 (원본은 그대로)
        mid = MakeFaded(MidPath, new Vector2(0f, PivotY), true);
        capL = MakeFaded(CapLPath, new Vector2(CapLPivotX, PivotY), false);
        capR = MakeFaded(CapRPath, new Vector2(CapRPivotX, PivotY), false);
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

        MakeCornerSprites();

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
        // 맵 테두리(모든 땅 블록을 감싸는 네모) 바깥도 땅으로 봄: 카메라가 맵 바깥을 안 보여 주므로(CameraFollow 범위)
        // 바깥쪽 면에 띠를 깔 필요가 없고, 깔면 테두리 벽이 '얇은 벽'으로 잡혀 안쪽 반만 띠가 보이고 나머지가 어두워졌음
        int bx0 = Mathf.RoundToInt((b.min.x - minX) / Cell), bx1 = Mathf.RoundToInt((b.max.x - minX) / Cell);
        int by0 = Mathf.RoundToInt((b.min.y - minY) / Cell), by1 = Mathf.RoundToInt((b.max.y - minY) / Cell);
        for (int x = 0; x < gw; x++)
            for (int y = 0; y < gh; y++)
                if (x < bx0 || x >= bx1 || y < by0 || y >= by1) solid[x, y] = true;
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

    // (i,j) 줄 칸에서 안쪽(inward)으로 m칸 내려간 곳마다, 옆으로 가장 가까운 빈 공간까지 거리 h를 재서 h ≤ m 이 되는 깊이 (0.1 단위)
    static float MiterDepth(int i, int j, int inward, float full)
    {
        int maxM = Mathf.CeilToInt(full / Cell);
        for (int m = 1; m <= maxM; m++)
        {
            int r = j + inward * m;
            for (int h = 1; h <= m; h++)
                if (!S(i - h, r) || !S(i + h, r)) return m * Cell;
        }
        return full;
    }

    // 모서리 채움 네모(꼭짓점 격자선 vx,vy에서 sx,sy 방향 w×h)를 땅 칸 안으로 줄임: 먼저 열마다 땅이 이어지는 높이, 그다음 줄마다 폭
    static void ClipPatch(int vx, int vy, int sx, int sy, ref float w, ref float h)
    {
        int wc = Mathf.RoundToInt(w / Cell), hc = Mathf.RoundToInt(h / Cell);
        for (int k = 0; k < wc; k++)
        {
            int ci = sx > 0 ? vx + k : vx - 1 - k, m = 0;
            while (m < hc && S(ci, sy > 0 ? vy + m : vy - 1 - m)) m++;
            hc = m;
        }
        for (int m = 0; m < hc; m++)
        {
            int cj = sy > 0 ? vy + m : vy - 1 - m, k = 0;
            while (k < wc && S(sx > 0 ? vx + k : vx - 1 - k, cj)) k++;
            wc = k;
        }
        w = wc * Cell; h = hc * Cell;
    }

    // 양옆보다 깊은 구간이 좁으면(1.5칸 이하, 양옆이 모두 있음) 양옆 중 더 깊은 값으로 낮춤
    static void ClampNarrowBumps(List<float> d)
    {
        const int MaxCells = 15;
        int a = 0;
        while (a < d.Count)
        {
            int b = a;
            while (b < d.Count && Mathf.Approximately(d[b], d[a])) b++;
            if (a > 0 && b < d.Count && b - a <= MaxCells)
            {
                float side = Mathf.Max(d[a - 1], d[b]);
                if (d[a] > side + 0.001f)
                    for (int k = a; k < b; k++) d[k] = side;
            }
            a = b;
        }
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
            // 판 아래(위)로 얇은 벽이 이어진 곳(예: 노란 문 위 x 110.3~110.7): 그 몇 칸만 땅이 길게 이어져서 깊이가 '두꺼움'으로 잡힘
            // → 그림 아래쪽 어두운 부분이 판 반대쪽까지 내려와 어두운 네모가 됨. 좁게 튀는 깊이는 양옆 깊이로 맞춤
            ClampNarrowBumps(depths);
            // 얇은 기둥 끝(폭 1칸 미만, 양쪽이 다 빈 공간): 띠 한 장을 찌그러뜨려 깔면 무늬 없는 분홍 막대가 됨 → 양옆 벽 띠만 보이게
            if (x1 - x0 < 1f && outerL && outerR) continue;
            // 0.5칸보다 좁은 턱(얇은 조각이 위 블록보다 옆으로 조금 나온 곳, 예: 백혈구 위 x 29.7): 바닥 그림이 찌그러져 어두운 네모가 됨 → 안 깔기
            if (x1 - x0 < 0.5f && (outerL || outerR)) continue;
            // 얇은 판(바닥과 아랫면 사이가 얇음)의 아랫면: 벽 그림(밝은 분홍)을 쓰면 윗면 바닥 그림(진한 자홍)과 색이 달라 밝은 띠가 됨
            // → 아랫면도 바닥 그림을 뒤집어 깔아서 색을 맞춤 (기둥 끝을 얕게 하기 전에 판단 → 기둥 아랫면은 양옆 벽과 같은 그림)
            int thinCells = 0;
            foreach (float d in depths) if (d < Depth - 0.001f) thinCells++;
            bool thinSlab = !floor && floorSegs != null && thinCells * 2 > depths.Count;
            // 옆 벽면과 나누기: 바닥·천장 그림의 깊은(어두운) 부분은 벽 그림보다 앞에 그려져서, 옆에 벽면이 있으면 벽 그림의 밝은 부분을 덮음
            // (기둥 끝 어두운 띠, 바깥 모서리 어두운 네모, 천장 끝 위 가로 경계선) → 땅 속 각 지점은 더 가까운 면의 그림이 보이게:
            // 그 깊이에서 옆으로 빈 공간(벽면)까지가 더 가까워지면 거기서 멈춤 (모서리는 45도로 나뉨). 단 밟는 면 띠(0.6)는 항상 보이게
            float fullD = (floor || thinSlab) && floorSegs != null ? FDepth : Depth;
            //   얇은 판(두께 절반만 보이는 칸)은 옆면이 짧아 원래대로 바닥 그림이 덮는 게 자연스러움 → 두꺼운 칸만
            for (int k = 0; k < depths.Count; k++)
                if (depths[k] >= fullD - 0.001f)
                    depths[k] = Mathf.Min(depths[k], Mathf.Max(0.6f, MiterDepth(start + k, j, inward, fullD)));

            if (floor && floorSegs != null) MakeFloorRun(new Vector3(x0, y), 0f, x1 - x0, depths, outerL, outerR, ref nextFloor);
            else if (floor) MakeRun(new Vector3(x0, y), 0f, x1 - x0, depths, outerL, outerR, ref nextFloor, "바닥");
            else if (thinSlab) { depths.Reverse(); MakeFloorRun(new Vector3(x1, y), 180f, x1 - x0, depths, outerR, outerL, ref nextCeiling); }
            else { depths.Reverse(); MakeRun(new Vector3(x1, y), 180f, x1 - x0, depths, outerR, outerL, ref nextCeiling, "천장"); }
            // 안쪽 모서리(줄 끝 너머가 벽으로 막힘): 어느 띠도 덮지 않아 검은 네모가 보이던 곳
            // → 모서리 꼭짓점에서 멀어질수록 띠와 같은 밝기로 어두워지는 그림으로 채움 (띠를 늘리지 않으니 삐져나오지 않음)
            // 단, 얇은 벽(폭 1.5칸 이하) 너머로 같은 높이의 줄이 다시 이어지면 → 줄 그림을 벽 위로 그대로 이어 붙임 (오른쪽 끝에서만)
            int airDj = floor ? 1 : -1;
            bool bridgeL, bridgeR;
            float eL = InnerCornerLength(start - 1, j, -1, 0, 0, airDj, out bridgeL), eR = InnerCornerLength(end, j, 1, 0, 0, airDj, out bridgeR);
            float dL = CornerDepthScale(start - 1, j, 0, inward) * Depth, dR = CornerDepthScale(end, j, 0, inward) * Depth;
            bool styleFloor = floor || thinSlab;
            // 모서리 채움 네모가 땅 밖(빈 공간)으로 삐져나가지 않게 땅 안쪽만큼만 (ㄱ자로 꺾인 곳의 작은 검은 점)
            int vy = floor ? j + 1 : j, sy = floor ? -1 : 1;
            if (!(bridgeL && eL <= 1.5f)) { ClipPatch(start, vy, -1, sy, ref eL, ref dL); CornerPatch(new Vector3(x0, y), -1f, sy, eL, dL, styleFloor); }
            if (bridgeR && eR <= 1.5f) Bridge(new Vector3(x1, y), eR, floor, styleFloor, Mathf.Min(dR, Depth));
            else { ClipPatch(end, vy, 1, sy, ref eR, ref dR); CornerPatch(new Vector3(x1, y), 1f, sy, eR, dR, styleFloor); }
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
            // (안쪽 모서리는 ScanRow에서 채움)
            if (leftFacing) MakeRun(new Vector3(x, y0), 90f, y1 - y0, depths, false, false, ref nextWall, "벽");
            else { depths.Reverse(); MakeRun(new Vector3(x, y1), -90f, y1 - y0, depths, false, false, ref nextWall, "벽"); }
            count++;
        }
        return count;
    }

    // ---------------- 안쪽 모서리 채우기 ----------------

    // (ci,cj)부터 (si,sj) 방향으로, 칸도 땅이고 빈 공간 쪽 옆칸(airDi,airDj)도 땅인 칸이 몇 칸 이어지는지 (최대 띠 깊이만큼)
    // bridge = 칸은 계속 땅인데 빈 공간 쪽이 다시 열려서 멈춤 (= 얇은 벽 너머로 같은 높이의 줄이 이어짐)
    static float InnerCornerLength(int ci, int cj, int si, int sj, int airDi, int airDj, out bool bridge)
    {
        bridge = false;
        int max = Mathf.CeilToInt(Depth / Cell), n = 0;
        while (n < max)
        {
            int a = ci + si * n, b = cj + sj * n;
            if (a < 0 || b < 0 || a >= gw || b >= gh) break;   // 맵 바깥은 채우지 않음
            if (!S(a, b)) break;
            if (!S(a + airDi, b + airDj)) { bridge = n > 0; break; }
            n++;
        }
        return n * Cell;
    }

    // 얇은 벽 위(아래)로 줄 그림을 이어 붙임: 끝점 p에서 오른쪽으로 w. 벽 띠보다 뒤에 그려서 벽 모양은 그대로 보임
    static void Bridge(Vector3 p, float w, bool floor, bool styleFloor, float depth)
    {
        // 가운데 조각(옆면 그늘이 없는 A_07)을 씀
        Sprite s = styleFloor && floorSegs != null ? floorSegs[7] : mid;
        float natural = s.bounds.size.x, fullDepth = styleFloor && floorSegs != null ? FDepth : Depth;
        const float Overlap = 0.04f;
        GameObject g = new GameObject("얇은 벽 위 잇기");
        g.transform.SetParent(skinRoot, false);
        // 가로는 틈 폭 + 양쪽 0.05만 (넘치면 겉 테두리 선이 옆 줄 아래로 비침), 세로는 원래 깊이 그대로
        // 바닥: 그대로(기준점 = 왼쪽 끝). 천장: 180도(기준점 = 오른쪽 끝)
        float span = w + Overlap * 2f;
        g.transform.position = new Vector3(floor ? p.x - Overlap : p.x + w + Overlap, p.y, p.z);
        g.transform.rotation = Quaternion.Euler(0f, 0f, floor ? 0f : 180f);
        // 판이 그림보다 얇으면(반대쪽으로 삐져나감) 판 두께에 맞춰 세로만 줄임 (depth = 판 두께의 절반)
        g.transform.localScale = new Vector3(span / natural, Mathf.Min(1f, depth * 2f / fullDepth), 1f);
        SpriteRenderer r = g.AddComponent<SpriteRenderer>();
        r.sprite = s;
        r.sortingOrder = FillOrder + 1;
        corners++;
        pieces++;
    }

    // 모서리 칸에서 안쪽으로 보여 줄 깊이 비율 (얇으면 그림을 눌러서 반대쪽으로 삐져나가지 않게)
    static float CornerDepthScale(int ci, int cj, int di, int dj)
    {
        if (!S(ci, cj)) return 1f;
        return Mathf.Clamp01(DepthAt(ci, cj, di, dj) / Depth);
    }

    // 모서리 꼭짓점 p에서 (sx, sy) 방향으로 가로 w × 세로 h 네모를 채움. 띠보다 뒤(땅 안쪽 바로 위)에 그림
    static void CornerPatch(Vector3 p, float sx, float sy, float w, float h, bool floor)
    {
        Sprite sprite = floor ? cornerFloor : cornerWall;
        if (sprite == null || w < 0.15f || h < 0.15f) return;
        GameObject g = new GameObject("모서리 채움");
        g.transform.SetParent(skinRoot, false);
        g.transform.position = p;
        g.transform.localScale = new Vector3(sx * w / Depth, sy * h / Depth, 1f);   // 그림 한 장 = Depth × Depth
        SpriteRenderer r = g.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = FillOrder + 1;
        Material m = CornerMaterial();
        if (m != null) r.sharedMaterial = m;   // 겹친 모서리는 더 밝은 쪽이 보이게
        corners++;
        pieces++;
    }

    const string CornerShaderPath = "Assets/Map/NewMapArt/땅/CornerMax.shader", CornerMatPath = "Assets/Map/NewMapArt/땅/모서리_밝은쪽.mat";
    static Material CornerMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(CornerMatPath);
        if (m != null) return m;
        Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(CornerShaderPath);
        if (sh == null) return null;
        m = new Material(sh);
        AssetDatabase.CreateAsset(m, CornerMatPath);
        return m;
    }

    // ---- 모서리 그림 만들기: 띠 그림을 깊이(줄)마다 평균 낸 색 → 꼭짓점에서의 거리로 칠함 ----
    const int CornerPx = 128;
    const string CornerWallPath = Dir + "모서리_채움_벽.png", CornerFloorPath = FloorDir + "모서리_채움_바닥.png";
    static Sprite cornerWall, cornerFloor;

    // ---- 띠 그림 안쪽 끝을 땅 안쪽 색으로 (FadeStart~FadeEnd 깊이에서 서서히) ----
    const string FadeDir = Dir + "페이드/";
    const float FadeStart = Depth * 0.45f, FadeEnd = Depth * 0.95f;

    static Sprite MakeFaded(string srcPath, Vector2 pivot, bool fillAlpha)
    {
        string root = System.IO.Path.GetDirectoryName(Application.dataPath);
        string full = System.IO.Path.Combine(root, srcPath);
        if (!System.IO.File.Exists(full)) return null;
        string outPath = FadeDir + System.IO.Path.GetFileName(srcPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, FadeDir));
        Texture2D t = new Texture2D(2, 2);
        t.LoadImage(System.IO.File.ReadAllBytes(full));
        Color[] px = t.GetPixels();
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float d = ((h - 1 - y) - SurfaceFromTop) / PPU;   // 밟는 면에서 안쪽으로 깊이
            float k = Mathf.Clamp01((d - FadeStart) / (FadeEnd - FadeStart));
            k = k * k * (3f - 2f * k);
            if (k <= 0f) continue;
            for (int x = 0; x < w; x++)
            {
                Color c = px[y * w + x];
                Color o = Color.Lerp(c, FillColor, k);
                o.a = fillAlpha ? Mathf.Lerp(c.a, 1f, k) : c.a;   // 가운데 조각: 맨 끝 투명한 줄도 땅 색으로 메움
                px[y * w + x] = o;
            }
        }
        t.SetPixels(px);
        t.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, outPath), t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(outPath);
        Setup(outPath, pivot, PPU);
        return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
    }

    static void MakeCornerSprites()
    {
        // 모서리 그림의 가로 방향(꼭짓점에서 벽 아래·위로 들어가는 쪽)은 벽 띠와 닿고, 세로 방향은 그 줄(바닥/천장)의 띠와 닿음
        // → 가로 가장자리는 벽 띠 색, 세로 가장자리는 줄 띠 색이 되게 각도로 섞음 (한 가지 색만 쓰면 네모 경계가 보였음)
        string wallSrc = FadeDir + "땅_가운데_반복.png";
        List<Color> wallProf = Profile(wallSrc, SurfaceFromTop, PPU);
        cornerWall = MakeCorner(wallProf, wallProf, CornerWallPath);
        cornerFloor = floorSegs != null ? MakeCorner(wallProf, Profile(FloorDir + "중경땅_A_07.png", FTop, FPPU), CornerFloorPath) : cornerWall;
    }

    // 띠 그림의 깊이별 평균 색 (0.01칸 간격, 밟는 면 아래 25px부터 = 테두리 검은 선 피함). 무늬 때문에 고리가 보이지 않게 앞뒤로 부드럽게
    static List<Color> Profile(string srcPath, float surfacePx, float ppu)
    {
        string full = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), srcPath);
        if (!System.IO.File.Exists(full)) return null;
        Texture2D src = new Texture2D(2, 2);
        src.LoadImage(System.IO.File.ReadAllBytes(full));
        int top = src.height - 1 - Mathf.RoundToInt(surfacePx + 25f);
        List<Color> raw = new List<Color>();
        for (int y = top; y >= 0; y--)
        {
            Color sum = Color.clear; float wsum = 0f;
            for (int x = 0; x < src.width; x += 6)
            {
                Color c = src.GetPixel(x, y);
                if (c.a < 0.5f) continue;
                sum += c; wsum += 1f;
            }
            raw.Add(wsum > 0f ? sum / wsum : FillColor);
        }
        Object.DestroyImmediate(src);
        List<Color> prof = new List<Color>();
        const float Step = 0.01f;
        for (float d = 0f; d < Depth * 1.5f; d += Step)
        {
            int k = Mathf.RoundToInt(d * ppu), rad = Mathf.RoundToInt(0.06f * ppu);
            Color sum = Color.clear; int n = 0;
            for (int q = k - rad; q <= k + rad; q++) { sum += q >= 0 && q < raw.Count ? raw[q] : FillColor; n++; }
            prof.Add(sum / n);
        }
        return prof;
    }

    static Sprite MakeCorner(List<Color> profX, List<Color> profY, string outPath)
    {
        if (profX == null || profY == null) return null;
        Texture2D t = new Texture2D(CornerPx, CornerPx, TextureFormat.RGBA32, false);
        for (int py = 0; py < CornerPx; py++)
            for (int px = 0; px < CornerPx; px++)
            {
                float dx = (px + 0.5f) * Depth / CornerPx, dy = (py + 0.5f) * Depth / CornerPx;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                int k = Mathf.Min(Mathf.RoundToInt(r / 0.01f), profX.Count - 1);
                float wy = Mathf.Atan2(dy, dx) / (Mathf.PI * 0.5f);   // 0 = 가로 가장자리, 1 = 세로 가장자리
                Color c = Color.Lerp(profX[k], profY[Mathf.Min(k, profY.Count - 1)], wy);
                if (r > Depth) c = Color.Lerp(c, FillColor, Mathf.Clamp01((r - Depth) / (Depth * 0.4f)));
                c.a = 1f;
                t.SetPixel(px, py, c);
            }
        t.Apply();
        string outFull = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), outPath);
        System.IO.File.WriteAllBytes(outFull, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(outPath);
        Setup(outPath, Vector2.zero, CornerPx / Depth);   // 그림 한 장 = Depth × Depth, 기준점 = 꼭짓점(왼쪽 아래)
        return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
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
    static Transform MakeFloorRun(Vector3 origin, float rotZ, float L, List<float> depths, bool capStart, bool capEnd, ref int order)
    {
        int o = order;
        order += 2;
        GameObject run = new GameObject((rotZ == 0f ? "바닥 (" : "얇은 판 아랫면 (") + origin.x.ToString("0.#") + ", " + origin.y.ToString("0.#") + ")");
        run.transform.SetParent(skinRoot, false);
        run.transform.position = origin;
        run.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);

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
