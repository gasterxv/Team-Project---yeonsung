using UnityEditor;
using UnityEngine;

// 몬스터 프리팹 값 조정 (프리팹 자체를 바꾸므로 모든 씬에 적용됨).
// 메뉴: Tools > 새맵 > 몬스터 > 인식 거리 8로 맞추기
// 몇 번 눌러도 같은 결과 (곱하기가 아니라 목표 값으로 맞춤).
public static class NewMapMonsterTuning
{
    const float RecognitionRadius = 8f;   // 플레이어를 알아채는 거리 (월드 단위, 한 화면 가로 19.2)

    static readonly string[] Prefabs =
    {
        "Assets/Prefabs/Enemy/JabMob/DengueMonster.prefab",
        "Assets/Prefabs/Enemy/JabMob/HepatitisMonster.prefab",
    };

    // 간염(고기말이) 몸통박치기 점프: 약 3칸 → 약 6칸, 붙어야(0.4) 뛰던 것을 4칸 떨어져서부터 뛰게.
    // 비행시간 = 2 × 점프힘 / 중력(9.81) ≈ 1.12초, 거리 = 옆속도 × 비행시간 ≈ 6.2칸, 최고 높이 ≈ 1.5칸
    [MenuItem("Tools/새맵/몬스터/간염 점프 거리 늘리기 (프리팹)")]
    static void SetHepatitisJump()
    {
        const string path = "Assets/Prefabs/Enemy/JabMob/HepatitisMonster.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyBase e = root.GetComponent<EnemyBase>();
            if (e == null) { Debug.LogWarning("[새맵] 간염 프리팹에서 몬스터 스크립트를 못 찾았어요."); return; }
            SerializedObject so = new SerializedObject(e);
            string log = "";
            log += Set(so, "attackHorizontalSpeed", 5.5f);
            log += Set(so, "attackJumpSpeed", 5.5f);
            log += Set(so, "maxAttackAirTime", 1.6f);
            log += Set(so, "attackStartGap", 4f);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[새맵] 간염 점프:" + log);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string Set(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return " (" + name + " 없음)";
        string s = " " + name + " " + p.floatValue.ToString("0.##") + "→" + value.ToString("0.##");
        p.floatValue = value;
        return s;
    }

    [MenuItem("Tools/새맵/몬스터/인식 거리 8로 맞추기 (프리팹)")]
    static void SetRecognition()
    {
        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EnemyBase e = root.GetComponent<EnemyBase>();
                SerializedProperty sensorProp = e != null ? new SerializedObject(e).FindProperty("recognitionSensor") : null;
                EnemySensor sensor = sensorProp != null ? sensorProp.objectReferenceValue as EnemySensor : null;
                CircleCollider2D circle = sensor != null ? sensor.GetComponent<CircleCollider2D>() : null;
                if (circle == null) { Debug.LogWarning("[새맵] " + path + ": 인식 센서(원 콜라이더)를 못 찾았어요."); continue; }

                float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
                float before = circle.radius * scale;
                circle.radius = RecognitionRadius / Mathf.Max(0.0001f, scale);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[새맵] " + root.name + " 인식 거리 " + before.ToString("0.##") + " → " + RecognitionRadius);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
