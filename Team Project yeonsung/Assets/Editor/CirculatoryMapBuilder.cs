using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 기획서의 "순환계-튜토리얼 구역" 맵을 지금 열려 있는 씬에 블록(회색 상자)으로 깔아주는 도구
// 메뉴: Tools > 맵 > 순환계 튜토리얼 맵 만들기
// 다시 실행하면 이전에 만든 맵(Map_순환계)을 지우고 새로 만듭니다. (Ctrl+Z로 되돌리기 가능)
public static class CirculatoryMapBuilder
{
    const string RootName = "Map_순환계";
    const string SquarePath = "Assets/Map/Square.png";
    const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

    // 땅 바닥(-6)과 천장 꼭대기(16) 기준선
    const float Bottom = -6f;
    const float Top = 16f;

    static readonly Color WallColor = new Color(0.35f, 0.05f, 0.08f);
    static readonly Color FloorColor = new Color(0.45f, 0.07f, 0.1f);
    static readonly Color CellColor = new Color(0.85f, 0.15f, 0.18f);     // 적혈구
    static readonly Color BloodColor = new Color(0.9f, 0.05f, 0.1f, 0.85f);
    static readonly Color MarkerColor = new Color(0.6f, 0.2f, 0.9f, 0.5f); // 몬스터 등 배치 표시
    static readonly Color ItemColor = new Color(0.3f, 0.8f, 1f, 0.6f);
    static readonly Color DoorColor = new Color(0.95f, 0.3f, 0.6f, 0.9f);

    static Sprite square;
    static Transform root;

    [MenuItem("Tools/맵/순환계 튜토리얼 맵 만들기")]
    public static void Build()
    {
        EnsureTagsAndLayers();
        square = GetSquareSprite();

        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "순환계 맵 만들기");

        BuildStartVessel();     // 1. 시작 혈관        x 0 ~ 30
        BuildJumpSection();     // 2. 기본 점프 구간   x 30 ~ 60
        BuildCombatSection();   // 3. 기본 전투 구간   x 60 ~ 80
        BuildDiveSection();     // 4. 낙하 특수공격    x 80 ~ 95
        BuildFinalTest();       // 5. 종합 테스트      x 95 ~ 125
        BuildBloodFlowJump();   // 6. 혈류 점프맵      x 125 ~ 170
        BuildDodgeSection();    // 7. 회피 연습        x 170 ~ 195
        BuildInfectedCore();    // 8. 감염 핵 구역     x 195 ~ 245
        BuildBossRoom();        // 9. 보스방           x 245 ~ 275
        BuildGiantDoor();       // 10. 경사 + 거대한 문 x 275 ~ 300

        SetupPlayer(new Vector3(3f, 1f, 0f));
        SetupCamera();
        SetupGameFlowManager();
        SendBackgroundCanvasBehind();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        Debug.Log("순환계 튜토리얼 맵을 만들었어요! Ctrl+S로 씬을 저장하세요.");
    }

    // ---------------- 구역 ----------------

    static void BuildStartVessel()
    {
        Transform s = Section("1_시작혈관");
        Wall(s, "왼쪽벽", -4f, -2f, Bottom, Top);
        Floor(s, "바닥", -2f, 30f, 0f);
        Ceiling(s, "천장", -2f, 30f, 6f);
        Item(s, "[아이템] 나노 수리 캡슐", 1f, 0.5f);
        Hint(s, "← → 이동   ↑ 점프", 6f, 3f);
        CheckPointAt(s, "체크포인트_시작", 3f);
        Hint(s, "혈류 점프맵", 121f, 3f);
    }

    static void BuildJumpSection()
    {
        Transform s = Section("2_점프구간");
        // 얕은 구덩이: 떨어져도 바로 다시 점프해서 올라올 수 있는 깊이
        Floor(s, "구덩이바닥", 30f, 60f, -1.5f);
        Floor(s, "바닥1", 30f, 36f, 0f);
        Floor(s, "바닥2", 38f, 44f, 0f);
        Floor(s, "낮은장애물1", 41f, 42f, 0.7f, 0f);
        Floor(s, "바닥3", 46.5f, 52f, 0f);
        Floor(s, "장애물2", 49f, 50f, 1.2f, 0f);
        Floor(s, "바닥4", 54f, 60f, 0f);
        Ceiling(s, "천장", 30f, 60f, 6f);
    }

    static void BuildCombatSection()
    {
        Transform s = Section("3_전투구간");
        Floor(s, "바닥", 60f, 80f, 0f);
        Ceiling(s, "천장", 60f, 80f, 6f);
        Hint(s, "X 공격", 63f, 3f);
        Marker(s, "[몬스터] 바이러스1", 68f, 0.5f);
        Marker(s, "[몬스터] 바이러스2", 75f, 0.5f);
    }

    static void BuildDiveSection()
    {
        Transform s = Section("4_낙하공격");
        Floor(s, "바닥", 80f, 95f, 0f);
        Floor(s, "계단", 78f, 80f, 1.2f, 0f);
        Floor(s, "높은발판", 80f, 88f, 2.6f, 2.2f);
        Ceiling(s, "천장", 80f, 95f, 8f);
        Hint(s, "공중에서 C 특수공격", 84f, 5f);
        Marker(s, "[몬스터] 낙하공격용 바이러스", 90f, 0.5f);
    }

    static void BuildFinalTest()
    {
        Transform s = Section("5_종합테스트");
        Floor(s, "구덩이바닥", 95f, 125f, -1.5f);
        Floor(s, "바닥1", 95f, 100f, 0f);
        Floor(s, "바닥2", 102.5f, 110f, 0f);
        Floor(s, "장애물", 106f, 107f, 1f, 0f);
        Floor(s, "바닥3", 113f, 125f, 0f);
        Ceiling(s, "천장", 95f, 125f, 6f);
        Marker(s, "[몬스터] 바이러스", 118f, 0.5f);
    }

    static void BuildBloodFlowJump()
    {
        Transform s = Section("6_혈류점프맵");
        // 혈관 하단: 단단한 바닥 위로 혈류가 흐르고, 그 위에 적혈구 발판
        Floor(s, "혈관바닥", 125f, 170f, -2f);
        Blood(s, "혈류", 125f, 170f, -2f, -0.6f);
        Ceiling(s, "천장", 125f, 170f, 6f);

        Cell(s, "적혈구1", 128.5f, 0f);
        Cell(s, "적혈구2", 132.5f, 0.6f);
        MovingCell(s, "적혈구3_좌우", 136.5f, 0f, new Vector2(2f, 0f), 3f);
        Cell(s, "적혈구4", 142f, 0.8f);
        Cell(s, "적혈구5", 146f, 0f);
        MovingCell(s, "적혈구6_상하", 150f, 0f, new Vector2(0f, 1.5f), 2.5f);
        Cell(s, "적혈구7", 154f, 0.6f);
        MovingCell(s, "적혈구8_좌우", 158f, 0.3f, new Vector2(3f, 0f), 3.5f);
        Cell(s, "적혈구9", 164.5f, 0f);

        // 양쪽 끝 단단한 땅 (혈류 위로 튀어나온 부분)
        Floor(s, "출구발판", 168f, 170f, 0f, -0.6f);
    }

    static void BuildDodgeSection()
    {
        Transform s = Section("7_회피연습");
        Floor(s, "바닥", 170f, 195f, 0f);
        Ceiling(s, "천장", 170f, 195f, 6f);
        Hint(s, "Space 대쉬로 몬스터 회피", 173f, 3f);
        Marker(s, "[몬스터] 바이러스1", 180f, 0.5f);
        Marker(s, "[몬스터] 바이러스2", 185f, 0.5f);
        Marker(s, "[몬스터] 바이러스3", 190f, 0.5f);
    }

    static void BuildInfectedCore()
    {
        Transform s = Section("8_감염핵구역");
        Floor(s, "바닥", 195f, 245f, 0f);
        Ceiling(s, "천장", 195f, 245f, 10f);
        Hint(s, "F 체력 회복", 197f, 3f);

        Floor(s, "발판1", 200f, 203f, 1.6f, 1.2f);
        Floor(s, "중앙발판", 205f, 225f, 3.4f, 3f);
        Floor(s, "윗발판", 212f, 216f, 5.2f, 4.8f);
        Marker(s, "[감염 핵] 처치하면 문이 열림", 214f, 6f);
        Item(s, "[아이템] 나노 수리 캡슐", 222f, 4f);

        Floor(s, "기둥1", 228f, 229.5f, 1.8f, 0f);
        Floor(s, "기둥2", 232f, 233.5f, 1.8f, 0f);
        Marker(s, "[몬스터] 바이러스", 217f, 0.5f);

        Door(s, "[감염된 혈관 문]", 238f, 0f, 4f);
        Hint(s, "감염 핵을 제거하고 오자.", 236f, 5f);
        CheckPointAt(s, "체크포인트_보스방앞", 242f);
    }

    static void BuildBossRoom()
    {
        Transform s = Section("9_보스방");
        Floor(s, "바닥", 245f, 275f, 0f);
        Ceiling(s, "천장", 245f, 275f, 12f);
        Marker(s, "[보스] 혈전", 265f, 1.5f);
        Item(s, "[항체] 보스 처치 후 드롭", 260f, 4f);
    }

    static void BuildGiantDoor()
    {
        Transform s = Section("10_거대한문");
        Floor(s, "바닥", 275f, 300f, 0f);
        for (int i = 0; i < 5; i++)
        {
            float x = 277f + i * 4f;
            Floor(s, "경사" + (i + 1), x, 300f, 0.6f * (i + 1), 0f);
        }
        Ceiling(s, "천장", 275f, 300f, 10f);
        Door(s, "[거대한 혈관 문] 백신으로 열기", 297f, 3f, 6f);
        Wall(s, "오른쪽벽", 300f, 302f, Bottom, Top);
    }

    // ---------------- 블록 만들기 ----------------

    static Transform Section(string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    // 밟을 수 있는 땅 (Ground 태그). bottom을 생략하면 맨 아래까지 채움
    static GameObject Floor(Transform parent, string name, float x0, float x1, float top, float bottom = Bottom)
    {
        GameObject go = Block(parent, name, x0, x1, bottom, top, FloorColor, 0);
        go.tag = "Ground";
        return go;
    }

    // 천장: y부터 위쪽 끝까지 (Ground 태그 아님, 머리 박아도 착지 판정 안 되게)
    static GameObject Ceiling(Transform parent, string name, float x0, float x1, float y)
    {
        return Block(parent, name, x0, x1, y, Top, WallColor, 0);
    }

    static GameObject Wall(Transform parent, string name, float x0, float x1, float y0, float y1)
    {
        return Block(parent, name, x0, x1, y0, y1, WallColor, 0);
    }

    // A: 고정형 적혈구 발판
    static GameObject Cell(Transform parent, string name, float centerX, float top)
    {
        GameObject go = Block(parent, name, centerX - 1f, centerX + 1f, top - 0.4f, top, CellColor, 2);
        go.tag = "Ground";
        return go;
    }

    // B: 왕복형 적혈구 발판
    static GameObject MovingCell(Transform parent, string name, float centerX, float top, Vector2 offset, float cycle)
    {
        GameObject go = Cell(parent, name, centerX, top);
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        MovingPlatform mp = go.AddComponent<MovingPlatform>();
        mp.moveOffset = offset;
        mp.cycleTime = cycle;
        return go;
    }

    static GameObject Blood(Transform parent, string name, float x0, float x1, float y0, float y1)
    {
        GameObject go = Block(parent, name, x0, x1, y0, y1, BloodColor, 5);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<BloodFlow>();
        return go;
    }

    // 문은 지나갈 수 없게 콜라이더가 있지만 Ground는 아님
    static GameObject Door(Transform parent, string name, float x, float y, float height)
    {
        return Block(parent, name, x - 0.5f, x + 0.5f, y, y + height, DoorColor, 3);
    }

    // 몬스터/핵/보스 등 나중에 실제 프리팹으로 바꿀 자리 표시 (콜라이더 없음)
    static void Marker(Transform parent, string name, float x, float y)
    {
        GameObject go = Block(parent, name, x - 0.4f, x + 0.4f, y - 0.4f, y + 0.4f, MarkerColor, 10);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
    }

    // 실제로 작동하는 체크포인트 (닿으면 저장, GameFlowManager가 CheckPoint 태그로 찾음)
    static void CheckPointAt(Transform parent, string name, float x)
    {
        GameObject go = Block(parent, name, x - 0.4f, x + 0.4f, 0f, 1.5f, new Color(0.3f, 1f, 0.4f, 0.4f), 10);
        go.tag = "CheckPoint";
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        CheckPoint cp = go.AddComponent<CheckPoint>();
        cp.activateOnTouch = true;
    }

    static void Item(Transform parent, string name, float x, float y)
    {
        GameObject go = Block(parent, name, x - 0.25f, x + 0.25f, y - 0.25f, y + 0.25f, ItemColor, 10);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
    }

    // 튜토리얼 안내 문구
    static void Hint(Transform parent, string text, float x, float y)
    {
        GameObject go = new GameObject("[안내] " + text);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = Vector3.one * 0.1f;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleLeft;
        tm.color = Color.white;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = 20;
    }

    // 크기는 scale 대신 SpriteRenderer.size / BoxCollider2D.size로 정해서
    // 위에 올라탄 오브젝트가 이상하게 늘어나지 않게 합니다.
    static GameObject Block(Transform parent, string name, float x0, float x1, float y0, float y1, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        Vector2 size = new Vector2(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = order;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        return go;
    }

    // ---------------- 씬 설정 ----------------

    static void SetupPlayer(Vector3 spawn)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        PlayerController found = null;

        foreach (PlayerController pc in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool fromPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pc.gameObject) == PlayerPrefabPath;
            if (fromPrefab && found == null)
            {
                found = pc;
            }
            else if (!fromPrefab && pc.gameObject.activeSelf)
            {
                // 프리팹이 아닌 예전 플레이어는 지우지 않고 꺼두기만 합니다.
                Undo.RecordObject(pc.gameObject, "예전 플레이어 끄기");
                pc.gameObject.SetActive(false);
                pc.gameObject.name = pc.gameObject.name + " (예전, 꺼둠)";
                Debug.Log("프리팹이 아닌 예전 Player를 꺼뒀어요: " + pc.gameObject.name);
            }
        }

        if (found == null)
        {
            if (prefab == null)
            {
                Debug.LogWarning("Player 프리팹을 찾지 못했어요: " + PlayerPrefabPath);
                return;
            }
            GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(p, "플레이어 배치");
            found = p.GetComponent<PlayerController>();
        }

        Undo.RecordObject(found.transform, "플레이어 위치");
        found.transform.SetParent(null, true);
        found.transform.position = spawn;
        found.gameObject.SetActive(true);
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Undo.RecordObject(cam, "카메라 설정");
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(5f, 2.5f, -10f);

        if (cam.GetComponent<CameraFollow>() == null)
        {
            Undo.AddComponent<CameraFollow>(cam.gameObject);
        }
    }

    static void SetupGameFlowManager()
    {
        if (Object.FindFirstObjectByType<GameFlowManager>() != null)
        {
            return;
        }

        GameObject go = new GameObject("GameFlowManager");
        Undo.RegisterCreatedObjectUndo(go, "GameFlowManager");
        GameFlowManager gm = go.AddComponent<GameFlowManager>();
        gm.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
    }

    // Screen Space - Overlay 캔버스는 모든 것 위에 그려져서 맵을 가립니다.
    // 배경 이미지가 든 캔버스를 카메라 뒤쪽(Screen Space - Camera)으로 보냅니다.
    static void SendBackgroundCanvasBehind()
    {
        Camera cam = Camera.main;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                continue;
            }
            if (c.GetComponentInChildren<UnityEngine.UI.Image>() == null)
            {
                continue;
            }

            Undo.RecordObject(c, "배경 캔버스 뒤로");
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 100f;
            c.sortingOrder = -100;
            Debug.Log("배경 캔버스 '" + c.name + "'를 맵 뒤로 보냈어요 (Screen Space - Camera).");
        }
    }

    // ---------------- 준비물 ----------------

    // 1칸 = 1유닛인 흰색 사각형 스프라이트를 만들어 둡니다.
    static Sprite GetSquareSprite()
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (s != null)
        {
            return s;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SquarePath));
        Texture2D tex = new Texture2D(4, 4);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SquarePath);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 4;
        ti.filterMode = FilterMode.Point;
        TextureImporterSettings settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
    }

    // 커밋된 TagManager에 충돌 표시(<<<<<<<)가 남아 태그가 날아갔어서, 필요한 태그/레이어를 다시 넣습니다.
    static void EnsureTagsAndLayers()
    {
        SerializedObject tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tm.FindProperty("tags");

        string[] needed = { "Ground", "CheckPoint", "체크포인트", "PlayerAttack", "BoseRoomDashFloor" };
        foreach (string tag in needed)
        {
            bool exists = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
        }

        SerializedProperty layers = tm.FindProperty("layers");
        SetLayerIfEmpty(layers, 7, "Player");
        SetLayerIfEmpty(layers, 8, "Enemy");

        tm.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    static void SetLayerIfEmpty(SerializedProperty layers, int index, string name)
    {
        SerializedProperty p = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(p.stringValue))
        {
            p.stringValue = name;
        }
    }
}
