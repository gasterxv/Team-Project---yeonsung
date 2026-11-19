using UnityEngine;

// 몬스터가 아니라 항상 활성화된 별도 빈 오브젝트에 붙입니다.
public class EnemySpawnPoint : MonoBehaviour
{
    private enum SpawnState
    {
        Ready,              // 최초 생성 대기
        Alive,              // 현재 몬스터 존재
        WaitingForReentry,  // 거리 이탈로 제거됨: 재진입 시 생성
        Defeated,           // 사망: 다시 생성하지 않음
        Unavailable         // 외부 삭제/비활성화: 다시 생성하지 않음
    }

    [Header("플레이어는 비워두면 자동으로 찾습니다")]
    [SerializeField] private Transform player;

    [Header("Project의 몬스터 프리팹 연결")]
    [SerializeField] private EnemyBase enemyPrefab;

    [Header("생성 위치: 비워두면 이 오브젝트 위치 사용")]
    [SerializeField] private Transform spawnPoint;

    private EnemyBase instance;
    private SpawnState state = SpawnState.Ready;
    private bool playerWasInside;

    private Vector3 SpawnPosition =>
        spawnPoint != null ? spawnPoint.position : transform.position;

    private Quaternion SpawnRotation =>
        spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError(
                "EnemySpawnPoint에 Enemy Prefab을 연결해주세요.", this);

            enabled = false;
            return;
        }

        if (!enemyPrefab.gameObject.activeSelf ||
            !enemyPrefab.enabled ||
            enemyPrefab.IsDead)
        {
            Debug.LogError(
                "몬스터 프리팹은 활성 상태이며 HP가 0보다 커야 합니다.", this);

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (enemyPrefab == null) return;

        // 기존 플레이어가 삭제되면 부활한 새 플레이어를 찾습니다.
        if (player == null)
        {
            playerWasInside = false;

            PlayerController newPlayer =
                FindFirstObjectByType<PlayerController>();

            // 아직 플레이어가 생성되지 않았다면 다음 프레임에 다시 찾습니다.
            if (newPlayer == null) return;

            player = newPlayer.transform;
        }

        // 플레이어와 스폰 지점 사이의 XY 거리를 기준으로 판단합니다.
        float range = enemyPrefab.MaxChaseDistance;
        Vector2 offset =
            (Vector2)player.position - (Vector2)SpawnPosition;

        bool inside = offset.sqrMagnitude <= range * range;
        bool entered = inside && !playerWasInside;

        playerWasInside = inside;

        // 같은 프레임에 사망과 거리 이탈이 발생해도 사망을 우선합니다.
        if (instance != null && instance.IsDead)
        {
            state = SpawnState.Defeated;
        }

        if (state == SpawnState.Defeated)
        {
            // 범위 안에서는 사망 애니메이션을 유지합니다.
            // 비활성화되었거나 플레이어가 범위 밖이면 정리합니다.
            if (instance != null &&
                (!instance.gameObject.activeInHierarchy || !inside))
            {
                RemoveInstance();
            }

            return;
        }

        if (state == SpawnState.Alive)
        {
            if (instance == null ||
                !instance.gameObject.activeInHierarchy ||
                !instance.enabled)
            {
                // 외부에서 삭제하거나 비활성화했다면 자동 재생성하지 않습니다.
                state = SpawnState.Unavailable;
                return;
            }

            if (!inside)
            {
                RemoveInstance();
                state = SpawnState.WaitingForReentry;
            }

            return;
        }

        if ((state == SpawnState.Ready && inside) ||
            (state == SpawnState.WaitingForReentry && entered))
        {
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        // 스폰 지점과 분리된 오브젝트로 생성합니다.
        instance = Instantiate(
            enemyPrefab,
            SpawnPosition,
            SpawnRotation);

        instance.UseSpawnPointManagement();
        instance.Died += OnEnemyDied;

        state = instance.IsDead
            ? SpawnState.Defeated
            : SpawnState.Alive;
    }

    private void OnEnemyDied(EnemyBase enemy)
    {
        if (enemy == instance)
        {
            state = SpawnState.Defeated;
        }
    }

    private void RemoveInstance()
    {
        if (instance == null) return;

        instance.Died -= OnEnemyDied;

        // 삭제 전에 공격과 센서부터 정지합니다.
        instance.gameObject.SetActive(false);
        Destroy(instance.gameObject);

        instance = null;
    }

    private void OnDestroy()
    {
        RemoveInstance();
    }

    private void OnDrawGizmosSelected()
    {
        if (enemyPrefab == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            SpawnPosition,
            enemyPrefab.MaxChaseDistance);

        Gizmos.DrawSphere(SpawnPosition, 0.15f);
    }
}