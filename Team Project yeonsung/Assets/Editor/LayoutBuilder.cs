using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Tools > Build Layout
// 1) image/ 폴더 PNG를 Sprite, PPU 200으로 설정
// 2) Sorting Layer 추가 (Background, Decoration, Ground, Objects, Enemies, Player, FX)
// 3) Tutorial_Circulation.unity, Title_Layout.unity 씬을 새로 만들어 저장 (다시 누르면 새로 덮어씀)
// 이미지가 아직 없는 것은 회색 반투명 사각형 + 이름표로 자리만 만듭니다.
public static class LayoutBuilder
{
    const string TutorialPath = "Assets/Scenes/Tutorial_Circulation.unity";
    const string TitlePath = "Assets/Scenes/Title_Layout.unity";   // 기존 Title.unity(팀원 파일)는 건드리지 않음
    const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    const string SquarePath = "Assets/Map/Square.png";              // 자리 표시용 흰 사각형 (1유닛)
    const string PillPath = "Assets/Map/Pill.png";                  // 타이틀 둥근 버튼용

    const string ImgDir = "Assets/image/";
    const string TitleImg = ImgDir + "Title_illustration.PNG";
    const string Far1 = ImgDir + "원경1.PNG";
    const string Far2 = ImgDir + "원경2.PNG";
    const string Ground1 = ImgDir + "중경_땅1.png";
    const string Ground2 = ImgDir + "중경_땅2.png";
    const string RedCell = ImgDir + "적혈구.png";
    const string BreakFloor = ImgDir + "부서지는바닥 1.png";
    const string Core = ImgDir + "감염핵 1.png";
    const string WhiteCell = ImgDir + "백혈구.png";

    // 이 파일들만 Import 설정을 바꿉니다. (coaak의 감염핵.png, 부서지는바닥.png, Resources/맵 은 제외)
    static readonly string[] OwnImages = { TitleImg, Far1, Far2, Ground1, Ground2, RedCell, BreakFloor, Core, WhiteCell };

    static readonly string[] SortingLayers = { "Background", "Decoration", "Ground", "Objects", "Enemies", "Player", "FX" };

    const float PPU = 200f;
    const float GroundTopY = -1.9f;     // 땅 윗면(검은 선)이 올 높이

    // ---- 땅 윗면 데이터 (PNG를 직접 열어서 측정) ----
    // 각 세로줄(column)마다 위에서부터 처음 나오는 불투명 픽셀 = 윗면 검은 외곽선.
    // 작은 거품이 튀어나온 곳은 무시하려고 ±300px 범위의 중앙값으로 다듬고 400px 간격으로 뽑았습니다.
    // RefRow = 윗선 행 번호의 중앙값. 이 행이 y = -1.9에 오도록 이미지 높이를 맞춥니다.
    //   땅1: 12870x1096, 중앙값 131행 (위에서 12%) / 땅2: 12870x1090, 중앙값 88행 (위에서 8%)
    static readonly GroundInfo G1 = new GroundInfo
    {
        path = Ground1, width = 12870, height = 1096, refRow = 131, centerX = 32.2f,
        cols = new[] { 11, 71, 471, 871, 1271, 1671, 2071, 2471, 2871, 3271, 3671, 4071, 4471, 4871, 5271, 5671, 6071, 6471, 6871, 7271, 7671, 8071, 8471, 8871, 9271, 9671, 10071, 10471, 10871, 11271, 11671, 12071, 12471, 12771 },
        rows = new[] { 126, 126, 104, 107, 121, 132, 115, 96, 90, 124, 149, 137, 139, 131, 116, 150, 167, 115, 94, 139, 145, 172, 172, 133, 158, 175, 137, 133, 101, 130, 119, 106, 140, 140 },
    };

    // 땅2는 땅1 오른쪽 끝에 맞붙입니다: 32.2 + 12870/200 = 96.55
    static readonly GroundInfo G2 = new GroundInfo
    {
        path = Ground2, width = 12870, height = 1090, refRow = 88, centerX = 96.55f,
        cols = new[] { 13, 73, 473, 873, 1273, 1673, 2073, 2473, 2873, 3273, 3673, 4073, 4473, 4873, 5273, 5673, 6073, 6473, 6873, 7273, 7673, 8073, 8473, 8873, 9273, 9673, 10073, 10473, 10873, 11273, 11673, 12073, 12473, 12698 },
        rows = new[] { 80, 80, 78, 99, 100, 118, 106, 106, 103, 95, 104, 74, 87, 64, 81, 72, 88, 119, 118, 77, 71, 97, 89, 104, 98, 49, 46, 62, 84, 86, 68, 59, 71, 71 },
    };

    const float BloodStart = 64f;   // 혈류 구간 (땅을 비움)
    const float BloodEnd = 88f;

    const float MapLeft = 0f;       // 양 끝 벽 안쪽 면
    const float MapRight = 183f;
    const float CeilingY = 4.5f;    // 천장 아랫면 (땅 윗면 -1.9에서 6.4 위)
    const float CeilingTop = 13f;

    // 체크포인트 뒤에 이어 붙이는 보스방(30) + 경사·거대한 문(25). 땅 그림이 없어서 자리 표시로 만듭니다.
    const float BossStart = 128f;
    const float BossEnd = 158f;
    const float BossCeilingY = 8.1f;    // 보스방은 천장을 높게 (땅 위 10)
    const float DoorCeilingY = 6.1f;    // 경사 구간 천장 (땅 위 8)

    static Sprite square;
    static Font font;

    class GroundInfo
    {
        public string path;
        public int width, height, refRow;
        public float centerX;
        public int[] cols, rows;

        public float Left => centerX - width / PPU * 0.5f;
        public float Right => centerX + width / PPU * 0.5f;
        public float ImageTop => GroundTopY + refRow / PPU;
        public float CenterY => ImageTop - height / PPU * 0.5f;

        public Vector2 Point(int i) => new Vector2(Left + cols[i] / PPU, ImageTop - rows[i] / PPU);

        public float SurfaceY(float x)
        {
            float col = (x - Left) * PPU;
            if (col <= cols[0]) return Point(0).y;
            for (int i = 1; i < cols.Length; i++)
            {
                if (col <= cols[i])
                {
                    float t = (col - cols[i - 1]) / (cols[i] - cols[i - 1]);
                    return Mathf.Lerp(Point(i - 1).y, Point(i).y, t);
                }
            }
            return Point(cols.Length - 1).y;
        }
    }

    static float Surface(float x) => x < G1.Right ? G1.SurfaceY(x) : G2.SurfaceY(x);

    [MenuItem("Tools/Build Layout")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SetupImports();
        EnsureSortingLayersAndTags();
        square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildTitle();
        BuildTutorial();

        EditorSceneManager.OpenScene(TutorialPath);
        Debug.Log("Build Layout 완료! " + TutorialPath + ", " + TitlePath);
    }

    // 튜토리얼 씬은 건드리지 않고 타이틀만 다시 만들기
    [MenuItem("Tools/Build Title Only")]
    public static void BuildTitleOnly()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SetupImports();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildTitle();
        Debug.Log("타이틀 씬을 다시 만들었어요: " + TitlePath);
    }

    // ================= 튜토리얼 (순환계) =================

    static void BuildTutorial()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 원경: 카메라 따라 패럴랙스 0.6, 두 장이 번갈아 반복 ---
        Transform bg = new GameObject("원경").transform;
        Far(bg, "원경1", Far1, 0f, 0);
        Far(bg, "원경2", Far2, 19.2f, 1);

        // --- 중경 땅 + 윗면을 따라가는 Edge Collider ---
        Transform ground = new GameObject("땅").transform;
        Place(ground, "중경_땅1", G1.path, new Vector2(G1.centerX, G1.CenterY), 1f, "Ground", 0);
        SpriteRenderer g2 = Place(ground, "중경_땅2", G2.path, new Vector2(G2.centerX, G2.CenterY), 1f, "Ground", 0);
        Edge(ground, "땅1_윗면콜라이더", G1, G1.Left, G1.Right);
        Edge(ground, "땅2_윗면콜라이더", G2, BloodEnd, G2.Right);

        // 땅2 그림에서 혈류 구간(64~88)만 가립니다. (원본 PNG는 그대로)
        g2.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        GameObject maskGo = new GameObject("땅2_혈류구간_가림마스크");
        maskGo.transform.SetParent(ground, false);
        maskGo.transform.position = new Vector3((BloodStart + BloodEnd) * 0.5f, -4f, 0f);
        maskGo.transform.localScale = new Vector3(BloodEnd - BloodStart, 12f, 1f);
        maskGo.AddComponent<SpriteMask>().sprite = square;

        // --- 오브젝트 ---
        Transform objects = new GameObject("오브젝트").transform;

        // 2 점프: 낮은 턱 적혈구
        Platform(objects, "적혈구_낮은턱", RedCell, 18f, Surface(18f), 0.45f);

        // 4 낙하공격: 떠 있는 부서지는바닥 (위에서 C 특수공격하면 파괴)
        float floorY = Surface(43f) + 3f;
        SpriteRenderer bf = Place(objects, "부서지는바닥", BreakFloor, new Vector2(43f, floorY), 0.3f, "Objects", 0);
        bf.gameObject.tag = "Ground";
        bf.gameObject.AddComponent<BoxCollider2D>();
        bf.gameObject.AddComponent<BreakableFloor>();
        bf.gameObject.AddComponent<BreakableFloorSlamReceiver>();

        // 혈류 점프맵: 혈류(자리) + 그 아래 단단한 바닥 + 적혈구 발판 5개 (2개는 좌우 왕복)
        GameObject blood = Placeholder(objects, "혈류", BloodStart, BloodEnd, -3.6f, -2.8f, "Ground", true);
        blood.GetComponent<BoxCollider2D>().isTrigger = true;
        blood.AddComponent<BloodFlow>();
        GameObject bloodFloor = Placeholder(objects, "혈류_바닥", BloodStart, BloodEnd, -5f, -3.6f, "Ground", true);
        bloodFloor.tag = "Ground";

        float[] cellX = { 67f, 71.5f, 76f, 80.5f, 85f };
        float[] cellTop = { -1.9f, -0.9f, -1.9f, -0.9f, -1.9f };
        for (int i = 0; i < cellX.Length; i++)
        {
            float h = 451f / PPU * 0.45f;
            GameObject cell = Platform(objects, "적혈구_발판" + (i + 1), RedCell, cellX[i], cellTop[i] - h, 0.45f);
            if (i == 1 || i == 3)
            {
                cell.name += "_좌우왕복(B)";
                Rigidbody2D rb = cell.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                MovingPlatform mp = cell.AddComponent<MovingPlatform>();
                mp.moveOffset = new Vector2(1.2f, 0f);
                mp.cycleTime = 3f;
            }
        }

        // 감염핵 구역: 백혈구(장식), 천장 턱, 감염핵
        float wbcH = 660f / PPU * 0.24f;
        Place(objects, "백혈구_장식", WhiteCell, new Vector2(92f, Surface(92f) + wbcH * 0.5f), 0.24f, "Decoration", 0);

        GameObject ledge = Placeholder(objects, "천장턱", 106f, 114f, 2.5f, CeilingY, "Ground", true);   // 천장에서 내려온 턱
        ledge.tag = "Ground";

        // 문 (위/아래 두 파트) + 안내 문구
        GameObject door = BuildDoor(objects, 119f);

        // 감염핵: 윗부분이 천장 턱 바로 아래에 붙도록
        float coreH = 2235f / PPU * 0.22f;
        SpriteRenderer core = Place(objects, "감염핵", Core, new Vector2(110f, 2.5f - coreH * 0.5f), 0.22f, "Objects", 0);
        core.gameObject.layer = LayerMask.NameToLayer("Enemy");
        core.gameObject.AddComponent<BoxCollider2D>().isTrigger = true;
        InfectionHeart heart = core.gameObject.AddComponent<InfectionHeart>();
        SerializedObject so = new SerializedObject(heart);
        so.FindProperty("bloodVesselDoor").objectReferenceValue = door;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 체크포인트 (보스방 직전)
        GameObject cp = Placeholder(objects, "체크포인트", 123.6f, 124.4f, Surface(124f), Surface(124f) + 1.6f, "Objects", true);
        cp.tag = "CheckPoint";
        cp.GetComponent<BoxCollider2D>().isTrigger = true;
        cp.AddComponent<CheckPoint>();

        // --- 튜토리얼 안내 문구 (이전 순환계 맵과 같은 문구) ---
        Transform hints = new GameObject("안내문구").transform;
        Hint(hints, "← → 이동   ↑ 점프", 4f);
        Hint(hints, "X 공격", 25f);
        Hint(hints, "공중에서 C 특수공격", 38f);
        Hint(hints, "Space 대쉬로 몬스터 회피", 53f);
        Hint(hints, "혈류 점프맵", 61f);
        Hint(hints, "F 체력 회복", 91f);

        // --- 양 끝 벽 + 맵 전체 천장 (자리) ---
        Transform walls = new GameObject("벽_천장").transform;
        Placeholder(walls, "왼쪽벽", MapLeft - 2f, MapLeft, -8f, CeilingTop, "Ground", true);
        Placeholder(walls, "오른쪽벽", MapRight, MapRight + 2f, -8f, CeilingTop, "Ground", true);
        Placeholder(walls, "천장", MapLeft - 2f, BossStart, CeilingY, CeilingTop, "Ground", true);
        Placeholder(walls, "천장_보스방", BossStart, BossEnd, BossCeilingY, CeilingTop, "Ground", true);
        Placeholder(walls, "천장_거대한문", BossEnd, MapRight + 2f, DoorCeilingY, CeilingTop, "Ground", true);

        BuildBossRoom(objects);

        // --- 몬스터 자리 ---
        Transform enemies = new GameObject("몬스터자리").transform;
        foreach (float x in new[] { 28f, 33f, 43f })
        {
            string label = x == 43f ? "몬스터자리_부서지는바닥아래" : "몬스터자리_전투";
            Placeholder(enemies, label, x - 0.5f, x + 0.5f, Surface(x), Surface(x) + 1f, "Enemies", false);
        }

        // --- 플레이어 / 카메라 / 게임 관리 ---
        Vector3 start = new Vector3(2f, Surface(2f) + 0.8f, 0f);
        GameObject startMarker = new GameObject("플레이어시작점");
        startMarker.transform.position = start;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab != null)
        {
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = start;
            foreach (SpriteRenderer sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Player";
            }

            GameObject gm = new GameObject("GameFlowManager");
            gm.AddComponent<GameFlowManager>().playerPrefab = prefab;
        }
        else
        {
            Debug.LogWarning("Player 프리팹을 못 찾았어요: " + PlayerPrefabPath);
        }

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.02f, 0.04f);
        camGo.transform.position = new Vector3(start.x + 2f, start.y + 1.5f, -10f);
        camGo.AddComponent<AudioListener>();
        CameraFollow follow = camGo.AddComponent<CameraFollow>();
        follow.useBounds = true;    // 벽/천장 바깥은 화면에 안 보이게
        follow.boundsMin = new Vector2(MapLeft - 2f, -8f);
        follow.boundsMax = new Vector2(MapRight + 2f, CeilingTop);

        EditorSceneManager.SaveScene(scene, TutorialPath);
    }

    static void Far(Transform parent, string name, string path, float x, int order)
    {
        SpriteRenderer sr = Place(parent, name, path, new Vector2(x, 0f), 1f, "Background", order);
        ParallaxTile p = sr.gameObject.AddComponent<ParallaxTile>();
        p.parallax = 0.6f;
        p.period = 19.2f * 2f;
    }

    static GameObject Platform(Transform parent, string name, string path, float x, float bottomY, float scale)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float h = s != null ? s.bounds.size.y * scale : 1f;
        SpriteRenderer sr = Place(parent, name, path, new Vector2(x, bottomY + h * 0.5f), scale, "Objects", 0);
        sr.gameObject.tag = "Ground";
        sr.gameObject.AddComponent<BoxCollider2D>();
        return sr.gameObject;
    }

    // 예전 메인 씬 맵의 9_보스방, 10_거대한문을 옮겨 온 것
    static void BuildBossRoom(Transform parent)
    {
        Transform boss = new GameObject("보스방").transform;
        boss.SetParent(parent, false);

        // 땅1/땅2 그림은 128에서 끝나므로 그 뒤 바닥은 자리 표시 (윗면 -1.9)
        GameObject floor = Placeholder(boss, "보스방_바닥", 127.8f, MapRight, -8f, GroundTopY, "Ground", true);
        floor.tag = "Ground";

        Placeholder(boss, "[보스] 혈전 (죽은 자리에 백신 드롭)", 145f, 149f, GroundTopY, GroundTopY + 3f, "Enemies", false);
        Placeholder(boss, "[항체] 보스방 중간에 드롭", 139.6f, 140.4f, GroundTopY + 3.6f, GroundTopY + 4.4f, "Objects", false);

        // 경사: 0.6씩 5칸 올라감
        for (int i = 0; i < 5; i++)
        {
            float x0 = BossEnd + 2f + i * 4f;
            float top = GroundTopY + 0.6f * (i + 1);
            GameObject step = Placeholder(boss, "경사" + (i + 1), x0, MapRight, GroundTopY, top, "Ground", true);
            step.tag = "Ground";
        }

        // 거대한 문: 마지막 계단 위에서 천장까지
        float doorBottom = GroundTopY + 3f;
        Placeholder(boss, "[거대한 혈관 문] 백신으로 열기", MapRight - 2.5f, MapRight - 1.5f, doorBottom, DoorCeilingY, "Objects", true);
    }

    static GameObject BuildDoor(Transform parent, float x)
    {
        float y0 = Surface(x);
        float h = CeilingY - y0;    // 천장까지 막아서 점프로 넘어갈 수 없게

        GameObject door = new GameObject("혈관문");
        door.transform.SetParent(parent, false);
        door.transform.position = new Vector3(x, y0 + h * 0.5f, 0f);
        BoxCollider2D zone = door.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(6f, h);

        Placeholder(door.transform, "혈관문_아랫파트", x - 0.5f, x + 0.5f, y0, y0 + h * 0.5f, "Objects", true);
        Placeholder(door.transform, "혈관문_윗파트", x - 0.5f, x + 0.5f, y0 + h * 0.5f, y0 + h, "Objects", true);

        GameObject hint = Label(door.transform, "감염 핵을 제거하고 오자.", new Vector3(x - 2.5f, y0 + 2.5f, 0f), Color.white);
        hint.name = "안내문구";
        hint.GetComponent<TextMesh>().anchor = TextAnchor.MiddleCenter;
        door.AddComponent<DoorHint>().hint = hint;
        return door;
    }

    // ================= 타이틀 =================

    static void BuildTitle()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        TitleMenu menu = canvasGo.AddComponent<TitleMenu>();
        Transform root = canvasGo.transform;

        // 순서: 일러 -> 로고 -> 버튼 -> 팀명 (설정 패널은 맨 위에 보여야 해서 마지막)
        RectTransform illu = UI(root, "Title_illustration", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image illuImg = illu.gameObject.AddComponent<Image>();
        illuImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitleImg);
        illuImg.preserveAspect = false;
        illuImg.raycastTarget = false;

        // 위치는 시안 이미지(1200x685)를 1920x1080 기준으로 환산한 값
        RectTransform logo = UI(root, "로고(임시)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -100f), new Vector2(600f, 180f));
        Text logoText = Txt(logo, "로고", 110, TextAnchor.MiddleCenter, Color.white);
        logoText.fontStyle = FontStyle.Bold;

        Button start = TitleButton(root, "Game Start", -440f);
        Button setting = TitleButton(root, "Setting", -665f);
        Button quit = TitleButton(root, "Quit", -890f);

        RectTransform team = UI(root, "팀명_버전", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(400f, 30f));
        Txt(team, "팀 이름  v0.1", 18, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.6f));

        // 설정 패널 (빈 패널, 열고 닫기만)
        RectTransform panel = UI(root, "SettingPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.06f, 0.12f, 0.92f);
        panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.3f, 0.7f, 1f, 1f);
        RectTransform panelTitle = UI(panel, "제목", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(0f, 80f));
        Txt(panelTitle, "Setting (준비 중)", 44, TextAnchor.MiddleCenter, Color.white);
        Button close = TitleButton(panel, "Close", 0f);
        RectTransform closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 40f);
        closeRt.sizeDelta = new Vector2(300f, 100f);
        close.GetComponent<Image>().pixelsPerUnitMultiplier = 128f / closeRt.sizeDelta.y;

        menu.settingPanel = panel.gameObject;
        UnityEventTools.AddPersistentListener(start.onClick, menu.OnGameStart);
        UnityEventTools.AddPersistentListener(setting.onClick, menu.OnSetting);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.OnQuit);
        UnityEventTools.AddPersistentListener(close.onClick, menu.OnCloseSetting);

        // EventSystem: Input System 패키지가 있으면 그 모듈을 사용
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        Type inputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModule != null) es.AddComponent(inputModule);
        else es.AddComponent<StandaloneInputModule>();

        EditorSceneManager.SaveScene(scene, TitlePath);
    }

    // 시안처럼 양 끝이 둥근 흰 알약 버튼 + 검은 글씨. 마우스를 올리면 더 밝아지고 살짝 파란 빛
    static Button TitleButton(Transform parent, string label, float y)
    {
        RectTransform rt = UI(parent, label + " 버튼", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(125f, y), new Vector2(350f, 128f));
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = GetPillSprite();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 128f / rt.sizeDelta.y;   // 모서리 반지름 = 버튼 높이의 절반
        img.color = Color.white;

        Button b = rt.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        cb.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
        cb.selectedColor = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        cb.pressedColor = new Color(0.7f, 0.85f, 1f, 1f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        RectTransform textRt = UI(rt, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Txt(textRt, label, 40, TextAnchor.MiddleCenter, Color.black);
        return b;
    }

    // 알약 버튼용 흰 원 스프라이트 (9-slice로 늘려서 양 끝만 둥글게)
    static Sprite GetPillSprite()
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
        if (s != null)
        {
            return s;
        }

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));   // 가장자리 1px 부드럽게
            }
        }
        System.IO.File.WriteAllBytes(PillPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(PillPath);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(PillPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.spriteBorder = new Vector4(63f, 63f, 63f, 63f);
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
    }

    static RectTransform UI(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Text Txt(RectTransform rt, string text, int size, TextAnchor align, Color color)
    {
        Text t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    // ================= 공통 도우미 =================

    static SpriteRenderer Place(Transform parent, string name, string path, Vector2 pos, float scale, string layer, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        if (sr.sprite == null)
        {
            Debug.LogWarning("스프라이트를 못 찾았어요: " + path);
        }
        return sr;
    }

    // 땅 윗면을 따라가는 Edge Collider (Ground 태그)
    static void Edge(Transform parent, string name, GroundInfo g, float fromX, float toX)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.tag = "Ground";

        var pts = new System.Collections.Generic.List<Vector2>();
        pts.Add(new Vector2(fromX, g.SurfaceY(fromX)));
        for (int i = 0; i < g.cols.Length; i++)
        {
            Vector2 p = g.Point(i);
            if (p.x > fromX && p.x < toX) pts.Add(p);
        }
        float endX = Mathf.Min(toX, g.Point(g.cols.Length - 1).x);
        if (endX > pts[pts.Count - 1].x + 0.01f)
        {
            pts.Add(new Vector2(endX, g.SurfaceY(endX)));
        }

        go.AddComponent<EdgeCollider2D>().points = pts.ToArray();
    }

    // 이미지가 없는 것: 회색 반투명 사각형 + 이름표
    static GameObject Placeholder(Transform parent, string name, float x0, float x1, float y0, float y1, string layer, bool collider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        Vector2 size = new Vector2(x1 - x0, y1 - y0);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        sr.sortingLayerName = layer;

        if (collider)
        {
            go.AddComponent<BoxCollider2D>().size = size;
        }

        Label(go.transform, name, go.transform.position, new Color(1f, 1f, 1f, 0.9f));
        return go;
    }

    // 튜토리얼 안내 문구: 땅 위 2.2 높이
    static void Hint(Transform parent, string text, float x)
    {
        GameObject go = Label(parent, text, new Vector3(x, Surface(x) + 2.2f, 0f), Color.white);
        go.name = "[안내] " + text;
        go.transform.localScale = Vector3.one * 0.1f;
    }

    static GameObject Label(Transform parent, string text, Vector3 pos, Color color)
    {
        GameObject go = new GameObject("이름표");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.06f;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = color;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingLayerName = "FX";
        return go;
    }

    // ================= 프로젝트 설정 =================

    static void SetupImports()
    {
        foreach (string path in OwnImages)
        {
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("이미지를 못 찾았어요: " + path);
                continue;
            }

            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            int max = Mathf.Max(w, h);
            int maxSize = max <= 2048 ? 2048 : max <= 4096 ? 4096 : max <= 8192 ? 8192 : 16384;

            bool changed = ti.textureType != TextureImporterType.Sprite
                || ti.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(ti.spritePixelsPerUnit, PPU)
                || ti.maxTextureSize != maxSize
                || ti.mipmapEnabled;
            if (!changed) continue;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = maxSize;     // 땅(12870px)이 8192로 줄어들지 않게
            ti.SaveAndReimport();
        }
    }

    // Sorting Layer (뒤 -> 앞) 와 태그를 없을 때만 추가합니다.
    static void EnsureSortingLayersAndTags()
    {
        SerializedObject tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layers = tm.FindProperty("m_SortingLayers");
        foreach (string name in SortingLayers)
        {
            bool exists = false;
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                {
                    exists = true;
                    break;
                }
            }
            if (exists) continue;

            layers.InsertArrayElementAtIndex(layers.arraySize);
            SerializedProperty e = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            e.FindPropertyRelative("name").stringValue = name;
            e.FindPropertyRelative("uniqueID").intValue = (name.GetHashCode() & 0x7FFFFFFF) | 1;
            e.FindPropertyRelative("locked").intValue = 0;
        }

        SerializedProperty tags = tm.FindProperty("tags");
        foreach (string tag in new[] { "Ground", "CheckPoint" })
        {
            bool exists = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) exists = true;
            }
            if (!exists)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
        }

        tm.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
