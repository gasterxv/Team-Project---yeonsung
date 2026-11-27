using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 새 맵 회색 상자 초안을 '메인씬 테스트' 씬에 만드는 도구.
// 좌표 = docs/superpowers/specs/2026-11-26-new-map-design.md
// 메뉴: Tools > 새맵 > 1. 회색 상자 맵 만들기
//
// - 방(빈 공간)을 적어 두고, 그 바깥을 땅 블록으로 자동으로 채웁니다.
// - 실제로 동작: 땅·벽, 발판, 부서지는 발판(BreakablePlatform), 혈소판(MovingPlatform), 혈류(BloodFlow), 체크포인트(CheckPoint),
//   백혈구 벽 X(WhiteCellWall), 백혈구 바닥 C(WhiteCellFloor), 항체+더블점프(AntibodyPickup, DoubleJumpAbility),
//   C 튕김 항체(BounceAntibody), 사망 시 초기화(CheckpointResetManager)
// - 표시만 (지나갈 수 있음): 노란 문, 보스방 문, 나노 캡슐, 감염핵, 보스, 몬스터
// - 예전 맵 묶음은 지우지 않고 꺼 두기만 합니다. 다시 실행하면 이 도구가 만든 '새맵_회색상자'만 새로 만듭니다.
public static class NewMapGrayboxBuilder
{
    const string SceneName = "메인씬 테스트";
    const string RootName = "새맵_회색상자";
    const string SquarePath = "Assets/Map/Square.png";
    const float Cell = 0.1f;
    const float MinX = -2.5f, MaxX = 203.5f, MinY = -34f, MaxY = 46f;
    const float PlayerScale = 2.44f;   // 키 약 1.2 유닛

    static readonly Color GroundColor = new Color(0.47f, 0.04f, 0.09f);
    static readonly Color PlatColor = new Color(0.53f, 0.53f, 0.5f);
    static readonly Color BreakColor = new Color(0.85f, 0.35f, 0.19f);
    static readonly Color PlateletColor = new Color(0.78f, 0.16f, 0.2f);
    static readonly Color InfectedColor = new Color(0.55f, 0.2f, 0.7f);
    static readonly Color BloodColor = new Color(0.89f, 0.16f, 0.2f, 0.85f);

    static Sprite square;
    static Transform root;

    // 방 안쪽 빈 공간: x0, x1, 바닥 y, 천장 y
    static readonly float[,] Rooms =
    {
        { 0, 22, 0, 12 },            // R1 시작
        { 22, 44, 0, 14 },           // R2 초반 전투
        { 44, 69.5f, 0, 15 },        // R3 완충
        { 69.5f, 110.5f, 0, 18 },    // R4 중앙 연결
        { 93.5f, 110.5f, 18, 43.5f },// 수직 통로
        { 112, 156.5f, 0, 20 },      // R5 중후반
        { 110.5f, 166, 32.5f, 43.5f },// 5 위쪽 혈류 복도
        { 158, 166, 14, 32.5f },     // 하강 통로
        { 156.5f, 178, 0, 14 },      // R6 보스 전
        { 178, 197.2f, 0, 10.8f },   // R7 보스방
        { 197.2f, 201, 0, 4 },       // 출구 칸
        { 4, 12, 12, 27 },           // 6 캡슐 방
        { 10, 30, -17.6f, -2.1f },   // 숨은 방
        { 30, 44, -27.6f, -12.1f },  // 아래 캡슐 방
        { 46, 58, -10.1f, -2.1f },   // 함정
        { 74, 92, -20.1f, -2.1f },   // 아래방
        { 92, 109, -31.6f, -13.6f }, // 웨이브
        { 109, 125, -21, -11.5f },   // 감염핵
        { 163, 182, -15.6f, -2.1f }, // 함정 2 A
        { 182, 201, -22.6f, -8.6f }, // 함정 2 B
        // 추가 빈 공간
        { 119.5f, 152, -2.5f, 0 },   // 중후반 혈류 웅덩이
        { 114, 153, 30.5f, 32.5f },  // 복도 혈류 웅덩이
        { 18, 21, -2.1f, 0 },        // 구멍: 시작 → 숨은 방
        { 49, 52, -2.1f, 0 },        // 구멍: 완충 → 함정
        { 78, 81, -2.1f, 0 },        // 구멍: 중앙 연결 → 아래방
        { 172, 175, -2.1f, 0 },      // 구멍: 보스 전 → 함정 2 A
    };

    // 빈 공간 안에 다시 세우는 벽·턱: x0, x1, y0, y1
    static readonly float[,] Solids =
    {
        { 21.75f, 22.25f, 4, 14 },        // 시작 | 초반 (통로 높이 4)
        { 43.75f, 44.25f, 4, 15 },        // 초반 | 완충
        { 69.25f, 69.75f, 4, 18 },        // 완충 | 중앙 연결
        { 156.25f, 156.75f, 6, 20 },      // 중후반 | 보스 전
        { 177.75f, 178.25f, 6, 14 },      // 보스 전 | 보스방
        { 110.25f, 110.75f, 18, 32.5f },  // 수직 통로 | 복도 (문 아래)
        { 110.25f, 110.75f, 36.5f, 43.5f },// 수직 통로 | 복도 (문 위)
        { 29.75f, 30.25f, -13.6f, -12.1f },
        { 91.75f, 92.25f, -16.1f, -13.6f },
        { 108.75f, 109.25f, -17, -13.6f },
        { 181.75f, 182.25f, -11.6f, -8.6f },
        { 30, 33, -27.6f, -17.6f },       // 아래 캡슐 방 들어오는 턱
        { 92, 95, -31.6f, -20.1f },       // 웨이브 왼쪽 턱
        { 106, 109, -31.6f, -21 },        // 웨이브 오른쪽 턱
        { 182, 185, -22.6f, -15.6f },     // 함정 2 B 들어오는 턱
        { 105, 110.5f, 31.5f, 32.5f },    // 수직 통로 도착 턱
        { 153, 158, 32.5f, 33 },          // 복도 끝 땅
        { 118, 119.5f, 0, 16 },           // 중후반 높이 16 벽
    };

    // 고정 발판 (중심 x, 윗면 y), 폭 3
    static readonly float[,] Platforms =
    {
        { 6, 6 }, { 10, 11.5f }, { 6, 16.5f }, { 10, 21.5f },
        { 96, 3 }, { 101.5f, 6 }, { 96, 9 }, { 101.5f, 12 }, { 96, 15 },
        { 13, -14.6f }, { 17.5f, -11.6f }, { 13, -8.6f }, { 16.5f, -5.8f }, { 19.5f, -3 },
        { 40.5f, -15.6f }, { 35, -24.8f }, { 39.5f, -22 }, { 35.5f, -19.6f },
        { 48.5f, -7.4f }, { 54, -5.2f }, { 50.5f, -3 },
        { 88.5f, -17.3f }, { 84, -14.5f }, { 88.5f, -11.7f }, { 84, -8.9f }, { 88, -6.1f }, { 79.5f, -3 },
        { 101, -28.8f }, { 97, -26 }, { 101, -23.2f },
        { 168, -12.8f }, { 172.5f, -10 }, { 168, -7.2f }, { 176.5f, -5.8f }, { 173.5f, -3 },
        { 192, -19.7f }, { 187.5f, -17.8f },
    };

    // 부서지는 발판 (중심 x, 윗면 y, 복구 초)
    static readonly float[,] BreakPlats =
    {
        { 101.5f, 18, 10 }, { 96, 21, 10 }, { 101.5f, 24, 10 }, { 96, 27, 10 }, { 101.5f, 30, 10 },   // 수직 통로
        { 149.5f, 1.5f, 5 }, { 146, 3, 5 }, { 142.5f, 4.5f, 5 },                                         // 중후반 무너지는 계단
        { 184.5f, 3, 20 }, { 190.5f, 3, 20 },                                                            // 보스방
    };

    // 혈소판 (시작 중심 x, 윗면 y, 이동 x, 이동 y, 왕복 초, 감염 여부 1/0)
    static readonly float[,] Platelets =
    {
        { 117, 33.5f, 7, 0, 4, 0 }, { 127, 33.5f, 7, 0, 4.5f, 1 }, { 137, 33.5f, 7, 0, 4, 1 }, { 146.5f, 33.5f, 4.5f, 0, 3.5f, 0 },
        { 135, 4.5f, 4, 0, 3.5f, 0 },   // 중후반 셔틀
        { 130.5f, 3, 0, 7, 4, 0 },      // 중후반 엘리베이터
    };

    [MenuItem("Tools/새맵/1. 회색 상자 맵 만들기")]
    public static void Build()
    {
        if (EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("새맵", "'" + SceneName + "' 씬을 먼저 열어 주세요.\n(Assets/Scenes/" + SceneName + ".unity)", "확인");
            return;
        }
        square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (square == null)
        {
            Debug.LogError("[새맵] " + SquarePath + " 스프라이트가 없어요.");
            return;
        }

        Undo.SetCurrentGroupName("새맵 회색 상자 만들기");
        int group = Undo.GetCurrentGroup();

        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);   // 이 도구가 만든 묶음만 다시 만듦
        }

        TurnOffOldMap();

        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "새맵 회색 상자");

        BuildRules(Group("규칙 (초기화·더블점프)"));
        BuildGround(Group("땅"));
        BuildPlatforms(Group("발판"));
        BuildBlood(Group("혈류"));
        BuildCheckpoints(Group("체크포인트"));
        BuildGimmicks(Group("기믹 (X벽·C바닥·항체)"));
        BuildMarkers(Group("표시 (나중에 진짜로 바꿀 것)"));
        BuildHints(Group("안내 글자"));
        SetupPlayerAndCamera();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[새맵] 회색 상자 맵을 만들었어요. Ctrl+S로 저장한 뒤 Play로 걸어 보세요.");
    }

    // ---------------- 예전 맵 끄기 ----------------

    static void TurnOffOldMap()
    {
        List<string> off = new List<string>();
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.activeSelf || go.name == RootName) continue;
            bool keep = go.GetComponentInChildren<Camera>(true) != null
                || go.GetComponentInChildren<PlayerController>(true) != null
                || go.GetComponentInChildren<GameFlowManager>(true) != null
                || go.GetComponentInChildren<Canvas>(true) != null
                || go.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null
                || go.GetComponentInChildren<SoundManager>(true) != null
                || go.GetComponentInChildren<Light>(true) != null
                || go.name.Contains("Light");
            if (keep) continue;
            Undo.RecordObject(go, "예전 맵 끄기");
            go.SetActive(false);
            off.Add(go.name);
        }
        Debug.Log("[새맵] 예전 맵 묶음 " + off.Count + "개를 꺼 뒀어요 (지우지 않음): " + string.Join(", ", off));
    }

    // ---------------- 땅 (방이 아닌 곳을 채움) ----------------

    static void BuildGround(Transform parent)
    {
        int w = Mathf.RoundToInt((MaxX - MinX) / Cell);
        int h = Mathf.RoundToInt((MaxY - MinY) / Cell);
        bool[,] solid = new bool[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) solid[x, y] = true;

        for (int i = 0; i < Rooms.GetLength(0); i++) Mark(solid, Rooms[i, 0], Rooms[i, 1], Rooms[i, 2], Rooms[i, 3], false);
        for (int i = 0; i < Solids.GetLength(0); i++) Mark(solid, Solids[i, 0], Solids[i, 1], Solids[i, 2], Solids[i, 3], true);

        // 붙어 있는 칸을 큰 직사각형으로 합쳐서 블록 수를 줄입니다.
        bool[,] used = new bool[w, h];
        int count = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!solid[x, y] || used[x, y]) continue;
                int rw = 1;
                while (x + rw < w && solid[x + rw, y] && !used[x + rw, y]) rw++;
                int rh = 1;
                bool grow = true;
                while (grow && y + rh < h)
                {
                    for (int k = 0; k < rw; k++)
                    {
                        if (!solid[x + k, y + rh] || used[x + k, y + rh]) { grow = false; break; }
                    }
                    if (grow) rh++;
                }
                for (int a = 0; a < rw; a++) for (int b = 0; b < rh; b++) used[x + a, y + b] = true;

                float x0 = MinX + x * Cell, y0 = MinY + y * Cell;
                GameObject block = Block(parent, "땅", x0, x0 + rw * Cell, y0, y0 + rh * Cell, GroundColor, 0);
                MakeGround(block);
                count++;
            }
        }
        Debug.Log("[새맵] 땅 블록 " + count + "개");
    }

    static void Mark(bool[,] grid, float x0, float x1, float y0, float y1, bool value)
    {
        int ax = Mathf.RoundToInt((x0 - MinX) / Cell), bx = Mathf.RoundToInt((x1 - MinX) / Cell);
        int ay = Mathf.RoundToInt((y0 - MinY) / Cell), by = Mathf.RoundToInt((y1 - MinY) / Cell);
        for (int x = Mathf.Max(0, ax); x < Mathf.Min(grid.GetLength(0), bx); x++)
            for (int y = Mathf.Max(0, ay); y < Mathf.Min(grid.GetLength(1), by); y++)
                grid[x, y] = value;
    }

    // ---------------- 발판 ----------------

    static void BuildPlatforms(Transform parent)
    {
        for (int i = 0; i < Platforms.GetLength(0); i++)
        {
            float cx = Platforms[i, 0], top = Platforms[i, 1];
            MakeGround(Block(parent, "발판", cx - 1.5f, cx + 1.5f, top - 0.6f, top, PlatColor, 2));
        }

        for (int i = 0; i < BreakPlats.GetLength(0); i++)
        {
            float cx = BreakPlats[i, 0], top = BreakPlats[i, 1];
            GameObject go = Block(parent, "부서지는 발판", cx - 1.5f, cx + 1.5f, top - 0.6f, top, BreakColor, 2);
            MakeGround(go);
            BreakablePlatform bp = go.AddComponent<BreakablePlatform>();
            bp.breakDelay = 1f;
            bp.restoreDelay = BreakPlats[i, 2];
        }

        for (int i = 0; i < Platelets.GetLength(0); i++)
        {
            float cx = Platelets[i, 0], top = Platelets[i, 1];
            bool infected = Platelets[i, 5] > 0.5f;
            GameObject go = Block(parent, infected ? "감염된 혈소판" : "혈소판", cx - 1.75f, cx + 1.75f, top - 0.7f, top,
                infected ? InfectedColor : PlateletColor, 3);
            MakeGround(go);
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            MovingPlatform mp = go.AddComponent<MovingPlatform>();
            mp.moveOffset = new Vector2(Platelets[i, 2], Platelets[i, 3]);
            mp.cycleTime = Platelets[i, 4];
        }
    }

    // ---------------- 혈류 ----------------

    static void BuildBlood(Transform parent)
    {
        Blood(parent, "혈류_중후반", 119.5f, 152, -2.5f, -1f);
        Blood(parent, "혈류_복도", 114, 153, 30.5f, 31.5f);
    }

    static void Blood(Transform parent, string name, float x0, float x1, float y0, float y1)
    {
        GameObject go = Block(parent, name, x0, x1, y0, y1, BloodColor, 5);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<BloodFlow>();
    }

    // ---------------- 체크포인트 ----------------

    static void BuildCheckpoints(Transform parent)
    {
        CheckPointAt(parent, "시작 지점 (보이지 않는 부활 위치)", 3f, true, false);
        CheckPointAt(parent, "체크포인트 1 (완충)", 66f, false, true);
        CheckPointAt(parent, "체크포인트 2 (보스 전)", 170f, false, true);
    }

    static void CheckPointAt(Transform parent, string name, float x, bool startSpot, bool visible)
    {
        GameObject go = Block(parent, name, x - 0.4f, x + 0.4f, 0f, 1.5f, new Color(0.39f, 0.6f, 0.13f, visible ? 0.8f : 0f), 10);
        go.tag = "CheckPoint";
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        CheckPoint cp = go.AddComponent<CheckPoint>();
        cp.activateOnTouch = true;
        cp.checkPointSpot = startSpot;
        go.AddComponent<CheckpointSaveTrigger>();   // 밟으면 맵 상태 저장
    }

    // ---------------- 규칙 ----------------

    static void BuildRules(Transform parent)
    {
        GameObject go = new GameObject("규칙");
        go.transform.SetParent(parent, false);
        go.AddComponent<CheckpointResetManager>();
        go.AddComponent<DoubleJumpAbility>();
    }

    // ---------------- 기믹 (실제로 동작) ----------------

    static void BuildGimmicks(Transform parent)
    {
        // X 백혈구 벽: 통로 높이 4, 폭 1. X 한 번에 부서짐. 땅 태그 아님(벽 옆면에 붙어 착지 판정 안 되게)
        Color xWall = new Color(0.93f, 0.58f, 0.69f);
        float[,] walls =
        {
            { 22, 0, 4 }, { 44, 0, 4 }, { 69.5f, 0, 4 },
            { 30, -17.6f, -13.6f }, { 92, -20.1f, -16.1f }, { 109, -21, -17 }, { 182, -15.6f, -11.6f },
        };
        for (int i = 0; i < walls.GetLength(0); i++)
        {
            float x = walls[i, 0];
            GameObject w = Block(parent, "[X 백혈구 벽]", x - 0.5f, x + 0.5f, walls[i, 1], walls[i, 2], xWall, 4);
            w.AddComponent<WhiteCellWall>();
        }

        // C 백혈구 바닥: 아래층 구멍을 막음. 위에서 C로 내려찍어야 부서짐
        Color cFloor = new Color(0.86f, 0.84f, 0.8f);
        foreach (float h in new[] { 18f, 49f, 78f, 172f })
        {
            GameObject f = Block(parent, "[C 백혈구 바닥]", h, h + 3, -2.1f, 0, cFloor, 4);
            MakeGround(f);
            f.AddComponent<WhiteCellFloor>();
        }

        // 항체 = 더블점프
        GameObject ab = Block(parent, "[항체 = 더블점프]", 112.6f, 113.4f, 0.1f, 0.9f, new Color(0.39f, 0.6f, 0.13f), 9);
        ab.GetComponent<BoxCollider2D>().isTrigger = true;
        ab.AddComponent<AntibodyPickup>();

        // C 튕김 항체 (1 x 1 네모, 그림은 나중에 Sprite만 교체)
        Orb(parent, "[C 튕김 항체 O1]", 124.5f, 9, 13);
        Orb(parent, "[C 튕김 항체 O2]", 115.5f, 5.5f, 13);
        Orb(parent, "[큰 C 튕김 항체]", 162, 2.5f, 27);
    }

    static void Orb(Transform parent, string name, float cx, float cy, float speed)
    {
        GameObject o = Block(parent, name, cx - 0.5f, cx + 0.5f, cy - 0.5f, cy + 0.5f, new Color(0.11f, 0.62f, 0.46f), 9);
        MakeGround(o);   // 낙하공격이 '착지'로 끝나야 튕길 수 있음
        BounceAntibody b = o.AddComponent<BounceAntibody>();
        b.bounceSpeed = speed;
        b.respawnTime = 2f;
    }

    // ---------------- 표시 (지나갈 수 있음) ----------------

    static void BuildMarkers(Transform parent)
    {
        Marker(parent, "[노란 문]", 110.1f, 110.9f, 32.5f, 36.5f, new Color(0.94f, 0.62f, 0.15f, 0.7f));
        Marker(parent, "[보스방 입구 문]", 178.3f, 178.7f, 0, 6, new Color(0.22f, 0.54f, 0.87f, 0.6f));
        Marker(parent, "[출구 문]", 196.5f, 196.9f, 0, 4, new Color(0.2f, 0.2f, 0.2f, 0.6f));

        Color cap = new Color(0.22f, 0.54f, 0.87f, 0.9f);
        Marker(parent, "[나노 캡슐 1]", 9.6f, 10.4f, 22.1f, 22.9f, cap);
        Marker(parent, "[나노 캡슐 3]", 112.1f, 112.9f, 33.1f, 33.9f, cap);
        Marker(parent, "[나노 캡슐 2]", 40.1f, 40.9f, -15.2f, -14.4f, cap);

        Color brown = new Color(0.6f, 0.24f, 0.11f, 0.9f);
        Marker(parent, "[감염핵]", 120.3f, 121.7f, -21, -19.6f, brown);
        Marker(parent, "[보스]", 191, 193, 0, 2, brown);

        Color mon = new Color(0.5f, 0.47f, 0.87f, 0.8f);
        float[,] monsters =
        {
            { 34, 0 }, { 85, 0 }, { 15, -17.6f }, { 21, -17.6f }, { 26, -17.6f }, { 37, -27.6f }, { 55, -10.1f },
            { 80, -20.1f }, { 84, -20.1f }, { 88, -20.1f }, { 116, -21 }, { 167, 0 },
            { 170, -15.6f }, { 176, -15.6f }, { 189, -22.6f }, { 193, -22.6f }, { 199, -22.6f },
        };
        for (int i = 0; i < monsters.GetLength(0); i++)
        {
            float mx = monsters[i, 0], my = monsters[i, 1];
            Marker(parent, "[몬스터]", mx - 0.4f, mx + 0.4f, my, my + 0.8f, mon);
        }
        Marker(parent, "[웨이브 소환 3곳]", 95.5f, 106, -31.6f, -31, mon);
    }

    static void Marker(Transform parent, string name, float x0, float x1, float y0, float y1, Color color)
    {
        GameObject go = Block(parent, name, x0, x1, y0, y1, color, 8);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
    }

    // ---------------- 안내 글자 ----------------

    static void BuildHints(Transform parent)
    {
        Hint(parent, "← → 이동", 1.5f, 4);
        Hint(parent, "↑ 점프", 9, 3);
        Hint(parent, "Space 대쉬", 13, 4);
        Hint(parent, "X 공격으로 백혈구 부수기", 15, 6);
        Hint(parent, "X 공격", 25, 4);
        Hint(parent, "C 특수공격 — 공중에서 내려찍기, 백혈구 바닥 부수기", 72, 4);
        Hint(parent, "저 벽은 너무 높다… 강공(C)을 써야 할 것 같다", 124, 14);
        Hint(parent, "↑ 두 번: 이단 점프", 112.5f, 3);
        Hint(parent, "더블점프 → C → 더블점프", 112.5f, 9);
        Hint(parent, "C 튕김 항체 — 점프해서 위에서 C로 내려찍으면 튕겨 오른다", 158, 6);
    }

    static void Hint(Transform parent, string text, float x, float y)
    {
        GameObject go = new GameObject("[안내] " + text);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = Vector3.one * 0.06f;
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleLeft;
        tm.color = Color.white;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = 30;
    }

    // ---------------- 플레이어·카메라 ----------------

    static void SetupPlayerAndCamera()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Undo.RecordObject(player.transform, "플레이어 위치·크기");
            player.transform.position = new Vector3(3f, 1.2f, 0f);
            float sx = Mathf.Sign(player.transform.localScale.x);
            player.transform.localScale = new Vector3(sx * PlayerScale, PlayerScale, 1f);
        }
        else
        {
            Debug.LogWarning("[새맵] 씬에서 플레이어를 찾지 못했어요.");
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "카메라 크기");
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            Undo.RecordObject(cam.transform, "카메라 위치");
            cam.transform.position = new Vector3(5f, 3f, cam.transform.position.z);
            CameraFollow follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = Undo.AddComponent<CameraFollow>(cam.gameObject);
            Undo.RecordObject(follow, "카메라 따라가기");
            follow.useBounds = false;
        }
    }

    // ---------------- 공용 ----------------

    static Transform Group(string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    static void MakeGround(GameObject go)
    {
        go.tag = "Ground";
        int layer = LayerMask.NameToLayer("Ground");
        if (layer >= 0) go.layer = layer;
    }

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
}
