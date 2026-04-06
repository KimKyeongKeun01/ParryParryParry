using System.Collections;
using UnityEngine;

public class Enemy_Boar : BaseEnemy
{
    [Header(" === Wild Boar ===")]
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    
    [Header("Attack")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float windupTime = 0.5f;
    [SerializeField] private float dashRestTime = 1f;
    [SerializeField] private float dashKnockbackForce;
    private bool isDashing = false;
    private Coroutine dashRoutine;

    [Header("Stun")]
    [SerializeField] private float parryStunTime = 3f;
    [SerializeField] private float wallStunTime = 2f;

    [Header("Detect")]
    [SerializeField] private float edgeOffset = 1f;
    [SerializeField] private float wallOffset = 0.5f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Color windupColor = Color.red;
    [SerializeField] private Color stunColor = Color.yellow;
    [SerializeField] private Color hitColor;
    private Color originalColor;
    private Vector3 defaultScale;

    protected override void Awake()
    {
        base.Awake();
        if (_sprite != null) originalColor = _sprite.color;
        defaultScale = transform.localScale;

        facingX = Mathf.Sign(transform.localScale.x);
        _coll = GetComponentInChildren<Collider2D>();
    }

    protected override void Update()
    {
        if (isDead) return;
        base.Update();

        UpdateVisualFlip();
    }

    public override void ChangeState(EnemyState next)
    {
        if (curState == EnemyState.Stun && next != EnemyState.Stun)
        {
            _coll.isTrigger = false;
            _rigid.bodyType = RigidbodyType2D.Dynamic;
            OffStunVisual();
        }

        if (next == EnemyState.Stun)
        {
            _rigid.bodyType = RigidbodyType2D.Kinematic;
            _coll.isTrigger = true;
            OnStunVisual();
        }

        base.ChangeState(next);
    }

    #region 대기/이동
    public override void OnIdle()
    {
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);

        if (stateTimer <= 0)
        {
            if (Random.value < 0.8f) ChangeState(EnemyState.Move);
            else ChangeState(EnemyState.Idle);

            stateTimer = 2f;
        }
    }

    public override void OnMove()
    {
        if (IsEdge() || IsWall())
        {
            facingX *= -1;
            UpdateVisualFlip();
            return;
        }

        _rigid.linearVelocity = new Vector2(moveSpeed * facingX, _rigid.linearVelocityY);
    }
    #endregion

    #region 공격
    public override void OnAttack()
    {
        if (isDashing) return;

        if (dashRoutine != null) StopCoroutine(dashRoutine);
        dashRoutine = StartCoroutine(Co_DashAttack());
    }

    private IEnumerator Co_DashAttack()
    {
        isDashing = true;

        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);

        // 방향 설정
        Player player = Player.Instance;
        if (player != null)
        {
            facingX = Mathf.Sign(player.transform.position.x - transform.position.x);
        }
            
        // 준비
        if (_sprite != null) _sprite.color = windupColor;
        yield return new WaitForSeconds(windupTime);
        if (_sprite != null) _sprite.color = originalColor;

        // 돌진
        while (CurState == EnemyState.Attack)
        {
            if (IsWall())
            {
                Debug.Log("[Boar] Hit Wall! Stuned");
                OnStun(wallStunTime);
                break;
            }

            if (IsEdge()) break;

            _rigid.linearVelocity = new Vector2(dashSpeed * facingX, _rigid.linearVelocityY);
            yield return null;
        }

        // 정지
        StopDash();
        if (curState != EnemyState.Stun)
        {
            yield return new WaitForSeconds(dashRestTime);
            ChangeState(EnemyState.Idle);
        }

        isDashing = false;
        dashRoutine = null;
    }

    private void StopDash()
    {
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
    }
    #endregion

    #region 탐지
    public override bool IsDetect()
    {
        Player player = Player.Instance;
        if (player == null) return false;

        // 거리 계산
        float dist = Vector2.Distance(transform.position, player.transform.position);

        // 높이 계산
        float heightDiff = Mathf.Abs(transform.position.y - player.transform.position.y);

        if (dist <= detectRange && heightDiff <= detectHeight)
        {
            // 장애물 체크만 수행
            return HasLineOfSight(player.transform.position);
        }

        return false;
    }

    private bool IsEdge()
    {
        float rayX = _coll.bounds.center.x + (facingX * _coll.bounds.extents.x);
        Vector2 rayOrigin = new Vector2(rayX + (facingX * edgeOffset), _coll.bounds.min.y);

        return !Physics2D.Raycast(rayOrigin, Vector2.down, 0.5f, groundLayer);
    }

    private bool IsWall()
    {
        // 레이 시작 지점: 콜라이더의 앞쪽 끝 중앙
        float rayX = _coll.bounds.center.x + (facingX * _coll.bounds.extents.x);
        Vector2 rayOrigin = new Vector2(rayX, _coll.bounds.center.y);

        // 아주 짧은 거리만 쏴서 바로 앞의 벽만 감지
        return Physics2D.Raycast(rayOrigin, Vector2.right * facingX, wallOffset + 0.1f, groundLayer);
    }
    #endregion

    #region 피격
    public override void TakeDamage(int dmg)
    {
        base.TakeDamage(dmg);
        PlayHitEffect();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (CurState != EnemyState.Attack)
        {
            base.OnCollisionEnter2D(collision);
            return;
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            Player player = Player.Instance;
            if (player == null) return;

            // 가드 판정 및 넉백 적용
            Vector2 hitDir = new Vector2(facingX, 0.2f).normalized;
            Player.GuardType guard = player.controller.OnKnockback(hitDir, dashKnockbackForce);

            if (guard == Player.GuardType.PerfectGuard)
            {
                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 1f));

                // 퍼펙트 가드 시 길게 기절 (플레이어 반격 기회)
                StopDash();
                OnStun(parryStunTime);
            }
            else if (guard == Player.GuardType.Guard)
            {
                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 1f));
            }
            else
            {
                player.TakeDamaged(damage);

                // 적은 짧게 주춤
                StopDash();
                StartCoroutine(Co_IgnorePlayer(player, 1f));
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Player"))
        {
            Player player = Player.Instance;
            if (player == null) return;

            // 플레이어 슬램
            if (CurState == EnemyState.Stun && player.isSlam)
            {
                Debug.Log("[Met] Player Attacked Slam!");

                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 0.2f));

                // 플레이어 넉백
                player.controller.OnKnockback(Vector2.up, slamKnockbackForce);
                EffectManager.Instance.PlaySlamImpactVisual(transform, player.transform.position, Vector2.up, true);
                TakeDamage(1);
                return;
            }

            // 공격 중일 때만 가드 및 데미지 판정
            if (curState == EnemyState.Attack)
            {
                Vector2 hitDir = new Vector2(facingX, 0.2f).normalized;
                Player.GuardType guard = player.controller.OnKnockback(hitDir, dashKnockbackForce);

                if (guard == Player.GuardType.PerfectGuard)
                {
                    Debug.Log("[Boar] Perfect Guarded!");
                    StartCoroutine(Co_IgnorePlayer(player, 1f));
                    StopDash();
                    OnStun(parryStunTime);
                }
                else if (guard == Player.GuardType.Guard)
                {
                    Debug.Log("[Boar] Normal Guarded!");
                    StartCoroutine(Co_IgnorePlayer(player, 1f));
                    StopDash();
                    ChangeState(EnemyState.Idle);
                }
                else
                {
                    Debug.Log("[Boar] Hit Player!");
                    StartCoroutine(Co_IgnorePlayer(player, 1f));
                    player.TakeDamaged(damage);
                    StopDash();
                }
            }
        }
    }
    #endregion

    #region 비주얼
    private void UpdateVisualFlip()
    {
        // 멧돼지 이미지가 기본적으로 왼쪽을 보고 있다고 가정할 때의 스케일 반전
        transform.localScale = new Vector3(defaultScale.x * -facingX, defaultScale.y, defaultScale.z);
    }

    private Coroutine blinkRoutine;
    private int blinkCount = 3;
    private float blinkInterval = 0.08f;

    public virtual void PlayHitEffect()
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);

        blinkRoutine = StartCoroutine(Co_Blink());
    }

    protected virtual IEnumerator Co_Blink()
    {
        if (_sprite == null) yield break;

        for (int i = 0; i < blinkCount; i++)
        {
            // 알파값이 적용된 컬러로 변경
            _sprite.color = new Color(_sprite.color.r, _sprite.color.g, _sprite.color.b, 0.2f);
            yield return new WaitForSeconds(blinkInterval);

            // 다시 원래 상태의 컬러(Alpha 1.0f 등)로 복구
            _sprite.color = new Color(_sprite.color.r, _sprite.color.g, _sprite.color.b, 1f);
            yield return new WaitForSeconds(blinkInterval);
        }

        blinkRoutine = null;
    }

    public virtual void OnStunVisual()
    {
        Debug.Log("[Boss Visual] Groggy Enable");
        _sprite.color = stunColor;
    }

    /// <summary> 그로기 해제 컬러 세팅(필요시 구현) </summary>
    public virtual void OffStunVisual()
    {
        Debug.Log("[Boss Visual] Groggy Disable");
        _sprite.color = originalColor;
    }
    #endregion

    #region 디버깅
    protected override void OnDrawGizmos()
    {
        #region 플레이어 탐지
        // 1. 탐지 범위 (Yellow Circle)
        // BaseEnemy의 GetDetectOrigin()을 사용하여 실제 탐지 시작점 기준으로 그림
        Gizmos.color = Color.red;
        Vector2 detectOrigin = GetDetectOrigin();
        Gizmos.DrawWireSphere(detectOrigin, detectRange);

        // 시야선 시각화 (플레이어가 있을 때만)
        if (Application.isPlaying && Player.Instance != null)
        {
            bool detected = IsDetect();
            Gizmos.color = detected ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, Player.Instance.transform.position);
        }
        #endregion

        #region 장애물 감지
        if (_coll == null) return;

        // 절벽 감지 레이 시각화
        float rayX = _coll.bounds.center.x + (facingX * _coll.bounds.extents.x);
        Vector2 rayOrigin = new Vector2(rayX + (facingX * edgeOffset), _coll.bounds.min.y);

        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.down * 0.5f * facingX);

        // 벽 감지 레이 시각화
        rayX = _coll.bounds.center.x + (facingX * _coll.bounds.extents.x);
        rayOrigin = new Vector2(rayX, _coll.bounds.center.y);

        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.right * facingX * (wallOffset + 0.1f));        
        #endregion
    }
    #endregion
}
