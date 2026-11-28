using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 작업 도우미용: 프로젝트 Temp/newmap_cmd.txt 에 명령을 적으면 에디터가 읽고 실행한 뒤 파일을 지움.
// (메뉴 순서가 컴파일마다 바뀌어 클릭이 어긋나는 문제를 피하려고 만듦. 게임에는 영향 없음)
// 명령 (한 줄에 하나):
//   frame xMin yMin xMax yMax   → Scene 화면을 그 영역으로 (적은 크기 그대로 보이게 맞춤)
//   menu Tools/새맵/9. ...       → 메뉴 실행
//   save                         → 열린 씬 저장
[InitializeOnLoad]
public static class NewMapCommandWatcher
{
    static double next;

    static NewMapCommandWatcher() { EditorApplication.update += Tick; }

    static string FilePath { get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "newmap_cmd.txt"); } }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.5;
        string f = FilePath;
        if (!File.Exists(f)) return;
        string[] lines;
        try { lines = File.ReadAllLines(f); File.Delete(f); } catch { return; }
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line == "") continue;
            try
            {
                if (line.StartsWith("frame "))
                {
                    string[] p = line.Substring(6).Split(' ');
                    float x0 = float.Parse(p[0]), y0 = float.Parse(p[1]), x1 = float.Parse(p[2]), y1 = float.Parse(p[3]);
                    SceneView sv = SceneView.lastActiveSceneView;
                    if (sv == null) continue;
                    sv.in2DMode = true;
                    sv.orthographic = true;
                    Vector3 c = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
                    float aspect = sv.position.width / Mathf.Max(1f, sv.position.height);
                    float half = Mathf.Max((y1 - y0) * 0.5f, (x1 - x0) * 0.5f / aspect);
                    sv.LookAt(c, Quaternion.identity, half * Mathf.Sqrt(aspect * aspect + 1f), true, true);
                    sv.Repaint();
                }
                else if (line.StartsWith("menu ")) EditorApplication.ExecuteMenuItem(line.Substring(5));
                else if (line == "save") EditorSceneManager.SaveOpenScenes();
                else if (line.StartsWith("pick "))
                {
                    // 그 점을 덮는 그림 조각들 (그리는 순서와 함께)
                    string[] p = line.Substring(5).Split(' ');
                    Vector3 pt = new Vector3(float.Parse(p[0]), float.Parse(p[1]), 0f);
                    string s = "[새맵 명령] pick " + pt + ":";
                    foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    {
                        Bounds b = r.bounds; b.extents = new Vector3(b.extents.x, b.extents.y, 1000f);
                        if (!r.enabled || !b.Contains(pt)) continue;
                        Transform t = r.transform;
                        s += "\n  " + (t.parent != null ? t.parent.name + "/" : "") + t.name + " 층 " + r.sortingLayerName + " 순서 " + r.sortingOrder + " 색 " + r.color + " 위치 " + b.min.ToString("0.##") + "~" + b.max.ToString("0.##") + " 마스크 " + r.maskInteraction + " 크기 " + b.size.ToString("0.##");
                    }
                    Debug.Log(s);
                    continue;
                }
                Debug.Log("[새맵 명령] " + line);
            }
            catch (System.Exception e) { Debug.LogWarning("[새맵 명령] 실패: " + line + " / " + e.Message); }
        }
    }
}
