using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 11-28 수정 묶음. 메뉴: Tools > 새맵 > 12 / 13. 여러 번 눌러도 같은 결과.
public static class NewMapFixes1128
{
    const string CheckpointSprite = "Assets/Resources/image/아이템/체크포인트.png";

    [MenuItem("Tools/새맵/12. 체크포인트 그림·감염핵 3번·물약 3개·O2 발판·카메라 막기")]
    static void ApplyScene()
    {
        GameObject root = GameObject.Find("새맵_회색상자");
        if (root == null) { Debug.LogWarning("[새맵] 새맵_회색상자가 없어요."); return; }
        string log = "[새맵] 12번:\n";
        log += Checkpoints(root);
        log += Heart(root);
        log += Potions(root);
        log += O2ToPlatforms(root);
        log += CameraSetup();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log(log + "→ Ctrl+S로 씬 저장");
    }

    // ① 체크포인트: 초록 네모 대신 메인씬과 같은 체크포인트 그림 (크기도 메인씬과 같게)
    static string Checkpoints(GameObject root)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CheckpointSprite);
        if (sprite == null) return "  체크포인트 그림을 못 찾음\n";
        Transform group = root.transform.Find("체크포인트");
        if (group == null) return "  체크포인트 묶음 없음\n";
        string s = "";
        foreach (Transform cp in group)
        {
            if (!cp.name.StartsWith("체크포인트")) continue;   // 시작 지점(안 보이는 부활 위치)은 그대로
            SpriteRenderer old = cp.GetComponent<SpriteRenderer>();
            if (old != null) { Undo.RecordObject(old, "체크포인트 그림"); old.enabled = false; }   // 초록 네모 끔 (지우지 않음)

            Transform art = cp.Find("체크포인트 그림");
            if (art == null)
            {
                GameObject g = new GameObject("체크포인트 그림");
                Undo.RegisterCreatedObjectUndo(g, "체크포인트 그림");
                g.transform.SetParent(cp, false);
                art = g.transform;
            }
            SpriteRenderer r = art.GetComponent<SpriteRenderer>();
            if (r == null) r = Undo.AddComponent<SpriteRenderer>(art.gameObject);
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Simple;
            r.color = Color.white;
            r.sortingOrder = old != null ? old.sortingOrder : 10;
            r.sortingLayerID = old != null ? old.sortingLayerID : 0;
            // 메인씬 크기 가로 4.94 × 세로 3.31. 그림의 받침 바닥(위에서 75%)이 땅(체크포인트 아래 끝)에 닿게
            float w = 4.935484f, h = 3.3098917f;
            Vector2 native = sprite.bounds.size;
            art.localScale = new Vector3(w / native.x, h / native.y, 1f);
            BoxCollider2D box = cp.GetComponent<BoxCollider2D>();
            float bottomLocal = box != null ? box.offset.y - box.size.y * 0.5f : -0.75f;
            art.localPosition = new Vector3(0f, bottomLocal + h * 0.252f - 0.05f, 0f);
            s += "  체크포인트 그림: " + cp.name + "\n";
        }
        return s;
    }

    // ② 감염핵: X 3번에 부서지게 (X 한 번 = 10, C 한 번 = 30), 맞을 때마다 빨갛게 반짝
    static string Heart(GameObject root)
    {
        InfectionHeart heart = root.GetComponentInChildren<InfectionHeart>(true);
        if (heart == null) return "  감염핵 없음\n";
        SerializedObject so = new SerializedObject(heart);
        SerializedProperty hp = so.FindProperty("infectionHeartHP");
        string s = "";
        if (hp != null) { s += "  감염핵 체력 " + hp.intValue + "→30\n"; hp.intValue = 30; so.ApplyModifiedProperties(); }
        if (heart.GetComponent<HitFlash>() == null) { Undo.AddComponent<HitFlash>(heart.gameObject); s += "  감염핵 맞으면 반짝 추가\n"; }
        return s;
    }

    // ③ 시작 물약(나노 캡슐) 5 → 3. 맵에 놓인 캡슐에 잘못 붙은 '사용' 기능은 끔 (F 한 번에 여러 번 회복되던 문제)
    static string Potions(GameObject root)
    {
        string s = "";
        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        NanoCapsuleUse inv = player != null ? player.GetComponent<NanoCapsuleUse>() : null;
        if (inv != null)
        {
            SerializedObject so = new SerializedObject(inv);
            so.FindProperty("MaxRemainingHealPotion").intValue = 3;
            so.FindProperty("RemainingHealPotion").intValue = 3;
            so.ApplyModifiedProperties();
            s += "  시작 물약 3개\n";
        }
        foreach (NanoCapsulePickup pick in root.GetComponentsInChildren<NanoCapsulePickup>(true))
        {
            NanoCapsuleUse extra = pick.GetComponent<NanoCapsuleUse>();
            if (extra != null && extra.enabled) { Undo.RecordObject(extra, "캡슐 사용 끔"); extra.enabled = false; s += "  " + pick.name + ": 사용 기능 끔\n"; }
        }
        return s;
    }

    // ④ 이단 점프 방(기둥 사이)의 작은 튕김 항체 O2 → 평범한 발판 두 개 (이단 점프로 편하게 올라가 오른쪽 기둥을 넘음)
    static string O2ToPlatforms(GameObject root)
    {
        string s = "";
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("[C 튕김 항체 O2]") && t.gameObject.activeSelf)
            { Undo.RecordObject(t.gameObject, "O2 끔"); t.gameObject.SetActive(false); s += "  O2 튕김 항체 끔\n"; }
            if (t.name.StartsWith("[안내] 더블점프 → C → 더블점프") && t.gameObject.activeSelf)
            { Undo.RecordObject(t.gameObject, "안내 끔"); t.gameObject.SetActive(false); s += "  안내 'C' 글자 끔\n"; }
        }

        Transform group = root.transform.Find("발판");
        if (group == null) return s + "  발판 묶음 없음\n";
        // 본보기: 세로 탑의 평범한 발판 (x 94.5~97.5, 윗면 3)
        Transform sample = null;
        foreach (Transform p in group)
        {
            if (p.name != "발판" || p.GetComponent<MovingPlatform>() != null) continue;
            BoxCollider2D b = p.GetComponent<BoxCollider2D>();
            if (b == null) continue;
            Bounds bb = Box(b);
            if (Mathf.Abs(bb.center.x - 96f) < 0.1f && Mathf.Abs(bb.max.y - 3f) < 0.1f) { sample = p; break; }
        }
        if (sample == null) return s + "  본보기 발판 못 찾음\n";
        // 간격 4.2칸 (이단 점프 최고 약 7.3칸 → 여유 있게). 맨 위(12.6)에서 이단 점프로 오른쪽 기둥(높이 16)을 넘음
        s += Place(group, sample, "발판 (이단 점프 방 아래)", 116f, 4.2f);
        s += Place(group, sample, "발판 (이단 점프 방 가운데)", 114f, 8.4f);
        s += Place(group, sample, "발판 (이단 점프 방 위)", 116f, 12.6f);
        return s;
    }

    static string Place(Transform group, Transform sample, string name, float centerX, float top)
    {
        Transform t = group.Find(name);
        if (t == null)
        {
            GameObject g = Object.Instantiate(sample.gameObject, group);
            g.name = name;
            Undo.RegisterCreatedObjectUndo(g, name);
            t = g.transform;
        }
        // 이름에 '발판'이 들어가서 C 내려찍기 때 통과됨 (DivePassThrough)
        Bounds b = Box(sample.GetComponent<BoxCollider2D>());
        Vector3 delta = new Vector3(centerX - b.center.x, top - b.max.y, 0f);
        Undo.RecordObject(t, name);
        t.position = sample.position + delta;
        return "  " + name + " 놓음 (가운데 x " + centerX + ", 윗면 " + top + ")\n";
    }

    static Bounds Box(BoxCollider2D c)
    {
        Vector3 center = c.transform.TransformPoint(c.offset);
        Vector3 size = Vector3.Scale(c.size, c.transform.lossyScale);
        return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f));
    }

    // ⑤ 카메라: 맵 바깥이 안 보이게 + 이단 점프 방 기둥(x 110.5~112) 너머가 안 보이게
    static string CameraSetup()
    {
        Camera cam = UnityEngine.Camera.main;
        if (cam == null) return "  카메라 없음\n";
        string s = "";
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            Undo.RecordObject(follow, "카메라 범위");
            follow.useBounds = true;
            follow.boundsMin = new Vector2(-2.5f, -34f);
            follow.boundsMax = new Vector2(203.5f, 46f);
            s += "  카메라: 맵 바깥(x -2.5~203.5, y -34~46) 안 보이게\n";
        }
        CameraZoneClamp clamp = cam.GetComponent<CameraZoneClamp>();
        if (clamp == null) clamp = Undo.AddComponent<CameraZoneClamp>(cam.gameObject);
        Undo.RecordObject(clamp, "카메라 구역");
        clamp.zones = new[]
        {
            new CameraZoneClamp.Zone
            {
                name = "기둥 왼쪽 (세로 발판 탑) → 오른쪽 기둥 너머 안 보이게",
                playerArea = Rect.MinMaxRect(92f, -1f, 110.5f, 31.5f),
                clampRight = true, maxViewX = 112f,
            },
            new CameraZoneClamp.Zone
            {
                name = "기둥 오른쪽 (이단 점프 방·혈류 점프맵) → 왼쪽 기둥 너머 안 보이게",
                playerArea = Rect.MinMaxRect(112f, -3f, 156.3f, 20f),
                clampLeft = true, minViewX = 110.5f,
            },
        };
        s += "  카메라 구역 2개 (기둥 왼쪽/오른쪽)\n";
        return s;
    }

    // ─────────────────────────────────────────────
    // 13. 몬스터 프리팹: 위쪽으로도 더 멀리 알아챔 + 간염이 위쪽 플레이어에게 뛰어올라 공격
    static readonly string[] MonsterPrefabs =
    {
        "Assets/Prefabs/Enemy/JabMob/DengueMonster.prefab",
        "Assets/Prefabs/Enemy/JabMob/HepatitisMonster.prefab",
    };

    [MenuItem("Tools/새맵/몬스터/13. 위쪽 인식 넓히기 + 간염 위로 뛰어 공격 (프리팹)")]
    static void ApplyMonsters()
    {
        foreach (string path in MonsterPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string log = "[새맵] " + root.name + ":";
                EnemyBase e = root.GetComponent<EnemyBase>();
                SerializedProperty sensorProp = e != null ? new SerializedObject(e).FindProperty("recognitionSensor") : null;
                EnemySensor sensor = sensorProp != null ? sensorProp.objectReferenceValue as EnemySensor : null;
                CircleCollider2D circle = sensor != null ? sensor.GetComponent<CircleCollider2D>() : null;
                if (circle != null)
                {
                    // 원(반지름 8) → 세로로 긴 캡슐: 옆 8, 아래 8, 위 14
                    float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
                    CapsuleCollider2D cap = sensor.GetComponent<CapsuleCollider2D>();
                    if (cap == null) cap = sensor.gameObject.AddComponent<CapsuleCollider2D>();
                    cap.isTrigger = true;
                    cap.direction = CapsuleDirection2D.Vertical;
                    cap.size = new Vector2(16f, 22f) / scale;
                    cap.offset = new Vector2(circle.offset.x, 3f / scale);
                    cap.sharedMaterial = circle.sharedMaterial;
                    circle.enabled = false;   // 지우지 않고 끔
                    log += " 인식 범위 옆 8·아래 8·위 14";
                }
                else log += " (인식 센서 원 콜라이더 못 찾음)";

                if (root.GetComponent<JabMob2>() != null && root.GetComponent<UpwardLeapAttack>() == null)
                {
                    root.AddComponent<UpwardLeapAttack>();
                    log += " + 위로 뛰어 공격";
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log(log);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
