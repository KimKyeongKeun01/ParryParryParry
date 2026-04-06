using System.Collections;
using UnityEditor;
using UnityEngine;

public class Boss_Utan : BaseBoss
{
    public enum AttackPattern { None, ArmSmash, RockThrow, Swing, ArmSmash_Phase2, ComboRockSmash, DoubleSwing }

    private BossStatus_Utan Status => (BossStatus_Utan)_status;
    private BossVisual_Utan Visual => (BossVisual_Utan)_visual;

    [Header(" === Boss Utan === ")]
    [Header("Attack State Machine")]
    [SerializeField] private AttackPattern curAttack = AttackPattern.None;
    [SerializeField] protected float attackTimer = 0f;

    [Header("Arm Smash")]
    public Utan_Arms arms;
    private bool isSmashHit = false;    // 타격 중복 방지 플래그

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
    private float lastJumpSmashTime = -99f;
    private float lastComboTime = -99f;
    private float lastDoubleSwingTime = -99f;

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
        if (isPlayingCutScene) return;
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

        // 1페이즈 쿨타임
        bool canArm = curTime - lastArmSmashTime >= Status.armSmashCooldown;
        bool canRock = curTime - lastRockTime >= Status.rockThrowCooldown;
        bool canSwing = curTime - lastSwingTime >= Status.swingCooldown;

        // 2페이즈 쿨타임
        bool canCombo = curTime - lastComboTime >= Status.comboCooldown;
        bool canDoubleSwing = curTime - lastDoubleSwingTime >= Status.doubleSwingCooldown;

        AttackPattern nextPattern = AttackPattern.None;

        if (phase >= 2)
        {
            // [2페이즈 우선순위 1] 내려찍기 - 쿨타임마다 3 : 7
            if (canArm)
            {
                float rand = Random.Range(1f, 100f);
                nextPattern = rand <= 30 ? AttackPattern.ArmSmash : AttackPattern.ArmSmash_Phase2;
            }
            // [2페이즈 우선순위 2] 더블스윙 - 내려찍기가 쿨타임인 경우
            else if (canDoubleSwing)
            {
                nextPattern = AttackPattern.DoubleSwing;
            }
            // [2페이즈 우선순위 3] 그로기 직후
            // Todo 그로기 직후를 알리는 변수에 맞게 if문 내부 수정 필요
            else if (canCombo)
            {
                nextPattern = AttackPattern.ComboRockSmash;
            }
        }
        else
        {
            // [1페이즈 우선순위 1] 내려찍기 - 쿨타임마다
            if (canArm)
            {
                nextPattern = AttackPattern.ArmSmash;
            }
            // [1페이즈 우선순위 2] 바야바 - 내려찍기가 쿨타임인 경우
            else if (canSwing)
            {
                nextPattern = AttackPattern.Swing;
            }
            // [1페이즈 우선순위 3] 그로기 직후
            // Todo 그로기 직후를 알리는 변수에 맞게 if문 내부 수정 필요
            else if (canRock)
            {
                nextPattern = AttackPattern.RockThrow;
            }
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

        originY = transform.position.y;

        // 패턴 시작 시 플레이어 방향 보기
        LookAtPlayer();

        curAttack = (AttackPattern)(index + 1);
        attackTimer = 10f;

        switch (curAttack)
        {
            case AttackPattern.ArmSmash:        yield return Co_ArmSmash();         break;
            case AttackPattern.ArmSmash_Phase2: yield return Co_ArmSmash_Phase2();  break;
            case AttackPattern.RockThrow:       yield return Co_RockThrow();        break;
            case AttackPattern.Swing:           yield return Co_Swing();            break;
            case AttackPattern.ComboRockSmash:  yield return Co_ComboRockSmash();   break;
            case AttackPattern.DoubleSwing:     yield return Co_DoubleSwing();      break;
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

        facingX = dirX;
    }
    #endregion

    #region 공격
    public override void OnAttack()
    {
        if (patternCoroutine != null) return;

        patternCoroutine = StartCoroutine(Co_PatternCycle());
    }

    #region 내려찍기
    private IEnumerator Co_ArmSmash()
    {
        Debug.Log("[Utan] Arm Smash!");

        // 팔 들어올리기
        yield return new WaitForSeconds(Status.defultWindUpTime);
        Visual?.PlayAnim("ArmSmash_Prepare");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);

        // 패턴 안전장치
        if (curAttack == AttackPattern.None) yield break;

        // 플레이어의 머리 위 armSmashMeleeRange 옆에 텔레포트
        Player player = Player.Instance;
        Vector2 startPos = _rigid.position; 
        float targetX = player.transform.position.x + (facingX < 0? 1 : -1) * Status.armSmashMeleeRange;
        Vector2 targetPos = new Vector2(targetX, startPos.y + Status.armSmashHeight);
        _rigid.position = targetPos;

        // airduration만큼 공중에서 대기
        yield return new WaitForSeconds(Status.armSmashAirDuration);
        if (curAttack == AttackPattern.None || CurState != EnemyState.Attack)
        {
            yield break;
        }
        LookAtPlayer();

        // 포물선을 그리며 플레이어에게 내려찍기
        Vector2 fallStartPos = _rigid.position;
        float playerPosX = player.transform.position.x + (facingX > 0 ? -4 : 4);
        Vector2 landingPos = new Vector2(playerPosX, originY);

        float dropDuration = Status.dropDuration;
        float arcHeight = Status.arcHeight;
        float elapsed = 0f;
        bool isPlaySmash = false;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / dropDuration;   // 진행률
            float nextX = Mathf.Lerp(fallStartPos.x, landingPos.x, t);
            float baseY = Mathf.Lerp(fallStartPos.y, landingPos.y, t);
            float arcY = Mathf.Sin(t * Mathf.PI) * arcHeight;

            if (!isPlaySmash && elapsed >= 0.4f)
            {
                Visual?.PlayAnim("ArmSmash_Action");
                isPlaySmash = true;
            }

            float nextY = baseY + arcY;

            _rigid.MovePosition(new Vector2(nextX, nextY));
            yield return null;
        }

        // 착지 후 복구 및 종료
        ValidateStage();
        LookAtPlayer();
        StopMove();
        _rigid.position = new Vector2(_rigid.position.x, originY);
        lastArmSmashTime = Time.time;
    }

    public void AE_SmashEnable()
    {
        isSmashHit = false;
        arms.SetAttack(true);
    }

    public void AE_SmashDisable()
    {
        arms.SetAttack(false);
    }

    public void OnArmHit()
    {
        if (isSmashHit || CurState == EnemyState.Stun) return;

        Player player = Player.Instance;
        if (player == null) return;

        isSmashHit = true;

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

    #region 2페이즈 내려찍기
    private IEnumerator Co_ArmSmash_Phase2()
    {
        Debug.Log("[Utan] Phase2 Arm Smash!");

        // 패턴 안전장치
        if (curAttack == AttackPattern.None) yield break;

        Player player = Player.Instance;
        Vector2 startPos = _rigid.position;
        
        // 팔 들어올리기
        Visual?.PlayAnim("ArmSmash_Prepare");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);

        // 가짜 텔레포트
        float targetX = player.transform.position.x + (facingX < 0? 1 : -1) * Status.armSmashMeleeRange;
        Vector2 targetPos = new Vector2(targetX, originY + Status.armSmashHeight);
        _rigid.position = targetPos;

        // airduration만큼 공중에서 대기
        LookAtPlayer();
        yield return new WaitForSeconds(Status.armSmashAirDuration_Phase2);
        if (curAttack == AttackPattern.None || CurState != EnemyState.Attack)
        {
            yield break;
        }

        // 두 번째 텔레포트
        targetX = player.transform.position.x + (facingX < 0? -1 : 1) * Status.armSmashMeleeRange;
        targetPos = new Vector2(targetX, originY + Status.armSmashHeight);
        _rigid.position = targetPos;

        yield return new WaitForSeconds(Status.armSmashAirDuration);
        if (curAttack == AttackPattern.None || CurState != EnemyState.Attack)
        {
            yield break;
        }
        LookAtPlayer();

        // 애니메이션과 동시에 내려찍기 수행
        Vector2 fallStartPos = _rigid.position;
        float playerPosX = player.transform.position.x + (facingX > 0 ? -4 : 4);
        Vector2 landingPos = new Vector2(playerPosX, originY);

        float dropDuration = Status.dropDuration;
        float arcHeight = Status.arcHeight;
        float elapsed = 0f;
        bool isPlaySmash = false;

        while (elapsed < dropDuration)
        {
            if (curAttack == AttackPattern.None || CurState != EnemyState.Attack)
            {
                yield break;
            }

            elapsed += Time.deltaTime;

            float t = elapsed / dropDuration;   // 진행률
            float nextX = Mathf.Lerp(fallStartPos.x, landingPos.x, t);
            float baseY = Mathf.Lerp(fallStartPos.y, landingPos.y, t);
            float arcY = Mathf.Sin(t * Mathf.PI) * arcHeight;

            // 1페이즈와 동일한 타이밍에 액션 애니메이션 재생
            if (!isPlaySmash && elapsed >= 0.4f)
            {
                Visual?.PlayAnim("ArmSmash_Action");
                isPlaySmash = true;
            }

            float nextY = baseY + arcY;

            _rigid.MovePosition(new Vector2(nextX, nextY));
            yield return null;
        }

        // 착지 후 복구 및 종료
        ValidateStage();
        LookAtPlayer();
        StopMove();
        _rigid.position = new Vector2(_rigid.position.x, originY);
        lastArmSmashTime = Time.time;
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

        Vector2 ascendStartPos = _rigid.position;
        Vector2 ascendTargetPos = new Vector2(transform.position.x, swingPosition.y + 10);

        Visual?.PlayAnim("Swing_Prepare");
        yield return new WaitForSeconds(0.8f);
        if (curAttack == AttackPattern.None) { ResetSwing(); yield break; }

        float ascendDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < ascendDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / ascendDuration;
            _rigid.MovePosition(Vector2.Lerp(ascendStartPos, ascendTargetPos, t));
            yield return null;
        }
        transform.position = swingPosition + new Vector2(startSide * lopeLength, 9f);;

        Visual?.PlayAnim("Swing_Action");
        Debug.Log("[Utan] Swing!");

        #region 나무 타기
        float radius = Vector2.Distance(transform.position, swingPosition);
        float currentAngle = Mathf.Atan2(transform.position.y - swingPosition.y, transform.position.x - swingPosition.x) * Mathf.Rad2Deg;

        float startAngle = currentAngle;
        float totalRotated = 0f;
        float targetRotation = 240f;

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
        transform.rotation = Quaternion.identity;

        Vector2 descendStartPos = transform.position;
        Vector2 descendTargetPos = new Vector2(transform.position.x, originY);
        float descendDuration = 0.17f;
        elapsed = 0f;
        Visual.PlayAnim("Swing_Landing");

        while (elapsed < descendDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / descendDuration;
            
            _rigid.MovePosition(Vector2.Lerp(descendStartPos, descendTargetPos, t));
            yield return null;
        }

        transform.position = descendTargetPos;
        _rigid.position = descendTargetPos;

        ValidateStage();    // 착지 위치 x값 보정
        Visual.PlayAnim("Motion_Reset");
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);
        yield return new WaitForSeconds(Status.defultWindDownTime);

        // 나무에서 착지
        transform.position = new Vector2(transform.position.x, originY);
        // 상태 초기화
        ResetSwing();
        lastSwingTime = Time.time;
    }

    private void ResetSwing()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector2(transform.position.x, originY);
        LookAtPlayer();
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

    #region 스파이크 콤보
    private IEnumerator Co_ComboRockSmash()
    {
        Debug.Log("[Utan] Phase 2: Volleyball Spike Combo");
        Player player = Player.Instance;
        if (player == null) yield break;

        // 공을 토스
        LookAtPlayer();
        Visual?.PlayAnim("ArmSmash_Prepare"); 
        yield return new WaitForSeconds(0.15f); // 팔을 들어올리는 타이밍 대기
        if (CurState != EnemyState.Attack) yield break;

        GameObject spikeRock = null;
        Rigidbody2D rockRigid = null;
        if (rockPrefab != null && rockSpawnPoint != null)
        {
            spikeRock = Instantiate(rockPrefab, rockSpawnPoint.position, Quaternion.identity);
            rockRigid = spikeRock.GetComponent<Rigidbody2D>();
        }

        player = Player.Instance;
        if (player == null) yield break;

        // 보스와 플레이어의 중간 위치
        float midX = (transform.position.x + player.transform.position.x) / 2f;
        float dirToPlayer = Mathf.Sign(player.transform.position.x - transform.position.x);

        // 공이 도달할 최고점 Y좌표
        float targetApexY = originY + Status.comboTossHeight;
        float timeToApex = 0.55f; // 돌이 최고점에 도달하기까지 걸리는 시간

        // 돌에 진짜 물리(중력+초속도)를 적용하여 완벽한 포물선으로 날려보냄
        if (spikeRock != null && rockRigid != null)
        {
            BaseProjectile proj = spikeRock.GetComponent<BaseProjectile>();
            
            // 시작점(보스의 손)과 목표점의 높이 차이
            float actualTossHeight = targetApexY - spikeRock.transform.position.y;
            if (actualTossHeight < 0.5f) actualTossHeight = 0.5f;

            // 포물선 물리 계산 (공식: h = 0.5 * g * t^2)
            float neededGravity = 2f * actualTossHeight / (timeToApex * timeToApex);
            float gravityScale = neededGravity / Mathf.Abs(Physics2D.gravity.y);
            
            // 계산된 중력과 속도를 강제 주입
            float vy = neededGravity * timeToApex;
            float dx = midX - spikeRock.transform.position.x;
            float vx = dx / timeToApex;

            proj.SetGravity(true, gravityScale);
            proj.SetGround(false);
            rockRigid.linearVelocity = new Vector2(vx, vy);
        }

        // 무릎을 굽히고 점프를 준비
        float bossWaitTime = 0.35f; // 바닥 대기 시간
        yield return new WaitForSeconds(bossWaitTime);
        if (CurState != EnemyState.Attack) 
        {
            if (spikeRock != null) Destroy(spikeRock);
            yield break;
        }

        // 급도약
        float originGravity = _rigid.gravityScale;
        _rigid.gravityScale = 0f;
        _rigid.linearVelocity = Vector2.zero;

        Vector2 bossStartPos = _rigid.position;
        // 보스의 목표 지점 (공보다 살짝 뒤, 살짝 위쪽)
        Vector2 bossApex = new Vector2(midX - (dirToPlayer * 1.5f), targetApexY + 0.5f);

        // 보스가 도약하는 시간 = 공이 최고점에 도달하기까지 남은 시간
        float bossJumpTime = timeToApex - bossWaitTime; 
        float elapsed = 0f;

        while (elapsed < bossJumpTime)
        {
            if (CurState != EnemyState.Attack)
            {
                _rigid.gravityScale = originGravity;
                if (spikeRock != null) Destroy(spikeRock);
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / bossJumpTime;
            float easeT = 1f - (1f - t) * (1f - t); // 처음엔 훅 떠오르고 점점 느려지는 감속 점프

            _rigid.MovePosition(Vector2.Lerp(bossStartPos, bossApex, easeT));
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);
        if (CurState != EnemyState.Attack)
        {
            _rigid.gravityScale = originGravity;
            if (spikeRock != null) Destroy(spikeRock);
            yield break;
        }

        // 공중 스파이크
        LookAtPlayer();
        Visual?.PlayAnim("ArmSmash_Action");

        if (spikeRock != null)
        {
            player = Player.Instance;
            Vector2 toPlayer = (player.transform.position - spikeRock.transform.position).normalized;
            
            float spikeSpeed = Status.rockSpeed * Status.comboSpikeSpeed; 
            BaseProjectile proj = spikeRock.GetComponent<BaseProjectile>();
            
            proj.Init(toPlayer, Status.rockDamage, spikeSpeed);
            proj.SetGravity(false);
            proj.SetGround(true);
            spikeRock.GetComponent<Rigidbody2D>().linearVelocity = toPlayer * spikeSpeed;
        }

        // 내려찍으며 바닥으로 곤두박질
        while (_rigid.position.y > originY)
        {
            if (CurState != EnemyState.Attack)
            {
                _rigid.gravityScale = originGravity;
                yield break;
            }
            _rigid.MovePosition(_rigid.position + Vector2.down * 120 * Time.deltaTime);
            yield return null;
        }

        // 착지
        _rigid.gravityScale = originGravity;
        _rigid.position = new Vector2(_rigid.position.x, originY);
        ValidateStage();
        
        AE_SmashEnable(); 
        yield return new WaitForSeconds(0.15f);
        AE_SmashDisable();

        yield return new WaitUntil(() => Visual.IsAnimFinished || CurState != EnemyState.Attack);
        yield return new WaitForSeconds(Status.defultWindDownTime);

        lastComboTime = Time.time;
    }
    #endregion

    #region 더블 스윙
    private IEnumerator Co_DoubleSwing()
    {
        Debug.Log("[Utan] Phase 2: Double Swing Start!");
        attackTimer = 30f;
        originY = transform.position.y;
        
        Player player = Player.Instance;
        if (player == null) yield break;

        LookAtPlayer();

        // [1타] 허공으로 높게 날아가는 스윙
        float startSide = -Mathf.Sign(player.transform.position.x - transform.position.x);
        
        // 피벗(나뭇가지)을 높게 잡음
        float pivotY = originY + Status.swingHeight + Status.doubleSwingFirstHeightOffset;
        swingPosition = new Vector2(player.transform.position.x, pivotY);
        float lopeLength = Status.swingLopeLength;

        // 올라가는 애니메이션 수행
        Vector2 ascendStartPos = _rigid.position;
        Vector2 ascendTargetPos = new Vector2(transform.position.x, swingPosition.y + 10);

        Visual?.PlayAnim("Swing_Prepare");
        yield return new WaitForSeconds(0.8f);
        if (curAttack == AttackPattern.None) { ResetSwing(); yield break; }

        float ascendDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < ascendDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / ascendDuration;
            _rigid.MovePosition(Vector2.Lerp(ascendStartPos, ascendTargetPos, t));
            yield return null;
        }

        transform.position = swingPosition + new Vector2(startSide * lopeLength, 9f);

        Visual?.PlayAnim("Swing_Action");

        float radius = Vector2.Distance(transform.position, swingPosition);
        float currentAngle = Mathf.Atan2(transform.position.y - swingPosition.y, transform.position.x - swingPosition.x) * Mathf.Rad2Deg;
        float totalRotated = 0f;

        // 1타 진행
        while (totalRotated < 240f)
        {
            if (curAttack == AttackPattern.None || CurState != EnemyState.Attack) { ResetSwing(); yield break; }
            
            float angleStep = Status.swingSpeed * 10f * Time.deltaTime; // deltaTime당 이동 각도
            totalRotated += angleStep;
            currentAngle += angleStep * -startSide; // 진행 방향

            float rad = currentAngle * Mathf.Deg2Rad;
            float nextX = swingPosition.x + Mathf.Cos(rad) * radius;
            float nextY = swingPosition.y + Mathf.Sin(rad) * radius;

            transform.position = new Vector2(nextX, nextY);
            transform.rotation = Quaternion.Euler(0, 0, currentAngle + 90f);    // 각에 맞게 rotation 전환

            yield return null;
        }

        // 방향 반전
        startSide = -startSide; 
        player = Player.Instance;
        LookAtPlayer();
        
        // 기존 1페이즈 스윙 높이로 피벗을 낮춤
        swingPosition = new Vector2(player != null ? player.transform.position.x : transform.position.x, originY + Status.swingHeight);

        // 처음 시작위치 반대에 지정
        Vector2 startPos = swingPosition + new Vector2(startSide * lopeLength, 9f);
        _rigid.MovePosition(startPos);
        transform.position = startPos;
        
        // 공중에 멈춘 현재 위치 기준으로 반지름과 각도 새로 계산
        radius = Vector2.Distance(transform.position, swingPosition);
        currentAngle = Mathf.Atan2(transform.position.y - swingPosition.y, transform.position.x - swingPosition.x) * Mathf.Rad2Deg;
        
        totalRotated = 0f; 
        float secondSwingSpeed = Status.swingSpeed;
        
        float targetRotation = 240f; 

        // 2타 진행
        while (totalRotated < targetRotation)
        {
            if (curAttack == AttackPattern.None || CurState != EnemyState.Attack) { ResetSwing(); yield break; }
            
            float angleStep = secondSwingSpeed * 10f * Time.deltaTime;
            totalRotated += angleStep;
            currentAngle += angleStep * -startSide; 

            float rad = currentAngle * Mathf.Deg2Rad;
            float nextX = swingPosition.x + Mathf.Cos(rad) * radius;
            float nextY = swingPosition.y + Mathf.Sin(rad) * radius;

            transform.position = new Vector2(nextX, nextY);
            transform.rotation = Quaternion.Euler(0, 0, currentAngle + 90f);

            yield return null;
        }

        // 착지 및 마무리
        Debug.Log("[Utan] Double Swing End");
        transform.rotation = Quaternion.identity;

        Vector2 descendStartPos = transform.position;
        Vector2 descendTargetPos = new Vector2(transform.position.x, originY);
        float descendDuration = 0.17f;
        elapsed = 0f;
        Visual?.PlayAnim("Swing_Landing");

        while (elapsed < descendDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / descendDuration;
            
            _rigid.MovePosition(Vector2.Lerp(descendStartPos, descendTargetPos, t));
            yield return null;
        }
        
        transform.position = descendTargetPos;
        _rigid.position = descendTargetPos;
        
        ValidateStage();
        Visual?.PlayAnim("Motion_Reset");
        
        yield return new WaitUntil(() => Visual.IsAnimFinished || curAttack == AttackPattern.None);
        yield return new WaitForSeconds(Status.defultWindDownTime);

        transform.position = new Vector2(transform.position.x, originY); 
        ResetSwing();
        lastDoubleSwingTime = Time.time;
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

        transform.position = new Vector2(transform.position.x, originY);

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
            if (curAttack == AttackPattern.Swing || curAttack == AttackPattern.DoubleSwing)
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
                player.controller.OnKnockback(Vector2.up, slamKnockbackForce, -1, true);

                TakeDamage(1);
                return;
            }

            if (CurState != EnemyState.Stun)
            {
                Debug.Log("[Utan] Player Body Crash");
                StartCoroutine(Co_IgnorePlayer(player, 1f)); // 중복 충돌 방지

                // 일반 접촉 데미지
                if (CurState != EnemyState.Stun)
                {
                    Debug.Log("[Utan] Player Body Crash");
                    StartCoroutine(Co_IgnorePlayer(player, 1f)); // 중복 충돌 방지

                    // 1. 넉백을 주면서 동시에 플레이어의 가드 상태를 받아옵니다.
                    Player.GuardType guard = player.controller.OnKnockback(new Vector2(facingX, 0.2f), slamKnockbackForce * 0.5f);

                    // 2. 가드 상태에 따라 데미지 적용 여부를 결정합니다.
                    if (guard == Player.GuardType.PerfectGuard)
                    {
                        Debug.Log("[Utan] Body Crash: Perfect Guarded!");
                    }
                    else if (guard == Player.GuardType.Guard)
                    {
                        Debug.Log("[Utan] Body Crash: Guarded!");
                    }
                    else
                    {
                        // 노멀 히트 -> 데미지 입힘
                        player.TakeDamaged(Status.contactDamage);

                        // 맞았을 때 대시 중이었다면 취소
                        if (player.isDash) player.controller.CancelDash(); 
                    }
                }
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
                player.controller.OnKnockback(Vector2.up, slamKnockbackForce, -1, true);
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
        

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, Status.meleeRange);
#if UNITY_EDITOR
        Handles.Label(pos + Vector2.up * (Status.detectRange + labelOffset), "Detect Range", GetLabelStyle(Color.gray));
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

        #region 스파이크 콤보

        // 도약 높이
        Gizmos.color = Color.magenta;
        float tossHeight = pos.y + Status.comboTossHeight;
        
        Vector2 tossLeft = new Vector2(pos.x - lineWidth, tossHeight);
        Vector2 tossRight = new Vector2(pos.x + lineWidth, tossHeight);
        Gizmos.DrawLine(tossLeft, tossRight);

#if UNITY_EDITOR 
        Handles.Label(new Vector3(pos.x, tossHeight + labelOffset, 0), "Combo Toss Height", GetLabelStyle(Color.magenta));
#endif
        #endregion

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
