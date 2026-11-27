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

    [MenuItem("Tools/새맵/진단: 웨이브 방 주변 오브젝트 적기")]
    static void DumpWaveArea()
    {
        Rect area = Rect.MinMaxRect(86f, -34f, 128f, -8f);
        StringBuilder sb = new StringBuilder("[새맵 진단] 웨이브 방 주변 (x 86~128, y -34~-8)\n");
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
            sb.AppendFormat("{0} | x {1:0.##}~{2:0.##} y {3:0.##}~{4:0.##} | 켜짐={5} 부모={6} 콜라이더={7}\n",
                t.name, b.min.x, b.max.x, b.min.y, b.max.y, t.gameObject.activeInHierarchy,
                t.parent != null ? t.parent.name : "-", c != null ? (c.isTrigger ? "트리거" : "있음") : "없음");
            n++;
        }
        sb.Append("총 " + n + "개");
        Debug.Log(sb.ToString());
    }
}
