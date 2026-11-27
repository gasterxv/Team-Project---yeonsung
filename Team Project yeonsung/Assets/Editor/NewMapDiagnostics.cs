using System.Text;
using UnityEditor;
using UnityEngine;

// 진단용: 지금 열린 씬(저장 안 한 변경 포함)에서 웨이브 방 주변 오브젝트의 위치·크기를 Console/로그에 적습니다. 아무것도 바꾸지 않음.
public static class NewMapDiagnostics
{
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
