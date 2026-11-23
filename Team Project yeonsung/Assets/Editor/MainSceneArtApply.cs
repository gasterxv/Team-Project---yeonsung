using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 지금 열려 있는 #MainScenes#에 그림을 넣는 도구 (새 씬은 만들지 않음, 저장도 하지 않음)
// 메뉴: Tools > 메인씬 그림 넣기
// - 적혈구 발판(지형 30, 31, 38~59): 가로를 2.35로 자르고 적혈구 그림을 넣음. 지형 31은 3개로 나눔
// - 나머지 빨간 블록(지형 01~29, 32~37): 붙어 있는 블록끼리 땅 그림을 한 줄로 이어 깔고 블록 모양 마스크로 파냄
//   그림 종류(AI 타일 / 땅1 / 땅2)는 구덩이처럼 끊기는 곳에서만 바뀌게 돌아가며 배치 (벽과 천장은 뒤집어서)
// - 혈류: 바닥 없는 낭떠러지 3곳에 혈류(대미지+튕김+밀림)와 아래 바닥. 땅 그림보다 뒤에 그려짐
// - 배경: 원경1, 원경2를 패럴랙스로 맨 뒤에 추가하고 예전 흑백 배경(Canvas > Image, Image2)은 끔
// 원래 블록과 이전 실행 결과는 지우지 않고 끄기만 합니다. 실행 후 Ctrl+Z로 한 번에 되돌릴 수 있어요.
public static class MainSceneArtApply
{
    const string SceneName = "#MainScenes#";
    const string Root = "Map_순환계/설계도 지형";

    const string RedCell = "Assets/image/적혈구.png";
    const string Ground1 = "Assets/image/중경_땅1.png";
    const string Ground2 = "Assets/image/중경_땅2.png";
    const string GroundAI = "Assets/image/진홍빛 유기질 지형 타일_반복.png";
    const string Far1 = "Assets/image/원경1.PNG";
    const string Far2 = "Assets/image/원경2.PNG";
    const string SquarePath = "Assets/Map/Square.png";

    const float CellWidth = 2.35f;      // 적혈구 비율(1137:451)에 맞는 발판 가로

    // 땅 그림 정보 (PNG를 열어 잰 값). topLine = 위에서 검은 윗선까지, body = 윗선부터 그림 아랫면까지 (유닛)
    class GroundArt
    {
        public string name, path;
        public float width, height, topLine, body;
        public bool tiled;          // true: 양 끝 둥근 부분을 테두리로 두고 가운데만 반복 (땅1, 땅2)
        public Color backing;       // 그림 아래쪽을 채우는 바탕색 (그림 아랫부분 색)
    }
    static readonly GroundArt ArtG1 = new GroundArt { name = "땅1", path = Ground1, width = 12870 / 200f, height = 1096 / 200f, topLine = 131 / 200f, body = (1062 - 131) / 200f, tiled = true, backing = new Color(0.231f, 0.007f, 0.065f) };
    static readonly GroundArt ArtG2 = new GroundArt { name = "땅2", path = Ground2, width = 12870 / 200f, height = 1090 / 200f, topLine = 88 / 200f, body = (1056 - 88) / 200f, tiled = true, backing = new Color(0.231f, 0.007f, 0.065f) };
    // AI 타일: PPU 100, 2108x724 (양 끝 8px 자르고 이음새 48px 섞은 반복용)
    static readonly GroundArt ArtAI = new GroundArt { name = "AI", path = GroundAI, width = 2108 / 100f, height = 724 / 100f, topLine = 183 / 100f, body = (601 - 183) / 100f, tiled = false, backing = new Color(0.290f, 0.028f, 0.102f) };
    static readonly GroundArt[] ArtCycle = { ArtAI, ArtG1, ArtG2 };

    static readonly int[] CellIds = { 30, 31, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 };
    // 적혈구 발판을 뺀 나머지 빨간 블록 전부 (바닥, 계단, 기둥, 턱, 벽과 천장 01~14)
    static readonly int[] GroundIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 32, 33, 34, 35, 36, 37 };

    // 바닥이 없는 낭떠러지 = 혈류 구간 (기획서: 혈관 하단 혈류, 닿으면 피해, 위에 적혈구 발판)
    static readonly Vector2[] BloodPits = { new Vector2(70.03f, 83.90f), new Vector2(106.0f, 125.73f), new Vector2(143.59f, 202.59f) };
    const float BloodTop = -2.5f;       // 땅 윗면(0)보다 2.5 아래
    const float BloodDepth = 0.8f;
    const float BloodFloorBottom = -5f;
    const int BloodOrder = -800;        // 땅 그림(-10 이하)보다 뒤, 배경(-1000)보다 앞 -> 기둥이 혈류 위로 솟아 보임

    const string StripRootName = "땅그림_v2";
    const string OldStripRootName = "땅그림_이어붙임";
    const float EndMargin = 3f;         // 그림 양 끝을 블록 밖으로 빼는 거리 (둥근 끝이 안 보이게)
    const float MaskTopMargin = 0.6f;   // 윗선 위로 튀어나온 혹이 잘리지 않게 마스크를 위로 늘리는 양
    const float BackingInset = 0.5f;    // 바탕을 윗선에서 이만큼 안쪽부터 깔기 (윗선 위로 색이 튀지 않게)
    static int runCount;                // 정렬 순서 구간 계산용

    [MenuItem("Tools/메인씬 그림 넣기")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            EditorUtility.DisplayDialog("메인씬 그림 넣기", SceneName + " 씬을 열고 실행해 주세요.\n지금 열린 씬: " + scene.name, "확인");
            return;
        }

        Transform root = FindPath(scene, Root);
        if (root == null)
        {
            Debug.LogError("'" + Root + "'를 찾지 못했어요.");
            return;
        }

        if (root.Find(StripRootName) != null)
        {
            EditorUtility.DisplayDialog("메인씬 그림 넣기", "이미 적용돼 있어요 (" + StripRootName + ").\n다시 하려면 Ctrl+Z로 되돌리거나 저장하지 않고 씬을 다시 연 뒤 실행해 주세요.", "확인");
            return;
        }

        runCount = 0;
        SetupGroundTiling();
        Undo.SetCurrentGroupName("메인씬 그림 넣기");
        int group = Undo.GetCurrentGroup();
        var log = new List<string>();

        Sprite cell = AssetDatabase.LoadAssetAtPath<Sprite>(RedCell);
        foreach (int id in CellIds)
        {
            Transform t = Find(root, TerrainName(id));
            if (t == null) { log.Add("못 찾음: " + TerrainName(id)); continue; }

            if (id == 31)
            {
                // 8.0 길이 발판 -> 적혈구 3개 (가운데는 원래 오브젝트, 양옆은 새로 추가)
                SplitInto(t, 3, cell, log);
            }
            else
            {
                MakeCell(t, cell, log);
            }
        }

        RetireOldGround(root, log);

        var blocks = new List<Transform>();
        foreach (int id in GroundIds)
        {
            Transform t = Find(root, TerrainName(id));
            if (t == null) { log.Add("못 찾음: " + TerrainName(id)); continue; }
            blocks.Add(t);
        }
        BuildGroundRuns(root, blocks, log);

        AddBloodFlow(root, log);
        AddBackground(scene, log);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("메인씬 그림 넣기 완료 (저장은 Ctrl+S)\n" + string.Join("\n", log));
    }

    // 씬의 이름은 "지형 01" ~ "지형 09"처럼 두 자리
    static string TerrainName(int id) => "지형 " + id.ToString("00");

    // ---------------- 적혈구 발판 ----------------

    // 가로를 2.35로 자르고(가운데 기준) 적혈구 그림을 자식으로 붙입니다.
    static void MakeCell(Transform t, Sprite cell, List<string> log)
    {
        if (t.Find("그림") != null) { log.Add("이미 있음: " + t.name); return; }

        float oldW = SetWidth(t, CellWidth);
        AddArt(t, cell, CellWidth / cell.bounds.size.x, Vector3.zero, SpriteMaskInteraction.None);
        HideGray(t);
        log.Add(t.name + ": 가로 " + oldW.ToString("0.##") + " -> " + CellWidth + ", 적혈구 그림");
    }

    static void SplitInto(Transform t, int count, Sprite cell, List<string> log)
    {
        MakeCell(t, cell, log);
        for (int i = 1; i < count; i++)
        {
            string name = t.name + "-" + (i + 1);
            if (t.parent.Find(name) != null) { log.Add("이미 있음: " + name); continue; }

            GameObject copy = Object.Instantiate(t.gameObject, t.parent);
            Undo.RegisterCreatedObjectUndo(copy, "적혈구 나누기");
            copy.name = name;
            // 원래 발판 양옆으로 붙여 놓기: -2.35, +2.35
            float dx = (i % 2 == 1 ? -1f : 1f) * CellWidth * ((i + 1) / 2);
            copy.transform.position = t.position + new Vector3(dx, 0f, 0f);
            copy.transform.SetSiblingIndex(t.GetSiblingIndex() + i);
            log.Add(name + ": 새로 추가 (x " + copy.transform.position.x.ToString("0.##") + ")");
        }
    }

    // ---------------- 땅 (빨간 블록) ----------------

    class Box
    {
        public Transform t;
        public SpriteRenderer gray;
        public float left, right, top, bottom;
        public bool ceiling;
        public float Line => ceiling ? bottom : top;
    }

    // 지난번 결과(땅그림_이어붙임, 블록마다 붙인 땅마스크/땅바탕)는 지우지 않고 끄고 이름 뒤에 (이전)을 붙임
    static void RetireOldGround(Transform root, List<string> log)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            bool old = t.name == OldStripRootName
                || ((t.name == "땅마스크" || t.name == "땅바탕") && t.parent != null && t.parent.name.StartsWith("지형 "));
            if (!old || !t.gameObject.activeSelf) continue;

            Undo.RecordObject(t.gameObject, "이전 땅 그림 끄기");
            t.gameObject.SetActive(false);
            t.gameObject.name += " (이전)";
            count++;
        }
        if (count > 0) log.Add("이전 땅 그림/마스크/바탕 " + count + "개: 끔 (이름 뒤에 (이전))");
    }

    // 땅1/땅2: 둥근 양 끝 200px만 테두리로 두고 가운데만 반복(Tiled)되게 Import 설정 (그림 파일은 그대로)
    static void SetupGroundTiling()
    {
        Vector4 border = new Vector4(200f, 0f, 200f, 0f);   // 왼쪽, 아래, 오른쪽, 위
        foreach (string path in new[] { Ground1, Ground2 })
        {
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporterSettings st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            if (st.spriteMeshType == SpriteMeshType.FullRect && ti.spriteBorder == border) continue;

            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
            ti.spriteBorder = border;
            ti.SaveAndReimport();
        }
    }

    // 같은 높이에서 서로 붙어 있는 블록끼리 한 "줄(run)"로 묶어 그림 하나를 이어 깔고, 블록 모양 마스크로 파냄
    // 그림 종류는 줄마다 AI -> 땅1 -> 땅2 순서로 돌아가서, 바닥이 끊기는 곳(구덩이)에서만 바뀜
    // 바닥: 검은 윗선이 블록 윗면에. 벽과 천장: 위아래를 뒤집어 검은 선이 블록 아랫면에.
    // 그림보다 깊은 부분은 그림 아랫부분 색의 바탕으로 채움 (그림을 세로로 쌓지 않아서 줄무늬가 안 생김)
    static void BuildGroundRuns(Transform root, List<Transform> blocks, List<string> log)
    {
        var boxes = new List<Box>();
        foreach (Transform t in blocks)
        {
            SpriteRenderer gray = t.GetComponent<SpriteRenderer>();
            Vector2 size = gray.size;
            boxes.Add(new Box
            {
                t = t,
                gray = gray,
                left = t.position.x - size.x * 0.5f,
                right = t.position.x + size.x * 0.5f,
                top = t.position.y + size.y * 0.5f,
                bottom = t.position.y - size.y * 0.5f,
                ceiling = t.parent != null && t.parent.name == "벽과 천장",
            });
        }

        // 같은 높이 + 서로 맞닿은 블록끼리 줄 만들기
        var runs = new List<List<Box>>();
        foreach (var grp in boxes.GroupBy(b => (b.ceiling, Mathf.RoundToInt(b.Line * 20f))))
        {
            List<Box> current = null;
            foreach (Box b in grp.OrderBy(b => b.left))
            {
                if (current != null && b.left <= current.Max(c => c.right) + 0.05f) current.Add(b);
                else { current = new List<Box> { b }; runs.Add(current); }
            }
        }

        GameObject stripRoot = new GameObject(StripRootName);
        Undo.RegisterCreatedObjectUndo(stripRoot, "땅 그림");
        stripRoot.transform.SetParent(root, false);

        // 바닥과 천장 각각 왼쪽부터 그림 종류를 돌아가며 배정
        foreach (bool ceiling in new[] { false, true })
        {
            int k = 0;
            foreach (var run in runs.Where(r => r[0].ceiling == ceiling).OrderBy(r => r.Min(b => b.left)))
            {
                GroundArt art = ArtCycle[k % ArtCycle.Length];
                k++;
                BuildRun(stripRoot.transform, run, art, log);
            }
        }
    }

    static void BuildRun(Transform parent, List<Box> run, GroundArt art, List<string> log)
    {
        bool ceiling = run[0].ceiling;
        float line = run[0].Line;
        float left = run.Min(b => b.left) - EndMargin;
        float right = run.Max(b => b.right) + EndMargin;
        SpriteRenderer sortingFrom = run[0].gray;

        int baseOrder = -10 - 3 * runCount;   // 줄마다 정렬 구간을 따로 써서 다른 줄 마스크에 비치지 않게
        runCount++;

        GameObject holder = new GameObject((ceiling ? "천장 " : "바닥 ") + art.name + " y" + line.ToString("0.##") + " x" + (left + EndMargin).ToString("0"));
        Undo.RegisterCreatedObjectUndo(holder, "땅 그림");
        holder.transform.SetParent(parent, false);

        float cy = ceiling ? line - art.topLine + art.height * 0.5f : line + art.topLine - art.height * 0.5f;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art.path);

        if (art.tiled)
        {
            // 땅1/땅2: 한 장을 가로로 늘려 가운데 무늬만 반복 (둥근 끝은 블록 밖)
            SpriteRenderer sr = NewArt(holder.transform, sprite, new Vector3((left + right) * 0.5f, cy, 0f), ceiling, sortingFrom, baseOrder);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(right - left, art.height);
        }
        else
        {
            // AI 타일: 한 장씩 이어 붙이고 한 장 건너 좌우 반전 -> 같은 무늬가 그대로 연속되지 않음
            int count = Mathf.CeilToInt((right - left) / art.width);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = NewArt(holder.transform, sprite, new Vector3(left + art.width * (i + 0.5f), cy, 0f), ceiling, sortingFrom, baseOrder);
                sr.flipX = i % 2 == 1;
            }
        }

        foreach (Box b in run)
        {
            AddBlockMask(b, baseOrder);
            AddBacking(b, art.backing, baseOrder - 1);
            HideGray(b.t);
        }

        log.Add(holder.name + ": " + string.Join(", ", run.Select(b => b.t.name)));
    }

    static SpriteRenderer NewArt(Transform parent, Sprite sprite, Vector3 pos, bool ceiling, SpriteRenderer sortingFrom, int order)
    {
        GameObject go = new GameObject(sprite != null ? sprite.name : "땅");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipY = ceiling;
        sr.sortingLayerID = sortingFrom.sortingLayerID;
        sr.sortingOrder = order;
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        return sr;
    }

    // 땅 그림 뒤에 까는 바탕 (그림 아랫부분과 같은 색). 그림보다 깊은 블록도 빈틈없이 채움
    static void AddBacking(Box b, Color color, int order)
    {
        float top = b.ceiling ? b.top : b.top - BackingInset;
        float bottom = b.ceiling ? b.bottom + BackingInset : b.bottom;
        if (top - bottom <= 0.01f) return;

        GameObject go = new GameObject("땅바탕");
        Undo.RegisterCreatedObjectUndo(go, "땅 바탕");
        go.transform.SetParent(b.t, false);
        go.transform.position = new Vector3((b.left + b.right) * 0.5f, (top + bottom) * 0.5f, b.t.position.z);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(b.right - b.left, top - bottom);
        sr.color = color;
        sr.sortingLayerID = b.gray.sortingLayerID;
        sr.sortingOrder = order;
    }

    // 블록 모양 마스크. 윗선 위 혹이 잘리지 않게 0.6 여유, 이 줄의 정렬 순서만 보이게 함
    static void AddBlockMask(Box b, int baseOrder)
    {
        float mTop = b.ceiling ? b.top : b.top + MaskTopMargin;
        float mBottom = b.ceiling ? b.bottom - MaskTopMargin : b.bottom;

        GameObject maskGo = new GameObject("땅마스크");
        Undo.RegisterCreatedObjectUndo(maskGo, "땅 마스크");
        maskGo.transform.SetParent(b.t, false);
        maskGo.transform.position = new Vector3((b.left + b.right) * 0.5f, (mBottom + mTop) * 0.5f, b.t.position.z);
        maskGo.transform.localScale = new Vector3(b.right - b.left, mTop - mBottom, 1f);

        SpriteMask mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = b.gray.sortingLayerID;
        mask.backSortingLayerID = b.gray.sortingLayerID;
        mask.frontSortingOrder = baseOrder;
        mask.backSortingOrder = baseOrder - 2;
    }

    // ---------------- 혈류 ----------------

    // 낭떠러지 바닥에 혈류(닿으면 대미지 + 튕김 + 밀림)와 그 아래 단단한 바닥. 이미 있으면 그리는 순서만 뒤로
    static void AddBloodFlow(Transform root, List<string> log)
    {
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        for (int i = 0; i < BloodPits.Length; i++)
        {
            string name = "혈류 " + (i + 1);
            Transform existing = root.Find(name);
            if (existing != null)
            {
                SetOrder(existing, BloodOrder);
                Transform floor = existing.Find(name + " 바닥");
                if (floor != null) SetOrder(floor, BloodOrder - 1);
                log.Add(name + ": 이미 있음 -> 땅 그림 뒤로 (순서 " + BloodOrder + ")");
                continue;
            }

            float x0 = BloodPits[i].x, x1 = BloodPits[i].y;
            GameObject blood = Block(root, name, square, x0, x1, BloodTop - BloodDepth, BloodTop, new Color(0.9f, 0.05f, 0.1f, 0.8f), BloodOrder);
            blood.GetComponent<BoxCollider2D>().isTrigger = true;
            BloodFlow flow = blood.AddComponent<BloodFlow>();
            flow.bounceVelocity = 11f;   // 적혈구 발판 높이(약 1.6)까지 튕겨 오르게

            Block(blood.transform, name + " 바닥", square, x0, x1, BloodFloorBottom, BloodTop - BloodDepth, new Color(0.35f, 0.02f, 0.05f, 1f), BloodOrder - 1);
            log.Add(name + ": x " + x0 + " ~ " + x1 + " (혈류 자리 + 아래 바닥)");
        }
    }

    static void SetOrder(Transform t, int order)
    {
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Undo.RecordObject(sr, "혈류 순서");
        sr.sortingOrder = order;
    }

    static GameObject Block(Transform parent, string name, Sprite sprite, float x0, float x1, float y0, float y1, Color color, int order)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        Vector2 size = new Vector2(x1 - x0, y1 - y0);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = order;
        go.AddComponent<BoxCollider2D>().size = size;
        return go;
    }

    // ---------------- 배경 ----------------

    static void AddBackground(Scene scene, List<string> log)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (r.name == "원경_배경") { log.Add("이미 있음: 원경_배경"); return; }
        }

        GameObject bg = new GameObject("원경_배경");
        Undo.RegisterCreatedObjectUndo(bg, "원경 배경");
        bg.transform.SetSiblingIndex(0);
        AddFar(bg.transform, "원경1", Far1, 0f, -1001);
        AddFar(bg.transform, "원경2", Far2, 19.2f, -1000);
        log.Add("원경_배경: 원경1, 원경2 추가 (패럴랙스 0.6, Sorting Order -1001/-1000)");

        // 예전 흑백 배경은 끄기만 합니다.
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (r.name != "Canvas") continue;
            foreach (string n in new[] { "Image", "Image2" })
            {
                Transform img = r.transform.Find(n);
                if (img != null && img.GetComponent<Image>() != null && img.gameObject.activeSelf)
                {
                    Undo.RecordObject(img.gameObject, "예전 배경 끄기");
                    img.gameObject.SetActive(false);
                    log.Add("Canvas/" + n + ": 끔 (예전 흑백 배경)");
                }
            }
        }
    }

    static void AddFar(Transform parent, string name, string path, float x, int order)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "원경");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 0f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        sr.sortingOrder = order;
        ParallaxTile p = go.AddComponent<ParallaxTile>();
        p.parallax = 0.6f;
        p.period = 19.2f * 2f;
    }

    // ---------------- 공통 ----------------

    // 회색 사각형 크기와 콜라이더 가로를 바꿉니다 (가운데 기준). 이전 가로를 돌려줌
    static float SetWidth(Transform t, float w)
    {
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        BoxCollider2D col = t.GetComponent<BoxCollider2D>();
        float old = sr.size.x;
        Undo.RecordObject(sr, "발판 자르기");
        sr.size = new Vector2(w, sr.size.y);
        if (col != null)
        {
            Undo.RecordObject(col, "발판 자르기");
            col.size = new Vector2(w, col.size.y);
            col.offset = new Vector2(0f, col.offset.y);
        }
        return old;
    }

    static void HideGray(Transform t)
    {
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null || !sr.enabled) return;
        Undo.RecordObject(sr, "회색 사각형 숨기기");
        sr.enabled = false;
    }

    static void AddArt(Transform parent, Sprite s, float scale, Vector3 localPos, SpriteMaskInteraction mask)
    {
        GameObject go = new GameObject("그림");
        Undo.RegisterCreatedObjectUndo(go, "그림");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        CopySorting(parent.GetComponent<SpriteRenderer>(), sr);
        sr.maskInteraction = mask;
    }

    static void CopySorting(SpriteRenderer from, SpriteRenderer to)
    {
        if (from == null) return;
        to.sortingLayerID = from.sortingLayerID;
        to.sortingOrder = from.sortingOrder;
    }

    static Transform FindPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (r.name != parts[0]) continue;
            Transform t = r.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t;
        }
        return null;
    }

    // 하위 전체에서 이름으로 찾기 (설계도 지형 바로 아래 또는 '벽과 천장' 아래)
    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
