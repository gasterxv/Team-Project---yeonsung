using System.Text;
using UnityEditor;
using UnityEngine;

// 진단용: 지금 열린 씬(저장 안 한 변경 포함)에서 웨이브 방 주변 오브젝트의 위치·크기를 Console/로그에 적습니다. 아무것도 바꾸지 않음.
public static class NewMapDiagnostics
{
    // 그림(SpriteRenderer) 크기와 충돌(BoxCollider2D) 크기가 다른 블록 찾기 → "땅 속으로 들어가 보이는" 원인
    [MenuItem("Tools/새맵/진단: 그림과 충돌 크기가 다른 블록 적기")]
    static void DumpMismatch()
    {
        GameObject root = GameObject.Find("새맵_회색상자");
        if (root == null) { Debug.LogWarning("[새맵 진단] 새맵_회색상자가 없어요."); return; }
        StringBuilder sb = new StringBuilder("[새맵 진단] 그림≠충돌 블록\n");
        int n = 0;
        foreach (BoxCollider2D c in root.GetComponentsInChildren<BoxCollider2D>(true))
        {
            SpriteRenderer s = c.GetComponent<SpriteRenderer>();
            if (s == null || c.isTrigger) continue;
            Bounds cb = c.bounds, sbnd = s.bounds;
            if (!c.enabled || !c.gameObject.activeInHierarchy) { cb = ComputeColliderBounds(c); }
            float d = Mathf.Max(Mathf.Abs(cb.min.x - sbnd.min.x), Mathf.Abs(cb.max.x - sbnd.max.x),
                                Mathf.Abs(cb.min.y - sbnd.min.y), Mathf.Abs(cb.max.y - sbnd.max.y));
            if (d < 0.05f) continue;
            Transform t = c.transform;
            sb.AppendFormat("{0} ({1}) | 그림 x {2:0.##}~{3:0.##} y {4:0.##}~{5:0.##} | 충돌 x {6:0.##}~{7:0.##} y {8:0.##}~{9:0.##} | 크기조절 {10} | 그림모드 {11} size {12} | 콜라이더 size {13} offset {14}\n",
                t.name, t.parent != null ? t.parent.name : "-",
                sbnd.min.x, sbnd.max.x, sbnd.min.y, sbnd.max.y, cb.min.x, cb.max.x, cb.min.y, cb.max.y,
                t.lossyScale.ToString("0.###"), s.drawMode, s.size.ToString("0.##"), c.size.ToString("0.##"), c.offset.ToString("0.##"));
            n++;
        }
        sb.Append("총 " + n + "개");
        Debug.Log(sb.ToString());
    }

    static Bounds ComputeColliderBounds(BoxCollider2D c)
    {
        Vector3 center = c.transform.TransformPoint(c.offset);
        Vector3 size = Vector3.Scale(c.size, c.transform.lossyScale);
        return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f));
    }

    // 몬스터 프리팹의 콜라이더·센서·이동/점프 값을 적습니다 (읽기만 함)
    [MenuItem("Tools/새맵/진단: 몬스터 프리팹 인식 범위 적기")]
    static void DumpMonsters()
    {
        string[] paths =
        {
            "Assets/Prefabs/Enemy/JabMob/DengueMonster.prefab",
            "Assets/Prefabs/Enemy/JabMob/HepatitisMonster.prefab",
        };
        StringBuilder sb = new StringBuilder("[새맵 진단] 몬스터 프리팹\n");
        foreach (string p in paths)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) { sb.Append(p + " 없음\n"); continue; }
            EnemyBase e = go.GetComponent<EnemyBase>();
            sb.Append("== " + go.name + " (" + (e != null ? e.GetType().Name : "?") + ")\n");
            if (e != null)
            {
                SerializedObject so = new SerializedObject(e);
                foreach (string f in new[] { "maxChaseDistance", "chaseSpeed", "MonsterWalkSpeed", "patrolRadius",
                                             "attackHorizontalSpeed", "attackJumpSpeed", "attackStartGap", "recognitionSensor", "attackRangeSensor" })
                {
                    SerializedProperty sp = so.FindProperty(f);
                    if (sp == null) continue;
                    string v = sp.propertyType == SerializedPropertyType.ObjectReference
                        ? (sp.objectReferenceValue != null ? sp.objectReferenceValue.name + " (" + Path(((Component)sp.objectReferenceValue).transform, go.transform) + ")" : "없음")
                        : sp.propertyType == SerializedPropertyType.Float ? sp.floatValue.ToString("0.##") : sp.intValue.ToString();
                    sb.Append("   " + f + " = " + v + "\n");
                }
            }
            foreach (Collider2D c in go.GetComponentsInChildren<Collider2D>(true))
            {
                EnemySensor s = c.GetComponent<EnemySensor>();
                string size = c is BoxCollider2D b ? b.size.ToString("0.##") : c is CapsuleCollider2D cc ? cc.size.ToString("0.##") : c is CircleCollider2D ci ? "r " + ci.radius.ToString("0.##") : "?";
                sb.AppendFormat("   콜라이더 {0} [{1}] 트리거={2} 크기={3} offset={4} 크기조절={5} 센서={6}\n",
                    Path(c.transform, go.transform), c.GetType().Name, c.isTrigger, size, c.offset.ToString("0.##"),
                    c.transform.lossyScale.ToString("0.##"), s != null ? new SerializedObject(s).FindProperty("sensorType").enumNames[new SerializedObject(s).FindProperty("sensorType").enumValueIndex] : "-");
            }
        }
        Debug.Log(sb.ToString());
    }

    static string Path(Transform t, Transform root)
    {
        string p = t.name;
        while (t.parent != null && t != root) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    [MenuItem("Tools/새맵/진단: 웨이브 방 주변 오브젝트 적기")]
    static void DumpWaveArea() { DumpArea("웨이브 방 주변", Rect.MinMaxRect(86f, -34f, 128f, -8f)); }

    [MenuItem("Tools/새맵/진단: 노란 문 주변 오브젝트 적기")]
    static void DumpDoorArea() { DumpArea("노란 문 주변", Rect.MinMaxRect(104f, 28f, 116f, 44f)); }

    static void DumpArea(string label, Rect area)
    {
        StringBuilder sb = new StringBuilder("[새맵 진단] " + label + " (x " + area.xMin + "~" + area.xMax + ", y " + area.yMin + "~" + area.yMax + ")\n");
        int n = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Bounds b;
            Collider2D c = t.GetComponent<Collider2D>();
            SpriteRenderer s = t.GetComponent<SpriteRenderer>();
            if (c != null) b = c.bounds;
            else if (s != null) b = s.bounds;
            else continue;
            if (b.max.x < area.xMin || b.min.x > area.xMax || b.max.y < area.yMin || b.min.y > area.yMax) continue;
            if (c != null && (!c.enabled || !c.gameObject.activeInHierarchy) && c is BoxCollider2D bc) b = ComputeColliderBounds(bc);
            string spr = s != null ? string.Format(" | 그림 x {0:0.##}~{1:0.##} y {2:0.##}~{3:0.##}", s.bounds.min.x, s.bounds.max.x, s.bounds.min.y, s.bounds.max.y) : "";
            sb.AppendFormat("{0} | x {1:0.##}~{2:0.##} y {3:0.##}~{4:0.##} | 켜짐={5} 부모={6} 콜라이더={7}{8}\n",
                t.name, b.min.x, b.max.x, b.min.y, b.max.y, t.gameObject.activeInHierarchy,
                t.parent != null ? t.parent.name : "-", c != null ? (c.isTrigger ? "트리거" : "있음") : "없음", spr);
            n++;
        }
        sb.Append("총 " + n + "개");
        Debug.Log(sb.ToString());
    }
}
