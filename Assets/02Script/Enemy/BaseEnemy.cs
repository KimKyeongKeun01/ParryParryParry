using System.Collections;
using UnityEngine;

public abstract class BaseEnemy : MonoBehaviour, IDamageable, IEnemy
{
    #region 상태
    public enum EnemyState { Idle, Move, Attack, Stun, Dead }

    [Header(" === Base Enemy === ")]
    [Header("State Machine")]
    [Tooltip("현재 AI 상태 (읽기 전용 디버그용)")]
    [SerializeField] protected EnemyState curState;
    public EnemyState CurState => curState;
    [Tooltip("상태 타이머"), SerializeField] protected float stateTimer = 0f;
    #endregion

    #region 스탯
    [Header("Base Stats")]
    [Tooltip("사망 여부")][SerializeField] public bool isDead = false;
    [Tooltip("최대 체력")][SerializeField] protected int maxHp = 3;
    [Tooltip("현재 체력")][SerializeField] protected int curHp;
    [Tooltip("플레이어에게 가하는 데미지")][SerializeField] protected int damage = 1;
    [Tooltip("기절 시간"), SerializeField] protected float stunDuration;

    [Header("Knockback")]
    [Tooltip("슬램 피격 시 플레이어를 위로 튕겨내는 힘")][SerializeField] protected float slamKnockbackForce = 20f;
    [Tooltip("대각 슬램 피격 시 적이 수평으로 밀려나는 속도")][SerializeField] protected float slamPushForce = 10f;
    [Tooltip("슬램 피격 후 AI가 멈추는 시간 (밀쳐지는 동안 AI 간섭 방지)")][SerializeField] protected float slamStunDuration = 0.5f;
    #endregion

    #region 탐지
    [Header("Detect Settings")]
    [Tooltip("플레이어 탐지 거리"), SerializeField] protected float detectRange = 7f;
    [Tooltip("플레이어 탐지 높이"), SerializeField] protected float detectHeight = 3f;
    [Tooltip("탐지 시작점 Y 오프셋"), SerializeField] protected float detectOffsetY = 0f;
    [Tooltip("탐지 시작점 X 오프셋"), SerializeField] protected float detectOffsetX = 0f;
    [Tooltip("장애물(벽/바닥) 레이어"), SerializeField] protected LayerMask groundLayer;
    protected float facingX = 1f;
    #endregion

    #region 참조
    protected Rigidbody2D _rigid;
    protected Collider2D _coll;
    private bool isKnockbackStun; // 슬램 밀치기 스턴 — velocity 유지
    #endregion

    protected virtual void Awake()
    {
        _rigid = GetComponent<Rigidbody2D>();
        _coll = GetComponentInChildren<Collider2D>();
        curHp = maxHp;
    }

    protected virtual void Update()
    {
        if (isDead) return;

        if (stateTimer > 0f) stateTimer -= Time.deltaTime;

        UpdateFacing();

        #region 상태 머신
        switch (curState)
        {
            case EnemyState.Idle:
                OnIdle();
                if (IsDetect()) ChangeState(EnemyState.Attack);
                break;
            case EnemyState.Move:
                OnMove();
                if (IsDetect()) ChangeState(EnemyState.Attack);
                break;
            case EnemyState.Attack:
                OnAttack();

                // 타임아웃
                if (IsDetect()) stateTimer = 5f;
                if (!IsDetect() && stateTimer <= 0) ChangeState(EnemyState.Idle);
                break;
            case EnemyState.Stun:
                _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);

                // 타임아웃
                if (stateTimer <= 0) ChangeState(EnemyState.Idle);
                break;
        }
        #endregion
    }

    #region 상태 전이
    public virtual void ChangeState(EnemyState next)
    {
        if (isDead || curState == next) return;

        curState = next;

        // 상태별 기본 타이머 설정
        switch (next)
        {
            case EnemyState.Attack:
                stateTimer = 5f; // 플레이어를 놓쳐도 5초간은 공격 태세 유지 (전투 긴장감)
                break;
            case EnemyState.Stun:
                stateTimer = stunDuration;
                break;
            default:
                stateTimer = 0f;
                break;
        }
    }
    #endregion

    #region 탐지
    public virtual bool IsDetect()
    {
        Player player = Player.Instance;
        if (player == null) return false;

        Collider2D playerColl = player.GetComponent<Collider2D>();
        if (playerColl == null) return false;

        Vector2 origin = GetDetectOrigin();
        Vector2 target = playerColl.bounds.center;
        Vector2 toPlayer = target - origin;

        // 1. 방향 체크
        if (toPlayer.x * facingX <= 0f) return false;

        // 2. 범위 체크
        if (Mathf.Abs(toPlayer.x) > detectRange) return false;
        if (Mathf.Abs(toPlayer.y) > detectHeight) return false;

        // 3. 장애물 체크
        if (!HasLineOfSight(target)) return false;

        return true;
    }

    /// <summary>탐지 시작점을 계산합니다.</summary>
    protected Vector2 GetDetectOrigin()
    {
        if (_coll == null) return (Vector2)transform.position;
        Bounds bounds = _coll.bounds;
        return new Vector2(bounds.center.x + facingX * (bounds.extents.x + detectOffsetX),
                           bounds.center.y + detectOffsetY);
    }

    /// <summary>플레이어와의 사이에 장애물이 없는지 확인합니다.</summary>
    protected bool HasLineOfSight(Vector2 targetPos)
    {
        Vector2 origin = GetDetectOrigin();
        RaycastHit2D hit = Physics2D.Linecast(origin, targetPos, groundLayer);
        return hit.collider == null;
    }
    #endregion

    #region 대기/이동
    /// <summary>하위 클래스에서 대기 로직 구현</summary>
    public virtual void OnIdle() { }

    /// <summary>하위 클래스에서 이동 로직 구현</summary>
    public virtual void OnMove() { }

    protected virtual void UpdateFacing()
    {
        if (Mathf.Abs(_rigid.linearVelocityX) > 0.1f)
        {
            facingX = Mathf.Sign(_rigid.linearVelocityX);
        }
    }
    #endregion

    #region 스턴
    /// <summary>일반 스턴: x 정지 / 슬램 넉백 스턴: velocity 유지</summary>
    public void OnStun(float duration)
    {
        ChangeState(EnemyState.Stun);

        stateTimer = duration;
    }
    #endregion

    #region 전투
    public virtual bool CanSlam()
    {
        return curState != EnemyState.Attack;
    }

    public virtual void OnAttack() { }
    #endregion

    #region 피격
    public virtual void TakeDamage(int dmg)
    {
        if (isDead) return;

        curHp -= dmg;
        Debug.Log($"[Enemy] {gameObject.name} Damaged!, HP: {curHp}/{maxHp}");

        if (curHp <= 0) Die();
    }

    protected virtual void OnEnable()
    {
        isDead = false;
        curHp = maxHp;
        ChangeState(EnemyState.Idle);
    }
    protected virtual void Die()
    {
        isDead = true;

        curState = EnemyState.Dead;
        Destroy(gameObject);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Player player = Player.Instance;

            // 대쉬 충돌
            if (player != null && player.isDash && !player.isSlam)
            {
                // 기절 중엔 플레이어 피격 안됨
                if (curState == EnemyState.Stun) return;

                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 1f));

                // 대쉬 캔슬
                player.controller.CancelDash();
                _rigid.linearVelocity = new Vector2(0f, _rigid.linearVelocity.y);

                // 피격
                player.controller.OnKnockback(new Vector2(facingX, 0.2f), slamKnockbackForce * 0.5f);
                player.TakeDamaged(1);
                
                return;
            }

            // 슬램 피격
            if (player != null && player.isSlam)
            {

                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 0.2f));

                // 슬램 방향 계산
                Vector2 slamDir = player.controller.CurrentSlamDir;

                // 플레이어, 적 넉백
                player.controller.OnKnockback(Vector2.up, slamKnockbackForce, -1, true);
                TakeDamage(1);
                return;
            }
            else if (player != null && !player.isSlam)
            {
                // 기절 중엔 플레이어 피격 안됨
                if (curState == EnemyState.Stun) return;

                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 1f));

                player.controller.OnKnockback(new Vector2(facingX, 0.2f), slamKnockbackForce * 0.5f);
                player.TakeDamaged(1);
            }
        }
    }

    protected IEnumerator Co_IgnorePlayer(Player player, float duration)
    {
        Collider2D playerCol = player.GetComponent<Collider2D>();
        if (playerCol == null || _coll == null) yield break;

        Physics2D.IgnoreCollision(_coll, playerCol, true);

        yield return new WaitForSeconds(duration);

        if (playerCol != null && _coll != null)
        {
            Physics2D.IgnoreCollision(_coll, playerCol, false);
        }
    }
    #endregion

    #region 시각화
    protected virtual void OnDrawGizmos()
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(detectRange, 0);

        // 탐지 박스 시각화
        Gizmos.color = Color.yellow;
        Vector2 boxSize = new Vector2(detectRange * 2, detectHeight * 2); // 높이는 위아래 합산 기준
        Vector2 boxCenter = origin + new Vector2(-detectRange, 0);
        Gizmos.DrawWireCube(boxCenter, boxSize);

        // 시야선 시각화 (플레이어가 있을 때만)
        if (Application.isPlaying && Player.Instance != null)
        {
            bool detected = IsDetect();
            Gizmos.color = detected ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, Player.Instance.transform.position);
        }
    }
    #endregion
}
