using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// '메인씬 테스트'에 이미 있는 새맵_회색상자를 그대로 두고(위치·크기·팀원이 고친 것 유지),
// 표시만 해 둔 기믹 오브젝트에 실제 동작 스크립트를 붙입니다.
// 메뉴: Tools > 새맵 > 2. 지금 씬에 기믹 연결 (위치 유지)
//
// '1. 회색 상자 맵 만들기'는 묶음을 지우고 새로 만들어서 직접 고친 것이 사라지므로, 이미 만든 씬에는 이 메뉴를 씁니다.
// 여러 번 실행해도 됩니다. (이미 붙어 있는 건 건너뜀, 지우는 것 없음)
public static class NewMapGimmickLinker
{
    const string SceneName = "메인씬 테스트";
    const string RootName = "새맵_회색상자";
    const string RulesGroupName = "규칙 (초기화·더블점프)";

    static readonly List<string> report = new List<string>();

    [MenuItem("Tools/새맵/2. 지금 씬에 기믹 연결 (위치 유지)")]
    public static void Link()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("새맵", "Play를 끈 상태에서 실행해 주세요.", "확인");
            return;
        }
        if (EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("새맵", "'" + SceneName + "' 씬을 먼저 열어 주세요.", "확인");
            return;
        }
        GameObject rootGo = GameObject.Find(RootName);
        if (rootGo == null)
        {
            EditorUtility.DisplayDialog("새맵", "씬에 '" + RootName + "' 묶음이 없어요.", "확인");
            return;
        }

        report.Clear();
        Undo.SetCurrentGroupName("새맵 기믹 연결");
        int group = Undo.GetCurrentGroup();

        Transform root = rootGo.transform;
        EnsureRules(root);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            string n = go.name;
            if (n.StartsWith("[안내]")) { UndoHintMistake(go); continue; }   // 안내 글자는 기믹이 아님
            if (n.StartsWith("[X 백혈구 벽]")) LinkWhiteCellWall(go);
            else if (n.StartsWith("[C 백혈구 바닥]")) LinkWhiteCellFloor(go);
            else if (n.StartsWith("[항체 = 더블점프]")) LinkAntibody(go);
            else if (n.Contains("C 튕김 항체") && n.StartsWith("[")) LinkBounce(go);
            else if (n == "부서지는 발판" || n.StartsWith("[보스방] 부서지는 발판")) LinkBreakable(go);

            if (go.GetComponent<CheckPoint>() != null && go.GetComponent<CheckpointSaveTrigger>() == null)
            {
                Undo.AddComponent<CheckpointSaveTrigger>(go);
                report.Add("체크포인트 저장 연결: " + n);
            }
        }

        TurnOffStrayOldMap();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[새맵] 기믹 연결 완료 (" + report.Count + "건). Ctrl+S로 저장해 주세요.\n- " + string.Join("\n- ", report));
    }

    // ---------------- 규칙 (초기화·더블점프) ----------------

    static void EnsureRules(Transform root)
    {
        if (Object.FindFirstObjectByType<CheckpointResetManager>(FindObjectsInactive.Include) != null
            && Object.FindFirstObjectByType<DoubleJumpAbility>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        Transform groupT = root.Find(RulesGroupName);
        if (groupT == null)
        {
            groupT = new GameObject(RulesGroupName).transform;
            Undo.RegisterCreatedObjectUndo(groupT.gameObject, "규칙 묶음");
            Undo.SetTransformParent(groupT, root, "규칙 묶음");
            groupT.localPosition = Vector3.zero;
        }
        GameObject rules = new GameObject("규칙");
        Undo.RegisterCreatedObjectUndo(rules, "규칙");
        Undo.SetTransformParent(rules.transform, groupT, "규칙");
        rules.transform.localPosition = Vector3.zero;

        if (Object.FindFirstObjectByType<CheckpointResetManager>(FindObjectsInactive.Include) == null)
            Undo.AddComponent<CheckpointResetManager>(rules);
        if (Object.FindFirstObjectByType<DoubleJumpAbility>(FindObjectsInactive.Include) == null)
            Undo.AddComponent<DoubleJumpAbility>(rules);
        report.Add("규칙 오브젝트 추가 (사망 초기화 + 더블점프)");
    }

    // ---------------- 기믹 ----------------

    static void LinkWhiteCellWall(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "X벽");
        col.isTrigger = false;   // 몸으로는 못 지나감
        if (go.GetComponent<WhiteCellWall>() == null)
        {
            Undo.AddComponent<WhiteCellWall>(go);
            report.Add("X 백혈구 벽 동작: " + go.name);
        }
    }

    static void LinkWhiteCellFloor(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "C바닥");
        col.isTrigger = false;
        MakeGround(go);
        if (go.GetComponent<WhiteCellFloor>() == null)
        {
            Undo.AddComponent<WhiteCellFloor>(go);
            report.Add("C 백혈구 바닥 동작: " + go.name);
        }
    }

    static void LinkAntibody(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "항체");
        col.isTrigger = true;
        if (go.GetComponent<AntibodyPickup>() == null)
        {
            Undo.AddComponent<AntibodyPickup>(go);
            report.Add("항체(더블점프) 동작: " + go.name);
        }
    }

    static void LinkBounce(GameObject go)
    {
        BoxCollider2D col = EnsureBox(go);
        Undo.RecordObject(col, "튕김 항체");
        col.isTrigger = false;
        MakeGround(go);   // 낙하공격이 '착지'로 끝나야 튕길 수 있음

        BounceAntibody b = go.GetComponent<BounceAntibody>();
        if (b != null) return;   // 이미 있으면 값은 그대로 둠

        b = Undo.AddComponent<BounceAntibody>(go);
        Match m = Regex.Match(go.name, @"힘\s*(\d+(\.\d+)?)");
        float speed = m.Success ? float.Parse(m.Groups[1].Value) : (go.name.Contains("큰") ? 27f : 13f);
        b.bounceSpeed = speed;
        b.respawnTime = 2f;
        report.Add("C 튕김 항체 동작 (힘 " + speed + "): " + go.name);
    }

    static void LinkBreakable(GameObject go)
    {
        if (go.GetComponent<BreakablePlatform>() != null) return;   // 이미 있으면 값 그대로
        EnsureBox(go);
        MakeGround(go);
        BreakablePlatform bp = Undo.AddComponent<BreakablePlatform>(go);
        bp.breakDelay = 1f;
        report.Add("부서지는 발판 동작 추가: " + go.name + " (" + go.transform.position.x.ToString("0.#") + ", " + go.transform.position.y.ToString("0.#") + ")");
    }

    // 첫 버전 메뉴가 '[안내] C 튕김 항체 — …' 글자에 실수로 튕김 항체·콜라이더·Ground 태그를 붙였던 것을 되돌립니다.
    static void UndoHintMistake(GameObject go)
    {
        BounceAntibody b = go.GetComponent<BounceAntibody>();
        if (b == null) return;
        Undo.DestroyObjectImmediate(b);
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) Undo.DestroyObjectImmediate(col);
        Undo.RecordObject(go, "안내 글자 되돌리기");
        go.tag = "Untagged";
        go.layer = 0;
        report.Add("안내 글자에 잘못 붙은 튕김 항체 제거: " + go.name);
    }

    // ---------------- 예전 맵에서 켜져 남은 몬스터·보스 끄기 ----------------

    static void TurnOffStrayOldMap()
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.activeSelf || go.name == RootName) continue;
            if (go.GetComponentInChildren<PlayerController>(true) != null) continue;
            if (go.GetComponentInChildren<Camera>(true) != null) continue;

            bool oldMonster = go.GetComponentInChildren<EnemySpawnPoint>(true) != null
                || go.GetComponentInChildren<EnemyBase>(true) != null
                || go.GetComponentInChildren<BossController>(true) != null;
            if (!oldMonster) continue;

            Undo.RecordObject(go, "예전 몬스터 끄기");
            go.SetActive(false);
            report.Add("예전 맵 몬스터·보스 꺼 둠 (지우지 않음): " + go.name);
        }
    }

    // ---------------- 공용 ----------------

    static BoxCollider2D EnsureBox(GameObject go)
    {
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null) return col;

        col = Undo.AddComponent<BoxCollider2D>(go);
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Undo.RecordObject(col, "콜라이더 크기");
            col.size = sr.drawMode != SpriteDrawMode.Simple ? sr.size
                : (sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one);
            col.offset = Vector2.zero;
        }
        report.Add("콜라이더 추가: " + go.name);
        return col;
    }

    static void MakeGround(GameObject go)
    {
        Undo.RecordObject(go, "땅 태그");
        go.tag = "Ground";
        int layer = LayerMask.NameToLayer("Ground");
        if (layer >= 0) go.layer = layer;
    }
}
