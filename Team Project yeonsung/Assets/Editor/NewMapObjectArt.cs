using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 오브젝트 그림 넣기 (메인씬 테스트). 메뉴: Tools > 새맵 > 10. 오브젝트 그림 넣기 (발판·백혈구·감염핵)
// 팀원 원본 그림(Resources/image/맵/중경(오브젝트))은 건드리지 않고, 잘라서 복사해 둔 Assets/Map/NewMapArt/오브젝트 그림을 씀.
// - 발판 = 적혈구, 움직이는 혈소판 = 적혈구(감염된 것은 보라), 부서지는 발판 = 적혈구(주황빛)
// - C로 깨는 백혈구 바닥 = 부서지는바닥, X로 깨는 백혈구 벽 = 백혈구(세로로 세움)
//   → 각 물건 자기 그림만 바꿈(그림 크기만 따로 정함). 충돌 범위는 그대로. 원반 윗면 = 밟는 면
// - 감염핵: 천장부터 바닥까지 닿게 키우고, 바닥·천장에 보라 감염 얼룩 (감염핵을 부수면 얼룩도 같이 사라짐)
// 여러 번 눌러도 됨.
public static class NewMapObjectArt
{
    const string Dir = "Assets/Map/NewMapArt/오브젝트/";
    const float PPU = 100f;

    // 적혈구 자른 그림 1090 × 327, 밟는 면(원반 위 테두리) = 위에서 14px
    const float RbcW = 1090f, RbcH = 327f, RbcStand = 14f;
    // 부서지는바닥 자른 그림 1952 × 1010, 몸통 윗면 = 위에서 217px (방울은 그 위로 튀어나옴)
    const float FloorW = 1952f, FloorH = 1010f, FloorStand = 217f;
    // 백혈구 세로 636 × 1594

    static readonly List<string> report = new List<string>();

    [MenuItem("Tools/새맵/10. 오브젝트 그림 넣기 (발판·백혈구·감염핵)")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != "메인씬 테스트")
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끄고 '메인씬 테스트' 씬에서 실행해 주세요.", "확인");
            return;
        }
        GameObject root = GameObject.Find("새맵_회색상자");
        if (root == null) return;
        report.Clear();

        // 그림 설정: 밟는 면이 오브젝트 충돌 윗면에 오도록 기준점(pivot)을 정함
        // (판 두께 절반 = 0.3(발판)/0.35(혈소판), 바닥 두께 절반 = 1.05 기준)
        Setup("적혈구_발판.png", RbcPivot(3.2f, 0.3f));
        Setup("적혈구_부서지는발판.png", RbcPivot(3.2f, 0.3f));
        Setup("적혈구_혈소판.png", RbcPivot(3.7f, 0.35f));
        Setup("부서지는바닥.png", FloorPivot(3.6f, 1.05f));
        Setup("백혈구_세로.png", new Vector2(0.5f, 0.5f));
        Setup("감염 얼룩.png", new Vector2(0.5f, 0.5f));
        Sprite rbcPlat = Load("적혈구_발판.png"), rbcBreak = Load("적혈구_부서지는발판.png"), rbcMove = Load("적혈구_혈소판.png");
        Sprite brokenFloor = Load("부서지는바닥.png"), wbc = Load("백혈구_세로.png"), stain = Load("감염 얼룩.png");
        if (rbcPlat == null || brokenFloor == null || wbc == null || stain == null) { Debug.LogWarning("[새맵] 오브젝트 그림을 못 찾았어요: " + Dir); return; }

        Undo.SetCurrentGroupName("새맵 오브젝트 그림");
        int group = Undo.GetCurrentGroup();
        int plats = 0, moving = 0, breaks = 0, floors = 0, walls = 0;

        foreach (BoxCollider2D c in root.GetComponentsInChildren<BoxCollider2D>(true))
        {
            SpriteRenderer r = c.GetComponent<SpriteRenderer>();
            if (r == null || c.isTrigger) continue;
            GameObject g = c.gameObject;
            if (g.GetComponent<WhiteCellFloor>() != null)
            {
                SetArt(r, brokenFloor, c.size.x + 0.6f, (c.size.x + 0.6f) * FloorH / FloorW, Color.white); floors++;
            }
            else if (g.GetComponent<WhiteCellWall>() != null)
            {
                SetArt(r, wbc, 1.6f, c.size.y + 0.6f, Color.white); walls++;
            }
            else if (g.GetComponent<MovingPlatform>() != null)
            {
                float w = c.size.x + 0.2f;
                bool infected = g.name.Contains("감염");
                SetArt(r, rbcMove, w, w * RbcH / RbcW, infected ? new Color(0.78f, 0.55f, 1f) : Color.white); moving++;
            }
            else if (g.GetComponent<BreakablePlatform>() != null)
            {
                float w = c.size.x + 0.2f;
                SetArt(r, rbcBreak, w, w * RbcH / RbcW, new Color(1f, 0.72f, 0.5f)); breaks++;
            }
            else if (g.name.Contains("발판") && g.GetComponent<BounceAntibody>() == null && c.size.y < 1f)
            {
                float w = c.size.x + 0.2f;
                SetArt(r, rbcPlat, w, w * RbcH / RbcW, Color.white); plats++;
            }
        }
        report.Add("발판 " + plats + ", 혈소판 " + moving + ", 부서지는 발판 " + breaks + ", C 백혈구 바닥 " + floors + ", X 백혈구 벽 " + walls);

        FixHeart(root.transform, stain);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 오브젝트 그림 넣기. Ctrl+S로 저장해 주세요.\n- " + string.Join("\n- ", report));
    }

    // 그림 크기 Wv일 때, 밟는 면이 오브젝트 중심 + half 에 오도록 하는 기준점
    static Vector2 RbcPivot(float wv, float half)
    {
        float hv = wv * RbcH / RbcW, stand = RbcStand / RbcH * hv;
        return new Vector2(0.5f, (hv - (half + stand)) / hv);
    }
    static Vector2 FloorPivot(float wv, float half)
    {
        float hv = wv * FloorH / FloorW, stand = FloorStand / FloorH * hv;
        return new Vector2(0.5f, (hv - (half + stand)) / hv);
    }

    static void SetArt(SpriteRenderer r, Sprite s, float w, float h, Color color)
    {
        Undo.RecordObject(r, "그림");
        r.sprite = s;
        r.drawMode = SpriteDrawMode.Sliced;   // 테두리 없는 슬라이스 = 그림 전체를 size 크기로 늘림 (오브젝트 크기·충돌은 그대로)
        r.size = new Vector2(w, h);
        r.color = color;
    }

    // ---------------- 감염핵 ----------------

    static void FixHeart(Transform root, Sprite stain)
    {
        InfectionHeart heart = null;
        foreach (InfectionHeart h in root.GetComponentsInChildren<InfectionHeart>(true)) { heart = h; break; }
        if (heart == null) { report.Add("감염핵을 못 찾음"); return; }
        SpriteRenderer hr = heart.GetComponent<SpriteRenderer>();
        CircleCollider2D cc = heart.GetComponent<CircleCollider2D>();
        if (hr == null || hr.sprite == null) return;

        Physics2D.SyncTransforms();
        Vector2 at = heart.transform.position;
        float? floor = Cast(at, Vector2.down, heart), ceil = Cast(at, Vector2.up, heart);
        if (floor == null || ceil == null) { report.Add("감염핵 방의 바닥·천장을 못 찾음"); return; }
        float? leftWall = Cast(new Vector2(at.x, floor.Value + 1f), Vector2.left, heart, true);
        float? rightWall = Cast(new Vector2(at.x, floor.Value + 1f), Vector2.right, heart, true);

        // 크기: 천장~바닥 + 0.4 (위 끈적이는 부분은 천장에, 아래는 바닥에 살짝 묻히게)
        Bounds sb = hr.sprite.bounds;   // 크기 1일 때
        float targetH = ceil.Value - floor.Value + 0.4f;
        float s = targetH / sb.size.y;
        float halfW = sb.size.x * s * 0.5f;
        float cx = at.x;
        if (rightWall != null) cx = Mathf.Min(cx, rightWall.Value - halfW + 0.6f);
        if (leftWall != null) cx = Mathf.Max(cx, leftWall.Value + halfW - 0.6f);
        float py = floor.Value - 0.2f - sb.min.y * s;

        Undo.RecordObject(heart.transform, "감염핵 크기");
        float sx = Mathf.Sign(heart.transform.localScale.x);
        heart.transform.localScale = new Vector3(sx * s, s, 1f);
        heart.transform.position = new Vector3(cx, py, heart.transform.position.z);

        // 맞는 범위: 몸통 아래쪽 (바닥에서 반지름 2.6 원) — 플레이어가 밑으로 지나가지 않게
        if (cc != null)
        {
            Undo.RecordObject(cc, "감염핵 맞는 범위");
            float rw = 2.6f;
            cc.radius = rw / s;
            cc.offset = new Vector2(0f, (floor.Value + rw - py) / s);
        }

        // 감염 얼룩 (감염핵 자식 → 부수면 같이 사라짐). 바닥 3개, 천장 2개
        Transform old = heart.transform.Find("감염 얼룩");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject sg = new GameObject("감염 얼룩");
        Undo.RegisterCreatedObjectUndo(sg, "감염 얼룩");
        sg.transform.SetParent(heart.transform, false);
        float minX = (leftWall ?? cx - 8f) + 0.3f, maxX = (rightWall ?? cx + 8f) - 0.3f;
        float[,] spots =
        {
            // x(감염핵 기준), 폭, 바닥(0)/천장(1)
            { -2.8f, 4.6f, 0 }, { 2.2f, 3.2f, 0 }, { -5.6f, 2.4f, 0 },
            { -1.6f, 4.2f, 1 }, { 2.6f, 2.6f, 1 },
        };
        int hrOrder = hr.sortingOrder;
        for (int i = 0; i < spots.GetLength(0); i++)
        {
            float w = spots[i, 1];
            float x = Mathf.Clamp(cx + spots[i, 0], minX + w * 0.5f, maxX - w * 0.5f);
            bool onCeil = spots[i, 2] > 0.5f;
            float y = onCeil ? ceil.Value - 0.05f : floor.Value + 0.05f;
            GameObject st = new GameObject(onCeil ? "천장 얼룩" : "바닥 얼룩");
            st.transform.SetParent(sg.transform, false);
            st.transform.position = new Vector3(x, y, 0f);
            float sw = w / (stain.bounds.size.x * s), shh = 0.5f / (stain.bounds.size.y * s);
            st.transform.localScale = new Vector3(sw, onCeil ? -shh : shh, 1f);
            SpriteRenderer r = st.AddComponent<SpriteRenderer>();
            r.sprite = stain;
            r.sortingOrder = hrOrder - 1;   // 감염핵 뒤, 땅 그림 앞
        }
        report.Add("감염핵: 천장(" + ceil.Value.ToString("0.#") + ")~바닥(" + floor.Value.ToString("0.#") + ") 높이로 키움 (크기 " + s.ToString("0.###") + "), 바닥·천장 감염 얼룩 5개");
    }

    // 감염핵 위치에서 dir 방향으로 처음 만나는 땅 (감염핵 자신·트리거·플레이어·몬스터 무시)
    static float? Cast(Vector2 from, Vector2 dir, InfectionHeart heart, bool horizontal = false)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(from, dir, 30f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit2D h in hits)
        {
            Collider2D c = h.collider;
            if (c == null || c.isTrigger || h.distance <= 0f) continue;
            if (c.GetComponentInParent<InfectionHeart>() != null || c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<EnemyBase>() != null) continue;
            if (!c.CompareTag("Ground")) continue;
            return horizontal ? h.point.x : h.point.y;
        }
        return null;
    }

    static Sprite Load(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name); }

    // 메인씬과 같은 원경(뒷배경)을 켬. 테스트 씬에는 메인씬에서 복사된 '원경_배경'이 이미 있는데
    // 예전 맵을 끌 때 같이 꺼져 있었음 → 그 묶음만 다시 켬 (예전 맵의 다른 것은 그대로 꺼 둠)
    // 원경은 카메라를 따라 끝없이 반복되므로(ParallaxTile) 새 맵 어디서나 보임. 그리는 순서 -1000·-1001 (땅 그림은 그 앞)
    [MenuItem("Tools/새맵/11. 메인씬 원경(뒷배경) 켜기")]
    public static void EnableBackground()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != "메인씬 테스트") return;
        int n = 0;
        foreach (ParallaxTile p in Object.FindObjectsByType<ParallaxTile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            for (Transform t = p.transform; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                if (t.GetComponentsInChildren<PlayerController>(true).Length > 0) break;   // 안전: 플레이어가 든 묶음은 건드리지 않음
                Undo.RecordObject(t.gameObject, "원경 켜기");
                t.gameObject.SetActive(true);
                Debug.Log("[새맵] 원경 켬: " + t.name);
                n++;
            }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 원경(뒷배경) 켜기: " + n + "개 켬. Ctrl+S로 저장해 주세요.");
    }

    static void Setup(string name, Vector2 pivot)
    {
        TextureImporter ti = AssetImporter.GetAtPath(Dir + name) as TextureImporter;
        if (ti == null) return;
        TextureImporterSettings st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        if (ti.textureType == TextureImporterType.Sprite && st.spriteAlignment == (int)SpriteAlignment.Custom
            && Vector2.Distance(st.spritePivot, pivot) < 0.0005f && Mathf.Abs(st.spritePixelsPerUnit - PPU) < 0.01f
            && st.spriteMeshType == SpriteMeshType.FullRect) return;
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
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
    }
}
