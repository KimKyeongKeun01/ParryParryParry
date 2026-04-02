using DG.Tweening;
using System.Collections;
using UnityEngine;

public class Enemy_Slime : BaseEnemy
{
    [Header(" === Enemy Slime === ")]
    [Header("Movement")]
    [Tooltip("대기 시간"), SerializeField] private float idleDuration;
    [Tooltip("이동 거리")][SerializeField] private float moveRange = 3f;
    [Tooltip("이동 속도 ")][SerializeField] private float moveTime = 0.4f;
    [Tooltip("추격시 속도"), SerializeField] private float chaseSpeedMulti;
    private bool isActing = false;
    private Coroutine actRoutine;


    [Header("Detect")]
    [SerializeField] private float edgeOffset = 1f;
    [SerializeField] private float wallOffset = 0.5f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [Space(5)]
    [SerializeField] private Vector3 idleSquashScale = new Vector3(1.1f, 0.9f, 1f);
    [SerializeField] private float idleSquashTime = 0.5f;
    [SerializeField] private Ease idleSquashEase;
    [Space(5)]
    [SerializeField] private Vector3 moveSquashScale = new Vector3(1.2f, 0.7f, 1f);
    [SerializeField] private float moveSquashTime = 0.2f;
    [SerializeField] private Ease moveSquashEase;
    [Space(5)]
    [SerializeField] private Vector3 moveEndSquarshScale = new Vector3(0.8f, 1.2f, 1f);
    [SerializeField] private float moveEndSquarshTime;
    [SerializeField] private Ease moveEndSquashEase;


    private SpriteRenderer _sprite;
    private Vector3 defaultScale;
    private Sequence squashSequence;

    protected override void Awake()
    {
        base.Awake();
        defaultScale = transform.localScale;

        _sprite = GetComponentInChildren<SpriteRenderer>();
    }

    protected override void Update()
    {
        if (isDead) return;

        #region 상태 전환 ( Move 90% <-> Idle 10% )
        // 행동 중이 아닐 때만 다음 상태 결정 (9:1 비율)
        if (!isActing && CurState != EnemyState.Attack && CurState != EnemyState.Stun)
        {
            if (Random.value < 0.9f) ChangeState(EnemyState.Move);
            else ChangeState(EnemyState.Idle);
        }
        #endregion

        base.Update();
    }

    #region 상태 전이
    public override void ChangeState(EnemyState next)
    {
        if (CurState == next) return;

        // 상태가 바뀌면 기존 코루틴 행동을 끊어야 함 (예: Move 도중 Attack 감지 시)
        if (next == EnemyState.Attack || next == EnemyState.Stun)
        {
            StopAction();
        }

        base.ChangeState(next);
    }

    private void StopAction()
    {
        isActing = false;
        if (actRoutine != null)
        {
            StopCoroutine(actRoutine);
            actRoutine = null;
        }
        KillSquash();
    }
    #endregion

    #region 대기
    public override void OnIdle()
    {
        if (isActing) return;
        actRoutine = StartCoroutine(Co_Idle(idleDuration));
    }

    private IEnumerator Co_Idle(float time)
    {
        isActing = true;
        _rigid.linearVelocity = new Vector2(0f, _rigid.linearVelocityY);

        PlayIdleSquash();
        yield return new WaitForSeconds(time);

        isActing = false;
        actRoutine = null;
    }
    #endregion

    #region 이동
    public override void OnMove()
    {
        if (isActing) return;
        actRoutine = StartCoroutine(Co_Move());
    }

    private IEnumerator Co_Move()
    {
        isActing = true;

        // 이동 시작 연출
        PlayMoveStartSquash();

        float speed = moveRange / moveTime;
        float elapsed = 0f;

        while (elapsed < moveTime)
        {
            // 벽이나 절벽 탐지 시 즉시 방향 전환 및 코루틴 종료 (다음 프레임에 다시 Move 시작됨)
            if (IsWall() || IsEdge())
            {
                facingX *= -1f;
                UpdateVisualFlip();
                break;
            }

            _rigid.linearVelocity = new Vector2(facingX * speed, _rigid.linearVelocityY);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정지 및 종료 연출
        _rigid.linearVelocity = new Vector2(0f, _rigid.linearVelocityY);
        PlayMoveEndSquash();
        yield return new WaitForSeconds(moveEndSquarshTime * 2f); // 연출 기다림
        yield return Co_Idle(idleDuration);

        isActing = false;
        actRoutine = null;
    }
    #endregion

    #region 공격
    public override void OnAttack()
    {
        if (isActing) return;

        actRoutine = StartCoroutine(Co_Attack());
    }

    private IEnumerator Co_Attack()
    {
        isActing = true;

        Player player = Player.Instance;
        if (player == null)
        {
            isActing = false;
            ChangeState(EnemyState.Idle);
            yield break;
        }

        // 방향 체크
        float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
        facingX = dir;
        UpdateVisualFlip();

        PlayMoveStartSquash();

        // 추격 속도 및 시간 계산 (배율 적용)
        float speed = (moveRange / moveTime) * chaseSpeedMulti;
        float adjustedMoveTime = moveTime / chaseSpeedMulti; // 더 빠르게 점프
        float elapsed = 0f;

        // 3. 추격 점프 루프
        while (elapsed < adjustedMoveTime)
        {
            // 추격 중에도 지형 체크
            if (IsWall() || IsEdge())
            {
                break;
            }

            _rigid.linearVelocity = new Vector2(facingX * speed, _rigid.linearVelocityY);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 착지 연출
        _rigid.linearVelocity = new Vector2(0f, _rigid.linearVelocityY);
        PlayMoveEndSquash();
        yield return new WaitForSeconds(moveEndSquarshTime * 2f);

        // 5. 추격 대기 (더 짧은 휴식)
        float adjustedIdleDuration = idleDuration / chaseSpeedMulti;
        yield return Co_Idle(adjustedIdleDuration);

        isActing = false;
        actRoutine = null;
    }
    #endregion

    #region 플레이어 탐지
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
    #endregion

    #region 장애물(벽/절벽) 체크
    private bool IsEdge()
    {
        Vector2 rayOrigin = new Vector2(transform.position.x + facingX * edgeOffset, _coll.bounds.min.y);

        return !Physics2D.Raycast(rayOrigin, Vector2.down, 0.5f, groundLayer);
    }

    private bool IsWall()
    {
        Vector2 rayOrigin = new Vector2(transform.position.x + facingX * wallOffset, _coll.bounds.center.y);

        return Physics2D.Raycast(rayOrigin, Vector2.right * facingX, 0.5f, groundLayer);
    }
    #endregion

    #region 피격
    public override void TakeDamage(int dmg)
    {
        base.TakeDamage(dmg);
        PlayHitEffect();
    }

    protected override void Die()
    {
        StopAction();

        base.Die();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Player player = Player.Instance;
            if (player == null) return;

            Vector2 hitDir = new Vector2(facingX, 0.2f).normalized;
            Player.GuardType guard = player.controller.OnKnockback(hitDir, slamKnockbackForce * 0.5f);

            // 플레이어 슬램
            if (player.isSlam)
            {
                base.OnCollisionEnter2D(collision);
                return;
            }

            // 플레이어 피격
            if (guard == Player.GuardType.PerfectGuard)
            {
                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 1f));

                // 퍼펙트 가드 시 길게 기절 (플레이어 반격 기회)
            }
            else if (guard == Player.GuardType.Guard)
            {
                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 1f));
            }
            else
            {
                StartCoroutine(Co_IgnorePlayer(player, 1f));

                player.TakeDamaged(damage);
            }
        }
    }
    #endregion

    #region Visual
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
            _sprite.color = new Color(_sprite.color.r, _sprite.color.g, _sprite.color.b, 0.78f);
            yield return new WaitForSeconds(blinkInterval);
        }

        blinkRoutine = null;
    }

    private void PlayMoveStartSquash() => PlaySquash(moveSquashScale, moveSquashTime, moveSquashEase);
    private void PlayMoveEndSquash() => PlaySquash(moveEndSquarshScale, moveEndSquarshTime, moveEndSquashEase);

    private void PlayIdleSquash()
    {
        KillSquash();
        Vector3 orientedIdleScale = GetOrientedScale(idleSquashScale);
        Vector3 orientedDefault = GetOrientedScale(defaultScale);

        squashSequence = DOTween.Sequence()
            .Append(visualRoot.DOScale(orientedIdleScale, idleSquashTime).SetEase(idleSquashEase))
            .Append(visualRoot.DOScale(orientedDefault, idleSquashTime).SetEase(idleSquashEase))
            .SetLoops(-1, LoopType.Restart);
    }

    private void PlaySquash(Vector3 targetScale, float time, Ease ease)
    {
        KillSquash();
        Vector3 orientedTarget = GetOrientedScale(targetScale);
        Vector3 orientedDefault = GetOrientedScale(defaultScale);

        squashSequence = DOTween.Sequence()
            .Append(visualRoot.DOScale(orientedTarget, time).SetEase(ease))
            .Append(visualRoot.DOScale(orientedDefault, time).SetEase(Ease.OutQuad));
    }

    private Vector3 GetOrientedScale(Vector3 baseScale) => new Vector3(baseScale.x * facingX, baseScale.y, baseScale.z);
    private void UpdateVisualFlip() => visualRoot.localScale = GetOrientedScale(defaultScale);

    private void KillSquash()
    {
        if (squashSequence != null) { squashSequence.Kill(); squashSequence = null; }
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

        // 2. 이동 범위 (Cyan Circle)
        // 현재 위치에서 한 번의 점프로 이동 가능한 최대 거리 시각화
        Gizmos.DrawWireSphere(transform.position, moveRange);
        #endregion

        #region 장애물 감지
        // 절벽 감지 레이 시각화
        float originY = Application.isPlaying ? _coll.bounds.min.y : transform.position.y;

        Vector2 edgeOrigin = new Vector2(transform.position.x + facingX * edgeOffset, originY);
        Gizmos.DrawLine(edgeOrigin, edgeOrigin + Vector2.down * 0.5f);

        // 벽 감지 레이 시각화
        originY = Application.isPlaying ? _coll.bounds.center.y : transform.position.y;

        Vector2 wallOrigin = new Vector2(transform.position.x, originY);
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * facingX * wallOffset);
        #endregion
    }
    #endregion
}
