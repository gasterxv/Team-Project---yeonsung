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
