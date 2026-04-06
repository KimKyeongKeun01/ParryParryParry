using DG.Tweening;
using NUnit.Framework;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


public class Boss_Met : BaseBoss
{
    public enum AttackPattern { None, PowerDash, TuskDrive, BodySlam, GroundSlam, StampDrive }

    private BossStatus_Met Status => (BossStatus_Met)_status;
    private BossVisual_Met Visual => (BossVisual_Met)_visual;

    [Header(" === Boss Met === ")]
    [Header("Attack State Machine")]
    [SerializeField] private AttackPattern curAttack = AttackPattern.None;
    [SerializeField] protected float attackTimer = 0f; //공격 안전장치 타이머

    [Header("Power Dash")]
    [SerializeField] private bool hitDash = false;
    [SerializeField] private bool isDashing = false;

    [Header("Tusk Drive")]
    [SerializeField] private bool hitTusk = false;
    [SerializeField] private bool isTusk = false;

    [Header("Body Slam")]
    [SerializeField] private bool hitSlam = false;
    [SerializeField] private bool isSlam = false;

    [Header("Stamp Drive")]
    [SerializeField] private bool hitStamp = false;
    [SerializeField] private bool isStamping = false;

    [Header("Wave Effect")]
    [SerializeField] private Transform headPoint;
    [SerializeField] private FullscreenShockwaveController fullscreenShockwaveController;


    private bool isExhausted = false;

    // 패턴 쿨타임 타이머
    private float lastDashTime = -99f;
    private float lastTuskTime = -99f;
    private float lastSlamTime = -99f;
    private float lastGroundSlamTime = -99f;
    private float lastStampTime = -99f;

    [Header("Ground Slam")]
    [SerializeField] private bool hitGroundSlam = false;
    [SerializeField] private bool isGroundSlamming = false;
    private float groundSlamOriginY;
    private Tween _groundSlamTween;

    [SerializeField] private bool isRecovering = false;

    protected override void Awake()
    {
        base.Awake();

        #region Status 동기화
        maxHp = Status.maxHp;
        curHp = maxHp;
        damage = Status.contactDamage;

        detectRange = Status.detectRange;
        detectHeight = Status.detectHeight;

        phase2Threshold = Status.phase2Threshold;

        stunDuration = Status.exhaustedDuration;
        stunHitLimit = Status.exhaustedHitLimit;
        #endregion
    }

    protected override void Update()
    {
        //if (isPlayingCutScene) return;
        if (isDead) return;

        // 스턴 시 패턴 초기화
        if (CurState == EnemyState.Stun)
        {
            curAttack = AttackPattern.None;
            isDashing = false;
            isTusk = false;
            isSlam = false;
            isGroundSlamming = false;
            isStamping = false;

            attackTimer = 0f;
            base.Update();
            return;
        }

        // 공격 상태가 아닐 때 공격 정보 초기화
        if (CurState != EnemyState.Attack)
        {
            curAttack = AttackPattern.None;
            isDashing = false;
            isTusk = false; 
            isSlam = false;
            isGroundSlamming = false;
            isStamping = false;
        }

        // 안전 장치 타이머
        if (curAttack != AttackPattern.None)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0) StopAttack();
        }
        else
        {
            attackTimer = 0f;
        }

        if (!isPhaseTransitioning) { CheckPhase();}
        base.Update();
    }

    #region 패턴 관리
    
    private void FixedUpdate()
    {
        if (!isExhausted) return;
        Debug.Log(new Vector2(_rigid.position.x, groundSlamOriginY));
        Vector2 exhaustedPos;
        if (groundSlamOriginY != 0f) 
        {
            exhaustedPos = new Vector2(_rigid.position.x, groundSlamOriginY); 
        }
        else
        {
            exhaustedPos = _rigid.position;
        }
        Debug.Log(exhaustedPos);
        _rigid.MovePosition(exhaustedPos + Vector2.left * facingX * 5);
        //_rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        isExhausted = false;

    }

    protected override int SelectNextPattern()
    {
        Player player = Player.Instance;
        if (player == null) return -1;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        float curTime = Time.time;

        bool canDash = curTime - lastDashTime >= Status.dashCooldown;
        bool canTusk = curTime - lastTuskTime >= Status.tuskCooldown;
        bool canSlam = curTime - lastSlamTime >= Status.slamCooldown;
        bool canGroundSlam = curTime - lastGroundSlamTime >= Status.groundSlamCooldown;
        bool canStamp = curTime - lastStampTime >= Status.stampCooldown;

        AttackPattern next = AttackPattern.None;

        // [우선순위 1] 멀리 있으면 무조건 돌진
        if (dist >= 20 && canDash)
        {
            next = AttackPattern.PowerDash;
        }
        else if (dist<20 && dist > Status.meleeRange && canStamp)
        {
            next = AttackPattern.StampDrive;
        }
        // [우선순위 2] 근접 상황
        else if (dist <= Status.meleeRange)
        {
            if (canTusk && Random.value > 0.5f) next = AttackPattern.TuskDrive;
            else if (canSlam) next = AttackPattern.BodySlam;
            else if (canStamp) next = AttackPattern.StampDrive;
            else if (canDash) next = AttackPattern.PowerDash;
            //else if (canGroundSlam) next = AttackPattern.GroundSlam;
        }

        if (next == AttackPattern.None) return -1;
        return (int)next - 1;
    }

    protected override IEnumerator Co_StartPattern(int index)
    {
        if (isRecovering)
            yield return new WaitUntil(() => !isRecovering);

        // 상태 초기화
        //Visual?.ResetAnimTrigger("Motion_Reset");

        // 시작 시 플레이어 방향 보기
        LookAtPlayer();

        curAttack = (AttackPattern)(index + 1);
        attackTimer = 10f; // 패턴 타임아웃

        switch (curAttack)
        {
            case AttackPattern.PowerDash:   yield return Co_PowerDash();    break;
            case AttackPattern.TuskDrive:   yield return Co_TuskDrive();    break;
            case AttackPattern.BodySlam:    yield return Co_BodySlam();     break;
            //case AttackPattern.GroundSlam: yield return Co_GroundSlam(); break;
            case AttackPattern.StampDrive: yield return Co_StampDrive(); break;
        }

        curAttack = AttackPattern.None;
    }

    protected override float GetPatternInterval()
    {
        return phase >= 2 ? Status.patternInterval * 0.7f : Status.patternInterval;
    }
    #endregion

    #region 이동
    public override void OnIdle()
    {
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        _visual?.PlayAnim("IsMoving", false);
    }

    private void LookAtPlayer()
    {
        Player player = Player.Instance;
        if (player == null) return;
        float dirX = player.transform.position.x - transform.position.x;
        facingX = Mathf.Sign(dirX);
        _visual?.Flip(dirX > 0f);
    }

    //기존 스핀 모션
    private IEnumerator Co_MoveToRange(float stopDistance, float maxTime)
    {
        float elapsed = 0f;
        _visual?.PlayAnim("IsMoving", true);

        while (elapsed < maxTime)
        {
            float dist = Mathf.Abs(Player.Instance.transform.position.x - transform.position.x);
            if (dist <= stopDistance) break;

            LookAtPlayer();
            _rigid.MovePosition(_rigid.position + Vector2.right * facingX * _status.moveSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        _visual?.PlayAnim("IsMoving", false);
        DisableHitbox();
    }

    #endregion

    #region 공격
    private void StopAttack()
    {
        curAttack = AttackPattern.None;
        isDashing = false;
        isTusk = false;
        isSlam = false;
        isGroundSlamming = false;
        isStamping = false;

        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        _visual?.AE_AnimFinished();
        StopPattern();
        ChangeState(EnemyState.Idle);
    }

    #region 돌진
    private IEnumerator Co_PowerDash()
    {
        Debug.Log("[Met] Dash Prepare...");

        // 준비
        _visual.PlayAnim("Dash_Prepare"); // 앞발 쓸기 애니메이션
        yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);
        if (curAttack == AttackPattern.None) yield break;

        Debug.Log("[Met] Dash Start!");

        // 시작
        hitDash = false;
        isDashing = true;
        _visual.PlayAnim("Dash_Action", true);

        // 벽에 부딪힐 때까지 이동 (OnCollision에서 처리)
        while (isDashing && curAttack == AttackPattern.PowerDash)
        {
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun) break;

            Vector2 origin = (Vector2)transform.position - Vector2.up;
            RaycastHit2D wallHit = Physics2D.Raycast(origin, Vector2.right * facingX, Status.wallDetectRange, LayerMask.GetMask("Ground"));

            if (wallHit.collider != null)
            {
                Debug.Log("[Met] Wall Detected! Dash Stop");
                isDashing = false;
                _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);

                break;
            }

            _rigid.linearVelocity = new Vector2(facingX * Status.dashSpeed, _rigid.linearVelocityY);
            yield return null;
        }

        // 종료
        _visual.PlayAnim("Dash_Action", false);
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        lastDashTime = Time.time;
        DisableHitbox();
        yield return new WaitForSeconds(Status.defultWindDownTime);
    }

    public void OnDashHit(Player player)
    {
        if (hitDash) return;
        hitDash = true;

        Vector2 hitDir = new Vector2(facingX, 0.2f).normalized;
        Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.dashKnockback);

        if (guard == Player.GuardType.PerfectGuard)
        {
            Debug.Log("[Met] Dash Perfect Guarded!");

            StartExhausted();
        }
        else if (guard == Player.GuardType.Guard)
        {
            Debug.Log("[Met] Dash Guarded!");
        }
        else
        {
            Debug.Log("[Met] Dash Player Hit!");

            player.TakeDamaged(Status.dashDamage);
        }
    }
    #endregion

    #region 엄니 박기
    private IEnumerator Co_TuskDrive()
    {
        hitTusk = false;
        Debug.Log("[Met] Tusk Drive");
        yield return StartCoroutine(Co_MoveToRange(Status.tuskRange, Status.maxApproachTime));

        _visual.PlayAnim("Tusk_Action"); // 고개 숙였다 올리기
        yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);

        lastTuskTime = Time.time;
        yield return new WaitForSeconds(Status.defultWindDownTime);
    }

    public void AE_TuskAction()
    {
        if (curAttack != AttackPattern.TuskDrive) return;

        StartCoroutine(Co_TuskMove());
    }

    private IEnumerator Co_TuskMove()
    {
        isTusk = true;
        Vector2 startPos = _rigid.position;
        float traveledDistance = 0f;

        while (traveledDistance < Status.tuskRange)
        {
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun) break;

            // 이동
            float step = Status.tuskSpeed * Time.deltaTime;
            _rigid.MovePosition(_rigid.position + Vector2.right * facingX * step);

            traveledDistance = Vector2.Distance(startPos, _rigid.position);

            yield return null;
        }

        isTusk = false;
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        ValidateStage();    // 위치 x값 보정
        if (curAttack == AttackPattern.None || CurState == EnemyState.Stun) yield break;

        // 이동 완료 후 애니메이션 리셋 및 상태 마무리
        _visual.PlayAnim("Motion_Reset");
        _visual.AE_AnimFinished(); // WaitUntil 조건을 해제

        Debug.Log("[Met] Tusk Drive Finished");
    }

    public void OnTuskHit(Player player)
    {
        if (hitTusk) return;
        hitTusk = true;
        Debug.Log("터스트 힛");
        // 고개를 쳐올리는 공격이므로 위쪽 방향 힘을 더 줌
        Vector2 hitDir = new Vector2(facingX * 0.2f, 1f).normalized;
        Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.tuskUpKnockback);

        if (guard == Player.GuardType.PerfectGuard)
        {
            StartExhausted(); // 기절
        }
        else if (guard == Player.GuardType.Guard)
        {
            player.controller.OnKnockback(hitDir, Status.tuskUpKnockback * 0.5f);
        }
        else
        {
            player.TakeDamaged(Status.tuskDamage);
        }
    }
    #endregion

    #region 몸통 박치기
    private IEnumerator Co_BodySlam()
    {
        hitSlam = false;
        Debug.Log("[Met] Body Slam Prepare...");
        //yield return StartCoroutine(Co_MoveToRange(Status.meleeRange, Status.maxApproachTime))

        _visual.PlayAnim("Slam_Prepare"); // 뒤로 뺐다 치기
        yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);

        lastSlamTime = Time.time;
        yield return new WaitForSeconds(Status.defultWindDownTime);
    }

    public void AE_SlamAction()
    {
        if (curAttack != AttackPattern.BodySlam) return;

        StartCoroutine(Co_SlamMove());
    }

    private IEnumerator Co_SlamMove()
    {
        Debug.Log("[Met] Body Slam!");
        if (isSlam) yield break;
        isSlam = true;
        Vector2 startPos = _rigid.position;
        float traveledDistance = 0f;

        while (traveledDistance < Status.slamRange)
        {
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun) break;
            if (!isSlam) break;

            // 이동
            float step = Status.slamSpeed * Time.deltaTime;
            _rigid.MovePosition(_rigid.position + Vector2.right * facingX * step);

            traveledDistance = Vector2.Distance(startPos, _rigid.position);

            yield return null;
        }

        isSlam = false;
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        ValidateStage();    // 위치 x값 보정

        // 이동 완료 후 애니메이션 리셋 및 상태 마무리
        _visual.PlayAnim("Slam_Action");
        _visual.AE_AnimFinished(); // WaitUntil 조건을 해제

        Debug.Log("[Met] Body Slam Finished");
    }

    public void OnSlamHit(Player player)
    {
        if (hitSlam) return;
        hitSlam = true;

        Vector2 hitDir = new Vector2(facingX, 0.15f).normalized;
        Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.slamXForce);

        if (guard == Player.GuardType.PerfectGuard)
        {
            Debug.Log("[Met] Body Slam Perfect Guarded!");

            StartExhausted();
        }
        else if (guard == Player.GuardType.Guard)
        {
            Debug.Log("[Met] Body Slam Guarded.");

            // 일반 가드 데미지X, 넉백 적용
            player.controller.OnKnockback(hitDir, Status.slamGuardKnockback);
        }
        else // 노멀 히트
        {
            Debug.Log("[Met] Player Hit by Body Slam!");

            // 데미지 적용
            player.TakeDamaged(Status.slamDamage);
        }
    }
    #endregion

    

    #region 롤링-금지된패턴-
    //private IEnumerator Co_GroundSlam()
    //{
    //    Player player = Player.Instance;
    //    if (player == null) yield break;

    //    // 시작 시 Y위치 저장 (착지 기준점)
    //    groundSlamOriginY = _rigid.position.y;

    //    hitStamp = false;
    //    isGroundSlamming = false;

    //    Debug.Log("[Met] GroundSlam Start!");

    //    // 패턴 시작 애니메이션
    //    _visual.PlayAnim("Rolling_Action");
    //    //yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);
    //    if (curAttack == AttackPattern.None) yield break;

    //    // 2회 반복
    //    for (int i = 0; i < 2; i++)
    //    {
    //        if (curAttack == AttackPattern.None || CurState == EnemyState.Stun) yield break;

    //        // ── 올라가기 ──────────────────────────────────────────
    //        // 목표 X: 보스와 플레이어 사이의 3/4 지점
    //        float targetX = _rigid.position.x + (player.transform.position.x - _rigid.position.x) * 0.75f;
    //        float targetY = groundSlamOriginY + Status.groundSlamJumpHeight;
    //        Vector2 jumpTarget = new Vector2(targetX, targetY);

    //        Debug.Log($"[Met] GroundSlam Jump Up #{i + 1} → {jumpTarget}"); 

    //        yield return StartCoroutine(Co_MoveToTarget(
    //        jumpTarget,
    //        0.3f,   // 예: 0.2f ~ 0.3f
    //        Ease.OutQuad                     // 처음 빠르게 치고 올라감
    //    ));

    //        if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
    //        {
    //            KillGroundSlamTween();
    //            yield break;
    //        }

    //        _rigid.position = jumpTarget;
    //        _rigid.linearVelocity = Vector2.zero;

    //        // ── 공중 대기 1초 ─────────────────────────────────────
    //        Debug.Log("[Met] GroundSlam Hovering...");
    //        yield return new WaitForSeconds(1f);
    //        if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
    //        {
    //            KillGroundSlamTween();
    //            yield break;
    //        }

    //        // ── 내려찍기 (플레이어 방향 대각선 낙하) ──────────────
    //        // 낙하 시작 시점 플레이어 위치 스냅
    //        Vector2 slamTarget = new Vector2(player.transform.position.x, groundSlamOriginY);
    //        hitStamp = false;
    //        isGroundSlamming = true;
    //        EnableHitbox();

    //        Debug.Log($"[Met] GroundSlam Down #{i + 1} → {slamTarget}");


    //        yield return StartCoroutine(Co_MoveToTarget(
    //        slamTarget,
    //        0.3f,   // 예: 0.3f ~ 0.45f
    //        Ease.InQuad                      // 처음 느리다가 점점 빠르게
        
    //        ));


    //        // 중간에 끊기면 정리 후 종료
    //        if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
    //        {
    //            isGroundSlamming = false;
    //            DisableHitbox();
    //            KillGroundSlamTween();
    //            yield break;
    //        }

    //        // 착지 스냅 & 정리
    //        _rigid.position = new Vector2(_rigid.position.x, groundSlamOriginY);
    //        _rigid.linearVelocity = Vector2.zero;
    //        isGroundSlamming = false;
    //        DisableHitbox();

    //        Debug.Log($"[Met] GroundSlam Landed #{i + 1}");

    //        // 마지막 회차에만 Dash_End 호출
    //        if (i == 1)
    //        {
    //            _visual.PlayAnim("Rolling_End");
    //            yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);
    //        }
    //        else
    //        {
    //            // 첫 번째 착지 후 짧은 딜레이
    //            yield return new WaitForSeconds(0.4f);
    //        }
    //    }
    //    KillGroundSlamTween();
    //    lastGroundSlamTime = Time.time;
    //    yield return new WaitForSeconds(0.2f);//Status.defultWindDownTime
    //}
    
    //private IEnumerator Co_MoveToTarget(Vector2 target, float duration, Ease ease)
    //{
    //    bool finished = false;

    //    KillGroundSlamTween();

    //    _groundSlamTween = _rigid
    //        .DOMove(target, duration)
    //        .SetEase(ease)
    //        .SetUpdate(UpdateType.Fixed)   // Rigidbody2D와 맞추기
    //        .OnComplete(() => finished = true);

    //    while (!finished)
    //    {
    //        if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
    //        {
    //            KillGroundSlamTween();
    //            yield break;
    //        }

    //        // tween이 외부에서 Kill된 경우 무한루프 방지
    //        if (_groundSlamTween == null || !_groundSlamTween.IsActive())
    //            yield break;

    //        yield return null;
    //    }

    //    KillGroundSlamTween();
    //}
    //private void KillGroundSlamTween()
    //{
    //    if (_groundSlamTween != null && _groundSlamTween.IsActive())
    //    {
    //        _groundSlamTween.Kill();
    //    }

    //    _groundSlamTween = null;
    //}
    //public void OnGroundSlamHit(Player player)
    //{
    //    if (hitGroundSlam) return;
    //    hitGroundSlam = true;

    //    Debug.Log("[Met] GroundSlam Player Hit!");

    //    // 내려찍기이므로 아래→위 방향 넉백 + 약간의 수평
    //    Vector2 hitDir = new Vector2(facingX * 0.2f, 1f).normalized;
    //    Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.groundSlamKnockback);

    //    if (guard == Player.GuardType.PerfectGuard)
    //    {
    //        Debug.Log("[Met] GroundSlam Perfect Guarded!");
    //        StartExhausted();
    //    }
    //    else if (guard == Player.GuardType.Guard)
    //    {
    //        Debug.Log("[Met] GroundSlam Guarded.");
    //        player.controller.OnKnockback(hitDir, Status.groundSlamKnockback * 0.5f);
    //    }
    //    else
    //    {
    //        player.TakeDamaged(Status.groundSlamDamage);
    //    }
    //}
    #endregion

    #region 앞발찍기

    private IEnumerator Co_StampDrive()
    {
        Player player = Player.Instance;
        if (player == null) yield break;

        // 시작 시 Y위치 저장 (착지 기준점)
        groundSlamOriginY = _rigid.position.y;
        float temporalJumpHeight = Status.groundSlamJumpHeight;
        hitStamp = false;
        isStamping = false;


        // 패턴 시작 애니메이션
        _visual.PlayAnim("Stamp_Prepare");
        if (curAttack == AttackPattern.None) yield break;
        float Co_StampMoveDuration = 0.3f;
        // 2회 반복
        for (int i = 0; i < 2; i++)
        {
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun) yield break;

            // ── 올라가기 ──────────────────────────────────────────
            // 목표 X: 보스와 플레이어 사이의 3/4 지점
            
            float targetX = _rigid.position.x + (player.transform.position.x - _rigid.position.x) * 0.75f;
            float targetY = groundSlamOriginY + temporalJumpHeight;
            Vector2 jumpTarget = new Vector2(targetX, targetY);
            
            Debug.Log($"[Met] GroundSlam Jump Up #{i + 1} → {jumpTarget}");

            yield return StartCoroutine(Co_StampMove(
            jumpTarget,
            Co_StampMoveDuration,   //  0.2f ~ 0.3f
            Ease.OutQuad                     // 처음 빠르게 치고 올라감
            ));
            
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
            {
                KillStampDrive();
                yield break;
            }

            _rigid.position = jumpTarget;
            _rigid.linearVelocity = Vector2.zero;
            
            // ── 공중 대기 1초 ─────────────────────────────────────
            Debug.Log("[Met] GroundSlam Hovering...");
            if (i != 1)// 0회차만
            {
                yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);
                yield return new WaitForSeconds(0.5f); 
            }
                
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
            {
                KillStampDrive();
                yield break;
            }

            // ── 내려찍기 (플레이어 방향 대각선 낙하) ──────────────
            
            LookAtPlayer();
            // 낙하 시작 시점 플레이어 위치 스냅
            Vector2 slamTarget = new Vector2(player.transform.position.x, groundSlamOriginY);
            hitStamp = false;
            isStamping = true;
            EnableHitbox();// TODO 여기에 히트박스 겸 이펙트 

            Co_StampMoveDuration -= 0.1f;
            temporalJumpHeight -= 3f;

            yield return StartCoroutine(Co_StampMove(
            slamTarget,
            0.3f,   //  0.3f ~ 0.45f
            Ease.InQuad                      // 처음 느리다가 점점 빠르게

            ));


            // 중간에 끊기면 정리 후 종료
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
            {
                isStamping = false;
                DisableHitbox();
                KillStampDrive();
                yield break;
            }

            // 착지 스냅 & 정리
            _rigid.position = new Vector2(_rigid.position.x, groundSlamOriginY);
            _rigid.linearVelocity = Vector2.zero;
            isStamping = false;
            DisableHitbox();

            Debug.Log($"[Met] GroundSlam Landed #{i + 1}");

            // 마지막 회차에만 Dash_End 호출
            if (i == 1)
            {
                _visual.PlayAnim("Stamp_Action", false);
                //yield return new WaitUntil(() => _visual.IsAnimFinished || curAttack == AttackPattern.None);
            }
            else
            {
                // 첫 번째 착지 후 짧은 딜레이
                yield return new WaitForSeconds(0.8f);
            }
        }
        KillStampDrive();
        lastStampTime = Time.time;
        Debug.Log("스탬프 드라이브 끝");
        //yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator Co_StampMove(Vector2 target, float duration, Ease ease)
    {
        bool finished = false;
        _visual.PlayAnim("Stamp_Action", true);
        KillStampDrive();

        _groundSlamTween = _rigid
            .DOMove(target, duration)
            .SetEase(ease)
            .SetUpdate(UpdateType.Fixed)   // Rigidbody2D와 맞추기
            .OnComplete(() => finished = true);

        while (!finished)
        {
            if (curAttack == AttackPattern.None || CurState == EnemyState.Stun)
            {
                KillStampDrive();
                yield break;
            }

            // tween이 외부에서 Kill된 경우 무한루프 방지
            if (_groundSlamTween == null || !_groundSlamTween.IsActive())
                yield break;

            yield return null;
        }

        KillStampDrive();
    }

    private void KillStampDrive()
    {
        if (_groundSlamTween != null && _groundSlamTween.IsActive())
        {
            _groundSlamTween.Kill();
        }

        _groundSlamTween = null;
    }
    

    public void OnStampHit(Player player)
    {
        if (hitStamp) return;
        hitStamp = true;

        // 내려찍기이므로 아래→위 방향 넉백
        Vector2 hitDir = new Vector2(facingX , 0.5f).normalized;
        Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.stampKnockback);

        if (guard == Player.GuardType.PerfectGuard)
        {
            Debug.Log("[Met] Stamp Perfect Guarded!");
            StartExhausted();
        }
        else if (guard == Player.GuardType.Guard)
        {
            Debug.Log("[Met] Stamp Guarded.");
            player.controller.OnKnockback(hitDir, Status.stampGuardKnockback);
        }
        else
        {
            Debug.Log("[Met] Stamp Player Hit!");
            player.TakeDamaged(Status.stampDamage);
        }
    }

    #endregion

    #endregion

    #region 탐지
    public override bool IsDetect()
    {
        Player player = Player.Instance;
        if (player == null) return false;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        return dist <= Status.detectRange;
    }
    #endregion

    #region 기절
    protected override void StartExhausted()
    {
        Debug.Log("기절 시작");
        curAttack = AttackPattern.None;
        
        Visual?.AE_AnimFinished(); // 패턴 애니메이션 강제 종료
        Visual?.ResetAnimTrigger("Motion_Reset");
        Visual?.PlayAnim("Dash_Action", false); // 돌진 애니메이션을 멈추고 멈춰있는 포즈로 전환
        isDashing = false;
        isTusk = false;
        isSlam = false;
        isGroundSlamming = false;
        isExhausted = true;
        
        base.StartExhausted();
        _visual?.PlayAnim("IsStuned"); // 기절 애니메이션
        EnableHitbox(); //기절 상태 히트박스 On
    }

    protected override void EndExhausted()
    {
        Debug.Log("기절 끝");
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        base.EndExhausted();
        DisableHitbox(); //기절 상태 히트박스 OFf
        Visual?.OffStunVisual();
        Visual?.PlayAnim("Motion_Reset");
        isRecovering = true;
        StartCoroutine(Co_WaitRecovery());
    }

    private IEnumerator Co_WaitRecovery()
    {
        yield return new WaitUntil(() => Visual.IsAnimFinished);
        isRecovering = false;
    }

    void enterPhaseChangeStatus()
    {
        isRecovering = false;
    }

    //기절 후 일어날 때 플레이어 넉백
    public void ShoutKnockback()
    {
        Player player = Player.Instance;
        //여기에 울리는 이펙트 필요
        fullscreenShockwaveController.PlayAtWorld(headPoint.position);
        Vector2 direction = player.transform.position - transform.position;
        direction.y = 0f;
        direction = direction.normalized;
        
        player.controller.OnKnockback(direction, 30f);
    }
    #endregion

    #region 피격
    public override void TakeDamage(int damage)
    {
        if (isPhaseTransitioning) return;

        // 스턴 상태시 데미지 배율 적용
        if (CurState == EnemyState.Stun)
        {
            damage = Mathf.RoundToInt(damage * Status.exhaustedDamageMulti);
        }

        base.TakeDamage(damage);
        Visual?.PlayHitEffect();

        if (CurState == EnemyState.Stun)
        {
            curHits++;
            if (curHits >= Status.exhaustedHitLimit)
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    protected override void Die()
    {
        isDead = true;

        curState = EnemyState.Dead;
        // TODO: 스테이지 매니저 보스 사망 처리
        Visual?.PlayAnim("IsBool", true);
    }


    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Player player = Player.Instance;
            if (player == null) return;

            // 공격 패턴에 따른 전용 히트 함수 호출
            switch (curAttack)
            {
                case AttackPattern.PowerDash:   OnDashHit(player); break;
                case AttackPattern.TuskDrive:   OnTuskHit(player); break;
                case AttackPattern.BodySlam:    OnSlamHit(player); break;
                //case AttackPattern.GroundSlam: OnGroundSlamHit(player); break;
                case AttackPattern.StampDrive: OnStampHit(player); break;
                default: base.OnCollisionEnter2D(collision); break;
            }

            // 중복 충돌 방지
            StartCoroutine(Co_IgnorePlayer(player, 1f));
        }
        else
        {
            base.OnCollisionEnter2D(collision);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead || isPhaseTransitioning) return;

        if (collision.CompareTag("Player"))
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

                TakeDamage(1);
                return;
            }
        }
    }
    #endregion

    #region 페이즈 전환
    protected override IEnumerator Co_PhaseTransition()
    {
        isPhaseTransitioning = true;

        if (patternCoroutine != null) StopCoroutine(patternCoroutine);

        ChangeState(EnemyState.Stun);
        stateTimer = 30f;
        Visual?.PlayPhaseChange();
        yield return new WaitUntil(() => Visual.IsAnimFinished);
        yield return new WaitForSeconds(Status.phaseTransitionDelay);

        phase = 2;
        isPhaseTransitioning = false;
        ChangeState(EnemyState.Idle);
    }
    #endregion

    // 공격 판정 콜라이더 (애니메이션 이벤트에서 활성화/비활성화)
    [SerializeField] private Collider2D attackCollider;
    public void EnableHitbox()
    {
        Debug.Log("히트박스 활성화");
        attackCollider.enabled = true;
    }

    public void DisableHitbox()
    {
        Debug.Log("히트박스 비활성화");
        attackCollider.enabled = false;
    }
    #region 디버깅
    protected override void OnDrawGizmos()
    {
        // Status 데이터가 없으면 그리지 않음
        if (Status == null) return;

        Vector2 pos = transform.position;
        float labelOffset = 0.5f;
        float drawDir = Application.isPlaying ? facingX : (transform.localScale.x > 0 ? -1f : 1f);

        #region 1. 탐지 및 판단 범위
        // 탐지 거리 (Detect Range)
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(pos, Status.detectRange);
        

        // 패턴 선택 기준 거리 (Melee Range)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, Status.meleeRange);

#if UNITY_EDITOR
        Handles.Label(pos + Vector2.up * (Status.detectRange + labelOffset), "Detect Range", GetLabelStyle(Color.gray));
        Handles.Label(pos + Vector2.up * (Status.meleeRange + labelOffset * 2f), "Melee Range (Pattern Selector)", GetLabelStyle(Color.red));
#endif
#endregion

        #region 2. 공격 범위
        #region Power Dash
        Gizmos.color = Color.gray;
        Vector2 rayOrigin = pos - Vector2.up;
        Vector2 rayEnd = rayOrigin + Vector2.right * drawDir * Status.wallDetectRange;
        Gizmos.DrawLine(rayOrigin, rayEnd);
        Gizmos.DrawSphere(rayEnd, 0.1f);

        
        #endregion

        #region Tusk Drive
        Gizmos.color = Color.purple;
        Vector2 tuskCenter = pos + new Vector2(drawDir * Status.tuskRange * 0.5f, 2f);
        Gizmos.DrawWireCube(tuskCenter, new Vector3(Status.tuskRange, 3f, 0));

#endregion

        #region Body Slam
        float bossHeight = (_coll != null) ? _coll.bounds.size.y : 2.0f;

        Vector2 slamBoxCenter = pos + new Vector2(drawDir * Status.slamRange * 0.5f, bossHeight * 0.5f);
        Vector2 slamBoxSize = new Vector2(Status.slamRange, bossHeight);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(slamBoxCenter, slamBoxSize);
#if UNITY_EDITOR
        // 레이 출발점 텍스트
        Handles.Label(rayEnd - Vector2.up, "Wall Sensor", GetLabelStyle(Color.gray));
        Handles.Label(tuskCenter + Vector2.up, "Tusk Sweep Range", GetLabelStyle(Color.purple));
        Handles.Label(slamBoxCenter + Vector2.up, $"Slam Path ({Status.slamRange}m)", GetLabelStyle(Color.red));
#endif
        #endregion
        #endregion

        #region 3. 스테이지 범위
        float gizmoVerticalLength = 10f; // 수직선 길이
        float centerLineY = transform.position.y;

        float parentX = (transform.parent != null) ? transform.parent.position.x : 0f;
        float worldLeftX = parentX + stageLeftX;
        float worldRightX = parentX + stageRightX;

        Gizmos.color = Color.black;
        Gizmos.DrawLine(new Vector3(worldLeftX, centerLineY - gizmoVerticalLength), new Vector3(worldLeftX, centerLineY + gizmoVerticalLength));
        Gizmos.DrawLine(new Vector3(worldRightX, centerLineY - gizmoVerticalLength), new Vector3(worldRightX, centerLineY + gizmoVerticalLength));

        // 2. 오프셋이 적용된 보정 위치 (로컬 값에 부모 월드 X를 더함)
        Vector3 boxSize = new Vector3(2f, 6f, 1f); // 박스 크기 살짝 조정

        // 왼쪽 보정 지점 박스
        Vector3 leftTargetPos = new Vector3(parentX + stageLeftX + stageOffsetX, centerLineY, 0);
        Gizmos.color = new Color(0, 0, 0, 0.3f); // 반투명 검은색
        Gizmos.DrawWireCube(leftTargetPos, boxSize);

        // 오른쪽 보정 지점 박스
        Vector3 rightTargetPos = new Vector3(parentX + stageRightX - stageOffsetX, centerLineY, 0);
        Gizmos.DrawWireCube(rightTargetPos, boxSize);

        // 레이블 위치 업데이트
#if UNITY_EDITOR
        UnityEditor.Handles.Label(new Vector3(worldLeftX, centerLineY + gizmoVerticalLength + 1f), "Stage Left (World)", GetLabelStyle(Color.black));
        UnityEditor.Handles.Label(new Vector3(worldRightX, centerLineY + gizmoVerticalLength + 1f), "Stage Right (World)", GetLabelStyle(Color.black));
#endif
        #endregion
    }

#if UNITY_EDITOR
    /// <summary> 기즈모 라벨 스타일 설정 </summary>
    private GUIStyle GetLabelStyle(Color color)
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 11;
        return style;
    }
#endif
    #endregion
}
