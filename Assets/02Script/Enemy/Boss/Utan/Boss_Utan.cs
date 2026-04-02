using System.Collections;
using UnityEditor;
using UnityEngine;

public class Boss_Utan : BaseBoss
{
    public enum AttackPattern { None, ArmSmash, RockThrow, Swing }

    private BossStatus_Utan Status => (BossStatus_Utan)_status;
    private BossVisual_Utan Visual => (BossVisual_Utan)_visual;


    
    [Header(" === Boss Utan === ")]
    [Header("Attack State Machine")]
    [SerializeField] private AttackPattern curAttack = AttackPattern.None;
    [SerializeField] protected float attackTimer = 0f;

    [Header("Arm Smash")]
    public Utan_Arms arms;

    [Header("Rock Throw")]
    public Transform rockSpawnPoint;
    public GameObject rockPrefab;

    [Header("Swing")]
    public Vector2 swingPosition;
    private const float GROUND_Y_OFFSET = 1.94f;

    // 패턴 타이머
    private float lastArmSmashTime = -99f;
    private float lastRockTime = -99f;
    private float lastSwingTime = -99f;

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

        if (CurState == EnemyState.Stun)
        {
            curAttack = AttackPattern.None;
            attackTimer = 0f;
            base.Update();
            return;
        }

        // Base State 변경시 Attack State 초기화
        if (CurState != EnemyState.Attack)
        {
            curAttack = AttackPattern.None;
        }

        #region Attack State Timer
        // Attack State 활성화시 타이머
        if (curAttack != AttackPattern.None)
        {
            attackTimer -= Time.deltaTime;

            if (attackTimer <= 0)
            {
                curAttack = AttackPattern.None;
                Visual?.AE_AnimFinished();
                StopPattern();
                ChangeState(EnemyState.Idle);
            }
        }
        #endregion

        // 1. 실시간 페이즈 체크
        if (!isPhaseTransitioning) CheckPhase();

        // 2. 상태 머신 업데이트
        base.Update();
    }

    #region 패턴 관리
    /// <summary> 이번에 실행할 공격 패턴 선택 메소드 </summary>
    protected override int SelectNextPattern()
    {
        Player player = Player.Instance;
        if (player == null) return -1;

        // 현재 거리 판단
        float dist = Vector2.Distance(transform.position, Player.Instance.transform.position);
        float curTime = Time.time;

        // 공격 패턴 쿨타임 체크
        bool canArm = curTime - lastArmSmashTime >= Status.armSmashCooldown;
        bool canRock = curTime - lastRockTime >= Status.rockThrowCooldown;
        bool canSwing = curTime - lastSwingTime >= Status.swingCooldown;

        AttackPattern nextPattern = AttackPattern.None;

        // [우선순위 1] 2페이즈 특수 로직: 멀리 있으면 스윙 시도
        if (phase >= 2 && canSwing && dist > Status.meleeRange * 0.7f)
        {
            nextPattern = AttackPattern.Swing;
        }
        // [우선순위 2] 근접 상황
        else if (dist <= Status.meleeRange && canArm)
        {
            nextPattern = AttackPattern.ArmSmash;
        }
        // [우선순위 3] 원거리 상황
        else if (dist >= Status.midRange && canRock)
        {
            nextPattern = AttackPattern.RockThrow;
        }
        // [우선순위 4] Fallback (남는 패턴 중 가능한 것 실행)
        else
        {
            if (canRock) nextPattern = AttackPattern.RockThrow;
            else if (canSwing) nextPattern = AttackPattern.Swing;
            else if (canArm) nextPattern = AttackPattern.ArmSmash;
        }

        // 3. 결과 반환
        if (nextPattern == AttackPattern.None) return -1;

        // Enum 값을 인덱스로 변환 (None이 0이므로, 패턴 인덱스는 Enum값 - 1)
        // Co_StartPattern에서 (index + 1)로 다시 Enum화 하기 때문에 이 방식이 안전함
        return (int)nextPattern - 1;
    }

    /// <summary> 선택된 공격 패턴 실행 메소드 </summary>
    protected override IEnumerator Co_StartPattern(int index)
    {
        Visual?.ResetAnimTrigger("Motion_Reset");

        // 패턴 시작 시 플레이어 방향 보기
        float lookDir = Mathf.Sign(Player.Instance.transform.position.x - transform.position.x);
        Visual.Flip(lookDir > 0);

        curAttack = (AttackPattern)(index + 1);
        attackTimer = 10f;

        switch (curAttack)
        {
            case AttackPattern.ArmSmash:    yield return Co_ArmSmash();     break;
            case AttackPattern.RockThrow:   yield return Co_RockThrow();    break;
            case AttackPattern.Swing:       yield return Co_Swing();        break;
        }

        // 3. 패턴 종료 후 상태 리셋
        curAttack = AttackPattern.None;
        attackTimer = 0f;
    }

    protected override float GetPatternInterval()
    {
        float interval = _status.patternInterval;

        if (phase >= 2)
            interval *= Status.phase2PatternIntervalMulti;

        return interval;
    }
    #endregion

    #region 대기/이동
    public override void OnIdle()
    {
        Visual?.PlayAnim("IsMoving", false);
    }

    /// <summary> 플레이어를 향해 이동하는 메소드 </summary>
    public override void OnMove()
    {
        Player player = Player.Instance;
        if (player == null) { ChangeState(EnemyState.Idle); return; }

        // 1. 방향 계산
        float dirX = player.transform.position.x - transform.position.x;
        float moveDir = Mathf.Sign(dirX);

        // 2. 비주얼
        LookAtPlayer();
        Visual?.PlayAnim("IsMoving", true);

        // 3. 이동
        Vector2 nextPos = _rigid.position + Vector2.right * moveDir * Status.moveSpeed * Time.deltaTime;
        _rigid.MovePosition(nextPos);
        facingX = moveDir;
    }

    /// <summary> 이동 정지 메소드 </summary>
    private void StopMove()
    {
        Visual?.PlayAnim("IsMoving", false);
    }

    /// <summary> 플레이어를 향해 보는 메소드 </summary>
    private void LookAtPlayer()
    {
        Player player = Player.Instance;
        if (player == null) return;

        float dirX = player.transform.position.x - transform.position.x;

        // 정확히 겹치면 기존 facing 유지
        if (Mathf.Abs(dirX) < 0.01f)
            dirX = facingX;

        Visual?.Flip(dirX > 0f);
    }


    /// <summary> 공격 직전 플레이어를 향해 이동하는 메소드 </summary>
    private IEnumerator Co_MoveToRange(float stopDistance, float maxTime)
    {
        Player player = Player.Instance;
        if (player == null) yield break;

        // 플레이어 위치 지정
        float dx = player.transform.position.x - transform.position.x;
        float moveDir = Mathf.Sign(dx);
        float elapsed = 0f;

        // 비주얼
        LookAtPlayer();
        Visual?.PlayAnim("IsMoving", true);

        // 이동
        while (elapsed < maxTime)
        {
            player = Player.Instance;
            if (player == null) yield break;

            if (Mathf.Abs(dx) <= stopDistance) break;

            Vector2 nextPos = _rigid.position + Vector2.right * moveDir * Status.moveSpeed * Time.deltaTime;
            _rigid.MovePosition(nextPos);
            facingX = moveDir;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정지
        StopMove();
    }
    #endregion

    #region 공격
    public override void OnAttack()
    {
        if (patternCoroutine != null) return;

        patternCoroutine = StartCoroutine(Co_PatternCycle());
    }

    #region 양팔 휘두르기
    private IEnumerator Co_ArmSmash()
    {
        Debug.Log("[Utan] Arm Smash!");
        yield return StartCoroutine(Co_MoveToRange(Status.armSmashStopDistance, Status.maxApproachTime));

        yield return new WaitForSeconds(Status.defultWindUpTime);
        Visual?.PlayAnim("ArmSmash_Prepare");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);

        // 패턴 안전장치
        if (curAttack == AttackPattern.None) yield break;

        Visual?.PlayAnim("ArmSmash_Action");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);
        yield return new WaitForSeconds(Status.defultWindDownTime);

        StopMove();
        lastArmSmashTime = Time.time;
    }

    public void AE_SmashEnable()
    {
        arms.SetAttack(true);
    }

    public void AE_SmashDisable()
    {
        arms.SetAttack(false);
    }

    public void OnArmHit()
    {
        if (CurState == EnemyState.Stun) return;

        Player player = Player.Instance;
        if (player == null) return;

        // 1. 플레이어 가드 확인
        Vector2 hitDir = new Vector2(facingX, 0.2f).normalized;
        Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.armSmashKnockback);


        if (guard == Player.GuardType.PerfectGuard)
        {
            AE_SmashDisable();

            // 퍼펙트 가드 성공 시 보스 기절
            Debug.Log("[Utan] Attack Pattern: Arm Smash, Player: Perfect Guarded!");
            StartExhausted();
            StartCoroutine(Co_IgnorePlayer(player, 0.2f));
        }
        else if (guard == Player.GuardType.Guard)
        {
            AE_SmashDisable();

            // 일반 가드 시 데미지 없음 (효과음이나 이펙트 추가 가능)
            Debug.Log("[Utan] Attack Pattern: Arm Smash, Player: Guarded!");
            StartCoroutine(Co_IgnorePlayer(player, 0.2f));
        }
        else // 노멀 히트
        {
            AE_SmashDisable();

            StartCoroutine(Co_IgnorePlayer(player, 1f)); // 중복 충돌 방지

            Debug.Log("[Utan] Attack Pattern: Arm Smash, Player: Hit!");
            player.TakeDamaged(Status.armSmashDamage); // Status에 데미지 설정이 있다고 가정
        }
    }
    #endregion

    #region 암석 던지기
    private IEnumerator Co_RockThrow()
    {
        Debug.Log("[Utan] Rock Throw!");
        LookAtPlayer();

        yield return new WaitForSeconds(Status.defultWindUpTime);
        Visual?.PlayAnim("RockThrow_Action");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);

        // 패턴 안전장치
        if (curAttack == AttackPattern.None) yield break;

        // 실제 돌 투척은 Aniamtor에서 ThrowRock 메소드를 Event로 연결

        Visual?.PlayAnim("Motion_Reset");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);
        yield return new WaitForSeconds(Status.defultWindDownTime);
        StopMove();
        lastRockTime = Time.time;
    }

    private GameObject currentRock;

    public void AE_SpawnRock()
    {
        if (rockPrefab == null || rockSpawnPoint == null) return;

        // 1. 암석 소환
        currentRock = Instantiate(rockPrefab, rockSpawnPoint.position, Quaternion.identity);
        currentRock.transform.SetParent(rockSpawnPoint.transform);

        // 2. 물리 정지
        BaseProjectile rock = currentRock.GetComponent<BaseProjectile>();
        rock.SetGravity(false);
        rock.SetGround(false);
        
        Debug.Log("[Utan] Rock Spawned");
    }

    public void AE_ThrowRock()
    {
        if (currentRock == null) return;
        BaseProjectile rock = currentRock.GetComponent<BaseProjectile>();

        // 1. 손에서 떼기 
        currentRock.transform.SetParent(null);

        // 2. 발사 
        if (rock != null)
        {
            #region 물리 계산
            // 위치 계산
            Vector2 startPos = currentRock.transform.position;
            Vector2 targetPos = Player.Instance.transform.position;

            // X축 거리 및 방향
            float dx = targetPos.x - startPos.x;
            float dy = targetPos.y - startPos.y;
            float direction = Mathf.Sign(dx);

            // [핵심] 수평 속도를 Status의 rockSpeed로 고정
            float vx = direction * Status.rockSpeed;

            // 1. 목표 지점까지 도달하는 데 걸리는 전체 시간 (T = 거리 / 속도)
            float totalTime = Mathf.Abs(dx) / Status.rockSpeed;
            if (totalTime < 0.1f) totalTime = 0.1f; // 최소 시간 보장

            // 2. Apex(최고점) 높이 설정
            float h = Mathf.Max(0.5f, Status.rockApexOffset); // 시작점 기준 상대적 높이

            // 3. 수직 초속도(vy)와 중력(g) 강제 계산
            // 공식: h = (vy^2) / (2g)  &&  dy = vy*T - 0.5*g*T^2
            // 위 두 식을 연립하여 g와 vy를 도출
            float g = (2 * h - dy + 2 * Mathf.Sqrt(h * h - h * dy)) / (0.5f * totalTime * totalTime);
            float vy = Mathf.Sqrt(2 * g * h);

            // 4. 계산된 중력을 Rigidbody에 강제 적용
            float gravityScale = g / Mathf.Abs(Physics2D.gravity.y);
            #endregion

            // 물리 활성화
            rock.SetGravity(true, gravityScale);
            
            // 5. 최종 속도 벡터 생성 및 Init
            Vector2 launchVelocity = new Vector2(vx, vy);
            rock.Init(launchVelocity.normalized, Status.rockDamage, launchVelocity.magnitude);
            rock.GetComponent<Rigidbody2D>().linearVelocity = launchVelocity;
            rock.SetGround(true);
        }

        currentRock = null;
        Debug.Log("[Utan] Rock Thrown");
    }
    #endregion

    #region 나무타기
    private float originY;

    private IEnumerator Co_Swing()
    {
        Debug.Log("[Utan] Vine Swing Start!");
        Player player = Player.Instance;
        if (player == null) yield break;
        LookAtPlayer();

        // 상태 세팅
        attackTimer = 30f;

        // 나무 위치 설정
        float startSide = -Mathf.Sign(player.transform.position.x - transform.position.x);
        swingPosition = new Vector2(player.transform.position.x, transform.position.y + Status.swingHeight);
        float lopeLength = Status.swingLopeLength;
        originY = transform.position.y;

        // 나무 오르기
        Visual?.PlayAnim("Swing_Prepare");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);
        if (curAttack == AttackPattern.None) { ResetSwing(); yield break; }

        // 실제 위치 이동
        Vector2 startPos = swingPosition + new Vector2(startSide * lopeLength, 5f);
        _rigid.MovePosition(startPos);
        transform.position = startPos;

        Visual?.PlayAnim("Swing_Action");
        Debug.Log("[Utan] Swing!");

        #region 나무 타기
        float radius = Vector2.Distance(transform.position, swingPosition);
        float currentAngle = Mathf.Atan2(transform.position.y - swingPosition.y, transform.position.x - swingPosition.x) * Mathf.Rad2Deg;

        float startAngle = currentAngle;
        float totalRotated = 0f;
        float targetRotation = 200f; // 총 200도 회전

        while (totalRotated < targetRotation)
        {
            if (curAttack == AttackPattern.None || CurState != EnemyState.Attack)
            {
                ResetSwing();
                yield break;
            }
            
            
            // swingSpeed를 각속도로 사용하여 각도 업데이트
            // (speed가 높을수록 초당 더 많은 각도를 회전)
            float angleStep = Status.swingSpeed * 10f * Time.deltaTime; // 10f는 감도 조절용 상수
            totalRotated += angleStep;

            // 오른쪽에서 왼쪽으로 스윙하므로 각도를 감소시킴
            currentAngle += angleStep * -startSide;

            // 새로운 위치 계산 (각운동 공식)
            float rad = currentAngle * Mathf.Deg2Rad;
            float nextX = swingPosition.x + Mathf.Cos(rad) * radius;
            float nextY = swingPosition.y + Mathf.Sin(rad) * radius;

            transform.position = new Vector2(nextX, nextY);

            // 진행 방향에 따라 몸체 회전 (선택 사항: 투사체처럼 회전시키고 싶을 때)
            float lookAngle = currentAngle + 90f; // 수직 방향 보정
            transform.rotation = Quaternion.Euler(0, 0, lookAngle);

            yield return null;
        }
        #endregion

        Debug.Log("[Utan] Vine Swing End");

        // 나무에서 착지
        transform.rotation = Quaternion.identity;
        transform.position = new Vector2(transform.position.x, 27.2f);      // 레전드 하드코딩 어쩔수가 없다.

        ValidateStage();    // 착지 위치 x값 보정
        Visual.PlayAnim("Motion_Reset");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);
        yield return new WaitForSeconds(Status.defultWindDownTime);

        // 상태 초기화
        ResetSwing();
        lastSwingTime = Time.time;
    }

    private void ResetSwing()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector2(transform.position.x, originY);
    }

    public void OnSwingHit(Player player)
    {
        if (player == null) return;

        // 넉백 방향
        Vector2 hitDir = new Vector2(facingX, 0.5f).normalized;
        Player.GuardType guard = player.controller.OnKnockback(hitDir, Status.swingKnockback);

        // 판정
        if (guard == Player.GuardType.PerfectGuard)
        {
            Debug.Log("[Utan] Attack Pattern: Swing, Player: Perfect Guarded!");

            // 기절
            StartExhausted();

            // 상태 초기화
            ResetSwing();
        }
        else if (guard == Player.GuardType.Guard)
        {
            Debug.Log("[Utan] Attack Pattern: Swing, Player: Guarded!");
            // 가드 시 데미지 없음
        }
        else
        {
            Debug.Log("[Utan] Attack Pattern: Swing, Player: Hit!");
            player.TakeDamaged(Status.swingDamage);
        }
    }
    #endregion
    #endregion

    public override bool IsDetect()
    {
        Player player = Player.Instance;
        if (player == null) return false;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        return dist <= Status.detectRange;
    }

    #region 기절
    protected override void StartExhausted()
    {
        AE_SmashDisable();

        curAttack = AttackPattern.None;
        base.StartExhausted();
        Visual?.PlayAnim("IsStuned");
    }

    protected override void EndExhausted()
    {
        Visual?.OffStunVisual();
        Visual?.PlayAnim("Motion_Reset");
        
        base.EndExhausted();
    }

    private IEnumerator Co_StunBreakSequence()
    {
        // 여기서 바로 Idle로 가지 않고, 잠시 Stun 상태를 유지해서 몸뎀 판정을 막음
        yield return new WaitForSeconds(0.2f);

        if (CurState == EnemyState.Stun) // 아직 스턴 상태라면 (중복 호출 방지)
        {
            EndExhausted();
            ChangeState(EnemyState.Idle);
        }
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
            Debug.Log($"[Utan] Stun Hit Count: {curHits} / {stunHitLimit}");
            if (curHits >= Status.exhaustedHitLimit)
            {
                Debug.Log("[Utan] Stun Ended by Hit Limit");

                StartCoroutine(Co_StunBreakSequence());
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
        if (isDead || isPhaseTransitioning) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Player player = Player.Instance;
            if (player == null) return;

            // 스윙 패턴 충돌
            if (curAttack == AttackPattern.Swing)
            {
                StartCoroutine(Co_IgnorePlayer(player, 1f));
                OnSwingHit(player);
                return;
            }

            // 플레이어 슬램
            if (player.isSlam) 
            {
                Debug.Log("[Utan] Player Attacked Slam!");

                // 중복 충돌 방지
                StartCoroutine(Co_IgnorePlayer(player, 0.2f));

                // 플레이어 넉백
                player.controller.OnKnockback(Vector2.up, slamKnockbackForce);

                TakeDamage(1);
                return;
            }

            if (CurState != EnemyState.Stun)
            {
                Debug.Log("[Utan] Player Body Crash");
                StartCoroutine(Co_IgnorePlayer(player, 1f)); // 중복 충돌 방지

                // 일반 접촉 데미지
                player.controller.OnKnockback(new Vector2(facingX, 0.2f), slamKnockbackForce * 0.5f);
                player.TakeDamaged(Status.contactDamage);

                if (player.isDash) player.controller.CancelDash();
            }
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
                Debug.Log("[Utan] Player Attacked Slam!");

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

    #region 페이즈 변경
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
        if (Status == null) return;

        Vector2 pos = transform.position;
        float labelOffset = 0.5f;
        float lineWidth = 20f;

        #region 탐지 범위
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(pos, Status.detectRange);


        #region 근거리 범위
        Gizmos.color = Color.purple;
        Gizmos.DrawWireSphere(pos, Status.armSmashStopDistance);
        

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, Status.meleeRange);
#if UNITY_EDITOR
        Handles.Label(pos + Vector2.up * (Status.detectRange + labelOffset), "Detect Range", GetLabelStyle(Color.gray));
        Handles.Label(pos + Vector2.up * (Status.armSmashStopDistance + labelOffset), "Smash Range", GetLabelStyle(Color.purple));
        Handles.Label(pos + Vector2.up * (Status.meleeRange + labelOffset), "Melee Range", GetLabelStyle(Color.red));
        Handles.Label(pos + Vector2.up * (Status.midRange + labelOffset), "Mid Range", GetLabelStyle(Color.red));
#endif

        #endregion

        #region 중거리 범위
        Gizmos.DrawWireSphere(pos, Status.midRange);
        
        #endregion
        #endregion

        #region 공격 범위
        #region 암석 높이
        Gizmos.color = Color.darkGray;
        float rockHeight = pos.y + Status.rockApexOffset;
        Vector2 rockLeft = new Vector2(pos.x - lineWidth, rockHeight);
        Vector2 rockRight = new Vector2(pos.x + lineWidth, rockHeight);

        Gizmos.DrawLine(rockLeft, rockRight);
        #endregion

        #region 나무타기 높이
        Gizmos.color = Color.forestGreen;
        float swingH = pos.y + Status.swingHeight;
        Vector2 swingLeft = new Vector2(pos.x - lineWidth, swingH);
        Vector2 swingRight = new Vector2(pos.x + lineWidth, swingH);

        Gizmos.DrawLine(swingLeft, swingRight);
        
        float lineLength = Status.swingLopeLength;

        // 현재 플레이어 위치 기준 Pivot 예상 지점
        Vector2 pivot = new Vector2(Player.Instance == null ? transform.position.x + -facingX * 15f : Player.Instance.transform.position.x + -facingX * 15f, 
                                    pos.y + Status.swingHeight);
        Gizmos.DrawWireSphere(pivot, 0.5f); // Pivot 점
        Gizmos.DrawLine(pivot, pivot + new Vector2(lineLength, 5f)); // 시작 점 연결선


#if UNITY_EDITOR
        Handles.Label(new Vector3(pos.x, rockHeight + labelOffset, 0), "Rock Apex Height", GetLabelStyle(Color.darkGray));
        Handles.Label(new Vector3(pos.x, swingH + labelOffset, 0), "Swing Height", GetLabelStyle(Color.forestGreen));
        Handles.Label(pivot + Vector2.up * 0.5f, "Swing Pivot", GetLabelStyle(Color.forestGreen));
        Handles.DrawWireArc(pivot, Vector3.forward, Vector3.right, 360f, lineLength);
#endif
        #endregion
        #endregion

        #region 스테이지 범위
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
    private GUIStyle GetLabelStyle(Color color)
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 12;
        return style;
    }
#endif
    #endregion

}
