using UnityEngine;

// 몬스터가 아니라 항상 활성화된 별도 빈 오브젝트에 붙인다.
public class EnemySpawnPoint : MonoBehaviour
{
    private enum SpawnState
    {
        Ready,              // 최초 생성 대기
        Alive,              // 현재 몬스터 존재
        WaitingForReentry,  // 거리 이탈로 제거됨: 재진입만 허용
        Defeated,           // 사망: 이 스폰 지점은 소진됨
        Unavailable         // 외부 삭제/비활성화: 자동 재생성하지 않음
    }

    [Header("Hierarchy의 플레이어와 Project의 몬스터 프리팹 연결")]
    [SerializeField] private Transform player;
    [SerializeField] private EnemyBase enemyPrefab;

    [Header("생성 위치: 비워두면 이 오브젝트 위치 사용")]
    [SerializeField] private Transform spawnPoint;

    private EnemyBase instance;
    private SpawnState state = SpawnState.Ready;
    private bool playerWasInside;

    private Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    private Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    private void Start()
    {
        if (player == null || enemyPrefab == null)
        {
            Debug.LogError("EnemySpawnPoint에 Player와 Enemy Prefab을 연결해주세요.", this);
            enabled = false;
            return;
        }

        if (!enemyPrefab.gameObject.activeSelf || !enemyPrefab.enabled || enemyPrefab.IsDead)
        {
            Debug.LogError("몬스터 프리팹은 활성 상태이며 HP가 0보다 커야 합니다.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (player == null || enemyPrefab == null) return;

        // XY 평면의 거리. 몬스터 현재 위치나 카메라 위치는 기준으로 사용하지 않는다.
        float range = enemyPrefab.MaxChaseDistance;
        Vector2 offset = (Vector2)player.position - (Vector2)SpawnPosition;
        bool inside = offset.sqrMagnitude <= range * range;
        bool entered = inside && !playerWasInside;
        playerWasInside = inside;

        // 같은 프레임에 사망과 거리 이탈이 발생해도 사망을 우선한다.
        if (instance != null && instance.IsDead)
            state = SpawnState.Defeated;

        if (state == SpawnState.Defeated)
        {
            // 범위 안에서는 사망 애니메이션을 유지한다.
            // 애니메이션 종료로 비활성화되었거나 범위 밖이면 오브젝트를 정리한다.
            if (instance != null && (!instance.gameObject.activeInHierarchy || !inside))
                RemoveInstance();
            return;
        }

        if (state == SpawnState.Alive)
        {
            if (instance == null || !instance.gameObject.activeInHierarchy || !instance.enabled)
            {
                // 알 수 없는 이유로 사라졌다면 거리 이탈로 간주하지 않는다.
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
        // 별도 루트에 생성하여 몬스터 제거 시 스폰 지점이 함께 제거되지 않게 한다.
        instance = Instantiate(enemyPrefab, SpawnPosition, SpawnRotation);
        instance.UseSpawnPointManagement();
        instance.Died += OnEnemyDied;
        // 활성 프리팹이므로 EnemyBase.OnEnable에서 최초 위치와 HP가 초기화된다.
        state = instance.IsDead ? SpawnState.Defeated : SpawnState.Alive;
    }

    private void OnEnemyDied(EnemyBase enemy)
    {
        if (enemy == instance) state = SpawnState.Defeated;
    }

    private void RemoveInstance()
    {
        if (instance == null) return;
        instance.Died -= OnEnemyDied;
        // Destroy는 프레임 끝에 실행되므로 공격·센서부터 즉시 정지한다.
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
        Gizmos.DrawWireSphere(SpawnPosition, enemyPrefab.MaxChaseDistance);
        Gizmos.DrawSphere(SpawnPosition, 0.15f);
    }
}
