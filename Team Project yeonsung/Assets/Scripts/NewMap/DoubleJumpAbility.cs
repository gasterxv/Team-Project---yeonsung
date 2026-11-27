using UnityEngine;

// 더블점프(이단 점프). 플레이어 프리팹은 고치지 않고, 씬의 '규칙' 오브젝트에 붙어서 지금 플레이어를 찾아 동작합니다.
// 항체(AntibodyPickup)를 먹으면 Unlock. 공중에서 점프 키를 한 번 더 누르면 점프 힘(jumpForce)으로 다시 뜀.
// 땅에 닿으면 공중 점프가 다시 충전됩니다. 얻은 상태는 체크포인트에 저장됩니다.
public class DoubleJumpAbility : ResettableBehaviour
{
    public static DoubleJumpAbility Instance { get; private set; }

    [SerializeField] bool unlocked;

    PlayerController player;
    Rigidbody2D rb;
    bool airJumpReady;
    bool requested;

    public bool Unlocked { get { return unlocked; } }

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    public void Unlock()
    {
        unlocked = true;
        airJumpReady = true;
        Debug.Log("[새맵] 항체 획득: 더블점프 사용 가능");
    }

    // C 튕김 항체 등에서 공중 점프를 다시 채워 줄 때
    public void RefreshAirJump()
    {
        airJumpReady = true;
    }

    void Update()
    {
        if (!FindPlayer() || !unlocked || Time.timeScale == 0f || player.IsDead) return;
        if (Input.GetKeyDown(player.jumpKey)
            && !PlayerStateReader.IsGrounded(player)
            && airJumpReady
            && !PlayerStateReader.IsBusy(player))
        {
            requested = true;
        }
    }

    void FixedUpdate()
    {
        if (!FindPlayer()) return;
        if (PlayerStateReader.IsGrounded(player)) airJumpReady = true;

        if (!requested) return;
        requested = false;
        if (unlocked && airJumpReady && !PlayerStateReader.IsGrounded(player) && !PlayerStateReader.IsBusy(player))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, player.jumpForce);
            airJumpReady = false;
        }
    }

    bool FindPlayer()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        }
        return player != null && rb != null;
    }

    protected override object CaptureState() { return unlocked; }
    protected override void ApplyState(object state) { unlocked = (bool)state; }
}
