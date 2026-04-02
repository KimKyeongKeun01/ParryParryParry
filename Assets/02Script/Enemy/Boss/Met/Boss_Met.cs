using System.Collections;
using UnityEditor;
using UnityEngine;

public class Boss_Met : BaseBoss
{
    public enum AttackPattern { None, PowerDash, TuskDrive, BodySlam }

    private BossStatus_Met Status => (BossStatus_Met)_status;
    private BossVisual_Met Visual => (BossVisual_Met)_visual;

    [Header(" === Boss Met === ")]
    [Header("Attack State Machine")]
    [SerializeField] private AttackPattern curAttack = AttackPattern.None;
    [SerializeField] protected float attackTimer = 0f;

    [Header("Power Dash")]
    [SerializeField] private bool hitDash = false;
    [SerializeField] private bool isDashing = false;

    [Header("Tusk Drive")]
    [SerializeField] private bool hitTusk = false;
    [SerializeField] private bool isTusk = false;

    [Header("Body Slam")]
    [SerializeField] private bool hitSlam = false;
    [SerializeField] private bool isSlam = false;

    // 패턴 쿨타임 타이머
    private float lastDashTime = -99f;
    private float lastTuskTime = -99f;
    private float lastSlamTime = -99f;

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
        if (isDead) return;

        // 스턴 시 패턴 초기화
        if (CurState == EnemyState.Stun)
        {
            curAttack = AttackPattern.None;
            isDashing = false;
            isTusk = false;
            isSlam = false;
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

        if (!isPhaseTransitioning) CheckPhase();
        base.Update();
    }

    #region 패턴 관리
    
    protected override int SelectNextPattern()
    {
        Player player = Player.Instance;
        if (player == null) return -1;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        float curTime = Time.time;

        bool canDash = curTime - lastDashTime >= Status.dashCooldown;
        bool canTusk = curTime - lastTuskTime >= Status.tuskCooldown;
        bool canSlam = curTime - lastSlamTime >= Status.slamCooldown;

        AttackPattern next = AttackPattern.None;

        // [우선순위 1] 멀리 있으면 무조건 돌진
        if (dist > Status.meleeRange && canDash)
        {
            next = AttackPattern.PowerDash;
        }
        // [우선순위 2] 근접 상황
        else if (dist <= Status.meleeRange)
        {
            if (canTusk && Random.value > 0.5f) next = AttackPattern.TuskDrive;
            else if (canSlam) next = AttackPattern.BodySlam;
            else if (canDash) next = AttackPattern.PowerDash;
        }

        if (next == AttackPattern.None) return -1;
        return (int)next - 1;
    }

    protected override IEnumerator Co_StartPattern(int index)
    {
        // 상태 초기화
        Visual?.ResetAnimTrigger("Motion_Reset");

        // 시작 시 플레이어 방향 보기
        LookAtPlayer();

        curAttack = (AttackPattern)(index + 1);
        attackTimer = 10f; // 패턴 타임아웃

        switch (curAttack)
        {
            case AttackPattern.PowerDash:   yield return Co_PowerDash();    break;
            case AttackPattern.TuskDrive:   yield return Co_TuskDrive();    break;
            case AttackPattern.BodySlam:    yield return Co_BodySlam();     break;
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
    }

    #endregion

    #region 공격
    private void StopAttack()
    {
        curAttack = AttackPattern.None;
        isDashing = false;
        isTusk = false;
        isSlam = false;

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
        yield return StartCoroutine(Co_MoveToRange(Status.meleeRange, Status.maxApproachTime));

        _visual.PlayAnim("Slam_Action"); // 뒤로 뺐다 치기
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
        _visual.PlayAnim("Motion_Reset");
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
        curAttack = AttackPattern.None;
        Visual?.AE_AnimFinished(); // 패턴 애니메이션 강제 종료
        Visual?.ResetAnimTrigger("Motion_Reset");
        Visual?.PlayAnim("Dash_Action", false); // 돌진 애니메이션을 멈추고 멈춰있는 포즈로 전환
        isDashing = false;
        isTusk = false;
        isSlam = false;
        
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        base.StartExhausted();
        _visual?.PlayAnim("IsStuned"); // 기절 애니메이션
    }

    protected override void EndExhausted()
    {
        _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocityY);
        base.EndExhausted();

        Visual?.PlayAnim("Motion_Reset");
        Visual?.OffStunVisual();
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
