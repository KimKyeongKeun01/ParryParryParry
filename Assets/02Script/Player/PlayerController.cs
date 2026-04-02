using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerController : MonoBehaviour, IPlatformPassenger2D, IBounceable
{
    public static event System.Action<PlayerController> JumpStarted;
    private const float MinSlamEaseDuration = 0.18f;
    private const float MaxSlamEaseFallbackMultiplier = 1.5f;

    Player player;
    PlayerStatus stat;
    Rigidbody2D rb;

    #region 기본 변수
    [Header("이동")]
    [SerializeField] private Vector2 moveDir;
    private float moveElapsedTime;
    private float decelElapsedTime;
    private float defaultMass;
    private float dashToSlamGraceUntilTime;

    [Header("Footstep")]
    [SerializeField] private float baseFootstepInterval = 0.18f;
    [SerializeField] private float minFootstepSpeed = 0.3f;
    private float footstepTimer;

    [Header("점프")]
    [SerializeField] private int curJumpCount;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float fallSquashVelocityThreshold = -0.05f;
    private bool isJumpHeld;
    private float jumpTimeCounter;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool isJumpProfileActive;
    private float jumpProfileStartY;
    private float jumpProfilePeakHeight;
    private float jumpProfileRiseDuration;
    private Ease jumpProfileEase;
    private float jumpProfileElapsed;
    private float jumpProfileMinDuration;
    private float jumpBufferUntilTime;
    private float bufferedJumpPressedTime;
    private float bufferedJumpReleasedTime;
    private bool hasBufferedJump;
    private bool bufferedJumpReleased;
    private bool wasFalling;
    private float _accelerationTime => player.IsGrounded ? stat.AccelerationTime : stat.InAirAccelerationTime;
    private float _decelarationTime => player.IsGrounded ? stat.DecelerationTime : stat.InAirDecelerationTime;
    [Header("대시")]
    private Coroutine dashCor;
    private bool dashPressed;
    private float dashCooldownTimer;
    [SerializeField] private int curDashCount;

    [SerializeField] private Vector2 slopeNormal;
    private Vector2 currentSlamDir; // 슬램 실행 방향
    public Vector2 CurrentSlamDir => currentSlamDir;
    private bool slamInterrupted; // 슬램 도중 넉백으로 인한 강제 중단 플래그

    private bool hasUsedAirSlam;

    private PlayerAbilityFlags allowedAbilities = PlayerAbilityFlags.All;
    public void SetAbilities(PlayerAbilityFlags flags) => allowedAbilities = flags;
    private bool Can(PlayerAbilityFlags flag) => (allowedAbilities & flag) != 0;

    [Header("슬램")]
    private Coroutine slamCor;
    private WaitForFixedUpdate cachedWaitForFixedUpdate;
    private WaitForSeconds cachedPerfectGuardWait;

    RaycastHit2D slamHit;
    private readonly RaycastHit2D[] groundHitResults = new RaycastHit2D[8];
    private readonly RaycastHit2D[] slamBlockHitResults = new RaycastHit2D[8];
    private readonly Collider2D[] ceilingOverlapResults = new Collider2D[8];

    // 슬램 탐지 버퍼

    /// <summary>점프 키 입력 타이머</summary>
    public float JumpTimeCounter { get; private set; }
    /// <summary>코요테 타임 타이머</summary>
    public float CoyoteTimeCounter { get; private set; }
    /// <summary>공중에서 점프를 사용한 횟수</summary>
    public int JumpsUsed { get; private set; }
    /// <summary>착지 후 착지 모멘텀이 유지되는 타이머</summary>
    public float LandingMomentumCounter { get; private set; }
    /// <summary>강제 이동의 최대속도 유지 타이머</summary>
    public float ForcedHorizontalTimer { get; private set; }
    /// <summary>강제 이동 시 적용되는 속도 값</summary>
    public float ForcedHorizontalVelocity { get; private set; }
    #endregion

    #region 액션 이벤트
    /// <summary>점프 실행 시 호출</summary>
    public event Action<Vector2> onJump;
    /// <summary>슬램 실행 시 호출</summary>
    public event Action onSlam;
    /// <summary>슬램 실행 시 호출</summary>
    public event Action<Vector2, Vector2> onSlamStart;
    /// <summary>가드 시작 시 호출</summary>
    public event Action onGuardStart;
    /// <summary>가드 종료 시 호출</summary>
    public event Action onGuardEnd;
    /// <summary>슬램 종료 시 호출</summary>
    public event Action<Vector2, Vector2, Color> onSlamImpact;
    /// <summary>슬램 적 타격 시 호출</summary>
    public event Action<Vector2, Vector2> onSlamEnemyImpact;
    /// <summary>퍼펙트 가드 성공 시 호출</summary>
    public event Action<Vector2, Vector2> onPerfectGuardSuccess;
    /// <summary>지상 이동 중 발걸음 타이밍마다 호출</summary>
    public event Action<int> onFootstep;
    #endregion

    public void Init(Player _player)
    {
        player = _player;
        stat = _player.status;
        Debug.Log(stat);
        rb = GetComponent<Rigidbody2D>();
        defaultMass = rb.mass;
        curDashCount = stat.MaxDashCount;
        dashToSlamGraceUntilTime = 0f;
        _guardGauge = stat.MaxGuardGauge;
        _guardRecoveryTimer = 0f;
        _guardCooldownTimer = 0f;
        player.isDash = false;
        player.isPlaying = true;
        cachedWaitForFixedUpdate = new WaitForFixedUpdate();
        cachedPerfectGuardWait = new WaitForSeconds(stat.PerfectGuardWindow);

    }

    // Start에서 호출 — InputManager는 Start 시점에 보장됨
    public void SubscribeInput()
    {
        var inputMgr = InputManager.Instance;
        inputMgr.moveAction += OnMoveInput;
        inputMgr.verticalMoveAction += OnVerticalMoveInput;
        inputMgr.jumpAction += OnJumpInput;
        inputMgr.dashAction += PerformDash;
        inputMgr.guardAction += OnGuardInput;
        inputMgr.slamAction += OnSlamInput;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance == null) return;
        var inputMgr = InputManager.Instance;
        inputMgr.moveAction -= OnMoveInput;
        inputMgr.verticalMoveAction -= OnVerticalMoveInput;
        inputMgr.jumpAction -= OnJumpInput;
        inputMgr.dashAction -= PerformDash;
        inputMgr.guardAction -= OnGuardInput;
        inputMgr.slamAction -= OnSlamInput;
    }

    private void Update()
    {
        if (player == null) return;

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        // 쿨타임 만료 + 지상 → 대쉬 횟수 충전 (공중에서는 착지까지 대기)
        if (dashCooldownTimer <= 0f && player.IsGrounded && curDashCount < stat.MaxDashCount)
        {
            curDashCount = stat.MaxDashCount;
            hasUsedAirSlam = false;
        }

        // 대시/슬램 판정: 모든 입력 이벤트가 처리된 후 평가
        if (dashPressed)
        {
            dashPressed = false;
            ExecuteDash();
        }
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        RemovePreviousPlatformVelocity();
        CheckGround();
        PlayerGravity();

        if (player.isPlaying)
        {
            PlayerMovement();
        }

        UpdateJumpTimers();
        UpdateGuardGauge();
        UpdateVisualFeedback();
        UpdateFootstep();

        if (ShouldApplyPlatformVelocity())
            ApplyPlatformVelocity();
    }

    public void Setup()
    {
        // 코루틴 전부 중단
        StopAllCoroutines();
        slamCor = null;
        dashCor = null;
        guardCor = null;

        // 이동
        moveDir = Vector2.zero;
        moveElapsedTime = 0f;
        decelElapsedTime = 0f;

        // 점프
        curJumpCount = 0;
        isJumpHeld = false;
        jumpTimeCounter = 0f;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        isJumpProfileActive = false;
        jumpProfileStartY = 0f;
        jumpProfilePeakHeight = 0f;
        jumpProfileRiseDuration = 0f;
        jumpProfileEase = Ease.Unset;
        jumpProfileElapsed = 0f;
        jumpProfileMinDuration = 0f;
        jumpBufferUntilTime = 0f;
        bufferedJumpPressedTime = 0f;
        bufferedJumpReleasedTime = 0f;
        hasBufferedJump = false;
        bufferedJumpReleased = false;
        wasFalling = false;

        // 대시
        dashPressed = false;
        dashCooldownTimer = 0f;
        curDashCount = stat.MaxDashCount;
        dashToSlamGraceUntilTime = 0f;

        // 가드
        _guardGauge = stat.MaxGuardGauge;
        _guardRecoveryTimer = 0f;
        _guardCooldownTimer = 0f;
        _guardStartTime = 0f;
        _guardReleaseDelayCor = null;

        // 슬램
        slopeNormal = Vector2.up;
        currentSlamDir = Vector2.zero;
        slamInterrupted = false;
        hasUsedAirSlam = false;

        // 플랫폼
        platformVelocity = Vector2.zero;
        lastAppliedPlatformVelocity = Vector2.zero;
        currentPlatform = null;

        // 물리
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f;
        rb.mass = defaultMass;

        // 플레이어 상태 플래그
        player.isSlam = false;
        player.isGuard = false;
        player.isPerfactGuard = false;
        player.isPlaying = true;
        player.isDash = false;
        player.visual.Setup();
    }

    private void UpdateVisualFeedback()
    {
        if (player?.visual == null) return;

        bool moveParticlesActive = player.isPlaying && player.IsGrounded && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        player.visual.SetMoveParticlesActive(moveParticlesActive);

        bool isFalling = !player.IsGrounded && rb.linearVelocity.y < fallSquashVelocityThreshold;

        if (isFalling && !wasFalling)
            player.visual.PlayFallingStartSquash();

        wasFalling = isFalling;
    }

    #region 이동
    private void OnMoveInput(float input)
    {
        moveDir.x = input;

        if (input != 0)
        {
            bool isLeft = input < 0;
            player.visual.SetFlip(isLeft);
            player.UpdateFacingDirection(isLeft ? -1 : 1);
        }
    }

    private void OnVerticalMoveInput(float input)
    {
        moveDir.y = input;
    }

    private void OnSlamInput(bool pressed)
    {
        if (!pressed) return;
        if (!Can(PlayerAbilityFlags.Slam)) return;
        if (slamCor != null) return;
        if (player.IsGrounded) return;
        if (hasUsedAirSlam) return;

        bool canConvertFromDash = player.isDash || Time.time <= dashToSlamGraceUntilTime;
        if (canConvertFromDash)
        {
            CancelDash();
            dashToSlamGraceUntilTime = 0f;
        }
        else if (!player.isPlaying)
        {
            return;
        }

        onSlam?.Invoke();
        slamCor = StartCoroutine(SlamCoroutine());
    }

    public void PlayerMovement()
    {
        float targetSpd = moveDir.x * stat.MoveSpeed;

        if (!player.IsGrounded)
            targetSpd *= stat.AirControlPercent;

        float curveValue;
        float newX = rb.linearVelocity.x;
        if (moveDir.x != 0)
        {
            moveElapsedTime += Time.fixedDeltaTime;
            decelElapsedTime = 0f;
            curveValue = stat.AccelerationCurve.Evaluate(moveElapsedTime / _accelerationTime);
            newX = Mathf.Lerp(0, targetSpd, curveValue);
        }
        else
        {
            float previousDecelElapsedTime = decelElapsedTime;
            float previousCurveValue = stat.DecelerationCurve.Evaluate(previousDecelElapsedTime / _decelarationTime);

            decelElapsedTime += Time.fixedDeltaTime;
            moveElapsedTime = 0f;
            curveValue = stat.DecelerationCurve.Evaluate(decelElapsedTime / _decelarationTime);

            if (curveValue <= 0f || previousCurveValue <= 0f)
            {
                newX = 0f;
            }
            else
                newX = rb.linearVelocity.x * (curveValue / previousCurveValue);
        }

        if (stat.LimitMaxHorizontalSpeed)
        {
            if (Mathf.Abs(rb.linearVelocity.x) > stat.MaxHorizontalSpeed)
                // 초과 속도: Deceleration 속도로 MaxHorizontalSpeed까지 복귀 (순간 끊김 방지 + 감속 너무 길어지는 것 방지)
                newX = Mathf.MoveTowards(newX, Mathf.Sign(newX) * stat.MaxHorizontalSpeed, stat.Deceleration * Time.fixedDeltaTime);
            else
                newX = Mathf.Clamp(newX, -stat.MaxHorizontalSpeed, stat.MaxHorizontalSpeed);
        }

        float slopeAngle = Vector2.Angle(slopeNormal, Vector2.up);
        // y > 0 (점프 직후 상승 중)이면 경사면 프로젝션 스킵 → 점프 y속도 보존
        if (player.IsGrounded && slopeAngle > stat.SlopeAngleThreshold && rb.linearVelocity.y <= 0f)
        {
            // 경사면: 수평 속도를 경사면 방향으로 변환 → 통통튀기 방지
            Vector2 slopeDir = new Vector2(slopeNormal.y, -slopeNormal.x);
            if (newX != 0f && Mathf.Sign(slopeDir.x) != Mathf.Sign(newX))
                slopeDir = -slopeDir;
            rb.linearVelocity = slopeDir * Mathf.Abs(newX);
        }
        else
        {
            var vel = rb.linearVelocity;
            vel.x = newX;
            rb.linearVelocity = vel;
        }

        float normalizedSpeed = stat.MoveSpeed > 0f ? Mathf.Abs(newX) / stat.MoveSpeed : 0f;
        player.visual.UpdateTilt(normalizedSpeed);
    }

    public void PlayerGravity()
    {
        if (isJumpProfileActive)
        {
            rb.gravityScale = 0f;
            return;
        }

        // 지상에서는 gravityScale 고정 — 경사면 이동 시 상승/하강 중력 오적용 방지
        if (player.IsGrounded)
        {
            rb.gravityScale = 1f;
            return;
        }

        if (rb.linearVelocity.y < 0f)
            rb.gravityScale = stat.FallGravityMultiplier;
        else if (rb.linearVelocity.y > 0f)
            rb.gravityScale = stat.RiseGravityMultiplier;
        else
            rb.gravityScale = 1f;
    }
    #endregion

    private void UpdateFootstep()
    {
        bool isWalking =
            player.IsGrounded &&
            Mathf.Abs(moveDir.x) > 0.01f &&
            Mathf.Abs(rb.linearVelocity.x) > minFootstepSpeed &&
            player.isPlaying &&
            !player.isDash &&
            !player.isSlam;

        if (!isWalking)
        {
            footstepTimer = 0f;
            return;
        }

        float speedRatio = Mathf.Clamp01(Mathf.Abs(rb.linearVelocity.x) / Mathf.Max(0.01f, stat.MoveSpeed));
        float interval = Mathf.Lerp(baseFootstepInterval * 1.3f, baseFootstepInterval * 0.7f, speedRatio);

        footstepTimer += Time.fixedDeltaTime;
        if (footstepTimer >= interval)
        {
            footstepTimer = 0f;
            onFootstep?.Invoke(player.FacingDirection);
        }
    }

    #region 점프
    private void OnJumpInput(bool isPressed)
    {
        if (isPressed)
        {
            if (!player.isPlaying)
                return;

            isJumpHeld = true;
            RegisterBufferedJumpInput();

            if (dashCor != null || player.isDash)
                return;

            TryConsumeBufferedJump();
            return;
        }

        isJumpHeld = false;

        if (hasBufferedJump)
        {
            bufferedJumpReleased = true;
            bufferedJumpReleasedTime = Time.time;
            return;
        }

        CancleJump();
    }

    private void TryJump()
    {
        bool hasCoyote = coyoteTimeCounter > 0f && curJumpCount == 0;
        bool hasJumpCount = curJumpCount < stat.MaxJumpCount;

        if (!hasCoyote && !hasJumpCount) return;
        if (player.isSlam) return;
        StartJump();
    }

    public void StartJump()
    {
        if (curJumpCount >= stat.MaxJumpCount) return;
        BeginJump(0f);
    }

    private void StartBufferedJump(float preHeldDuration)
    {
        if (curJumpCount >= stat.MaxJumpCount) return;
        BeginJump(preHeldDuration);
    }

    private void BeginJump(float preHeldDuration)
    {
        bool useGroundJumpProfile = ShouldUseGroundJumpProfile();

        curJumpCount++;
        coyoteTimeCounter = 0f;
        onJump?.Invoke(transform.position);

        var vel = rb.linearVelocity;

        ConfigureJumpProfile(useGroundJumpProfile, preHeldDuration);
        vel.y = GetJumpProfileVelocity(jumpProfileElapsed);

        rb.linearVelocity = vel;
        wasFalling = false;
        player.visual?.PlayJumpSquash();
        player.visual?.PlayJumpParticles();
        JumpStarted?.Invoke(this);
    }

    private bool ShouldUseGroundJumpProfile()
    {
        return curJumpCount == 0 && (player.IsGrounded || coyoteTimeCounter > 0f);
    }

    private void ConfigureJumpProfile(bool useGroundJumpProfile, float preHeldDuration)
    {
        isJumpProfileActive = true;
        jumpProfileStartY = rb.position.y;
        jumpProfilePeakHeight = useGroundJumpProfile ? stat.GroundJumpPeakHeight : stat.AirJumpPeakHeight;
        jumpProfileRiseDuration = Mathf.Max(0.01f, useGroundJumpProfile ? stat.GroundJumpRiseDuration : stat.AirJumpRiseDuration);
        jumpProfileEase = useGroundJumpProfile ? stat.GroundJumpEase : stat.AirJumpEase;
        jumpProfileMinDuration = jumpProfileRiseDuration * Mathf.Clamp01(stat.JumpCutMinHeightRatio);
        jumpProfileElapsed = Mathf.Clamp(preHeldDuration, 0f, jumpProfileRiseDuration);
        jumpTimeCounter = Mathf.Max(0f, jumpProfileRiseDuration - jumpProfileElapsed);
    }

    public void CancleJump()
    {
        if (!isJumpProfileActive)
            return;

        jumpProfileElapsed = Mathf.Min(jumpProfileElapsed, jumpProfileRiseDuration);
        jumpTimeCounter = Mathf.Max(0f, jumpProfileRiseDuration - jumpProfileElapsed);
    }

    private void UpdateJumpTimers()
    {
        UpdateBufferedJumpState();
        UpdateJumpProfile();

        // 코요테 타임
        if (player.IsGrounded)
            coyoteTimeCounter = stat.CoyoteTime;
        else
            coyoteTimeCounter -= Time.fixedDeltaTime;
    }

    public void CheckGround()
    {
        RaycastHit2D hit = GetFirstSolidGroundHit(groundCheck.position, Vector2.down, stat.GroundCheckDistance, stat.GroundLayer);

        bool grounded = hit.collider != null;
        player.UpdateGroundState(grounded);
        HandleCeilingCollision();

        // 착지 시 점프·대쉬 카운트 초기화 및 충전
        if (player.IsGrounded && !player.WasGrounded && IsLandingGroundHit(hit))
        {
            curJumpCount = 0;
            curDashCount = stat.MaxDashCount;
            hasUsedAirSlam = false;
            player.isDash = false;
            StopJumpProfile();
            wasFalling = false;
            if (player.isSlam) player.visual?.PlaySlamSquash();
            else player.visual?.PlayLandingSquash();
            player.visual?.PlayLandingParticles();
        }

        if (grounded)
        {
            slopeNormal = hit.normal;
        }
    }

    private bool IsLandingGroundHit(RaycastHit2D hit)
    {
        if (hit.collider == null)
            return false;

        if (hit.normal.y < 0.5f)
            return false;

        return rb == null || rb.linearVelocity.y <= 0.05f;
    }

    private void HandleCeilingCollision()
    {
        if (!isJumpProfileActive && rb.linearVelocity.y <= 0f)
            return;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
            return;

        Bounds bounds = playerCollider.bounds;
        float skinWidth = 0.02f;
        float castDistance = stat.GroundCheckDistance + skinWidth;
        Vector2 boxSize = new Vector2(bounds.size.x * stat.GroundCheckRadiusScale, skinWidth);
        Vector2 boxCenter = new Vector2(bounds.center.x, bounds.max.y + castDistance * 0.5f);

        Collider2D hit = GetFirstSolidOverlapBox(boxCenter, boxSize, 0f, stat.GroundLayer);
        if (hit == null || hit.gameObject.CompareTag("HalfPlatform"))
            return;

        var vel = rb.linearVelocity;
        if (vel.y > 0f)
        {
            vel.y = 0f;
            rb.linearVelocity = vel;
        }

        StopJumpProfile();
        isJumpHeld = false;
    }
    #endregion

    #region 가드
    private Coroutine guardCor;
    private Coroutine _guardReleaseDelayCor;
    private float _guardStartTime;
    private bool _guardButtonHeld;
    // 게이지 모드
    private float _guardGauge;
    private float _guardRecoveryTimer;
    // 쿨타임 모드
    private float _guardCooldownTimer;

    /// <summary>가드 상태 UI용 (0~1). 쿨타임 모드: 1=사용가능 0=쿨타임중 / 게이지 모드: 1=풀 0=소진</summary>
    public event Action<float> onGuardGaugeChanged;

    private void OnGuardInput(bool isPressed)
    {
        if (isPressed)
        {
            if (!player.isPlaying) return;
            if (player.isSlam) return;
            if (player.isGuard) return;
            StartGuard();
        }
        else
        {
            RequestEndGuard();
        }
    }

    private void RequestEndGuard()
    {
        if (!player.isGuard && !player.isPerfactGuard) return;

        float elapsed = Time.time - _guardStartTime;
        float remaining = stat.GuardMinDuration - elapsed;

        if (remaining <= 0f)
        {
            EndGuard();
            return;
        }

        if (_guardReleaseDelayCor != null) StopCoroutine(_guardReleaseDelayCor);
        _guardReleaseDelayCor = StartCoroutine(GuardReleaseDelayCoroutine(remaining));
    }

    private IEnumerator GuardReleaseDelayCoroutine(float delay)
    {
        var wait = new WaitForSeconds(delay);
        yield return wait;
        _guardReleaseDelayCor = null;
        EndGuard();
    }

    private void StartGuard()
    {
        if (stat.UseGuardGaugeMode)
        {
            if (_guardGauge <= 0f) return; // 게이지 소진 시 불가
        }
        else
        {
            if (_guardCooldownTimer > 0f) return; // 쿨타임 중 불가
        }

        if (_guardReleaseDelayCor != null)
        {
            StopCoroutine(_guardReleaseDelayCor);
            _guardReleaseDelayCor = null;
        }

        _guardStartTime = Time.time;
        player.isGuard = true;
        player.visual.SetActiveShield(true);
        rb.mass = defaultMass * stat.GuardMassMultiplier;
        onGuardStart?.Invoke();

        if (guardCor != null) StopCoroutine(guardCor);
        guardCor = StartCoroutine(PerfectGuardCoroutine());
    }

    private void EndGuard()
    {
        if (_guardReleaseDelayCor != null)
        {
            StopCoroutine(_guardReleaseDelayCor);
            _guardReleaseDelayCor = null;
        }
        if (!stat.UseGuardGaugeMode && (player.isGuard || player.isPerfactGuard))
        {
            _guardCooldownTimer = stat.GuardCooldownTime;
        }
        player.isGuard = false;
        player.isPerfactGuard = false;
        player.visual.SetActiveShield(false);
        player.visual?.StopDashTrail();
        rb.mass = defaultMass;
        onGuardEnd?.Invoke();

        if (guardCor != null)
        {
            StopCoroutine(guardCor);
            guardCor = null;
        }
    }

    private void UpdateGuardGauge()
    {
        if (stat.UseGuardGaugeMode)
        {
            // 게이지 모드: 가드 중 소모, 해제 후 회복
            if (player.isGuard)
            {
                _guardRecoveryTimer = 0f;
                _guardGauge -= stat.GuardDrainRate * Time.fixedDeltaTime;
                if (_guardGauge <= 0f)
                {
                    _guardGauge = 0f;
                    EndGuard();
                }
            }
            else
            {
                _guardRecoveryTimer += Time.fixedDeltaTime;
                if (_guardRecoveryTimer >= stat.GuardRecoveryDelay)
                    _guardGauge = Mathf.Min(_guardGauge + stat.GuardRecoveryRate * Time.fixedDeltaTime, stat.MaxGuardGauge);
            }
            onGuardGaugeChanged?.Invoke(_guardGauge / stat.MaxGuardGauge);
        }
        else
        {
            // 쿨타임 모드
            if (_guardCooldownTimer > 0f)
                _guardCooldownTimer -= Time.fixedDeltaTime;

            float normalized = stat.GuardCooldownTime > 0f
                ? 1f - Mathf.Clamp01(_guardCooldownTimer / stat.GuardCooldownTime)
                : 1f;
            onGuardGaugeChanged?.Invoke(normalized);
        }
    }

    private IEnumerator PerfectGuardCoroutine()
    {
        player.isPerfactGuard = true;
        yield return cachedPerfectGuardWait;
        player.isPerfactGuard = false;
        player.isGuard = true;
        guardCor = null;
    }
    #endregion


    #region 대시 / 슬램
    public void PerformDash()
    {
        dashPressed = true;
    }

    private void ExecuteDash()
    {
        if (!player.isPlaying || dashCor != null) return;
        if (!Can(PlayerAbilityFlags.Dash)) return;
        if (curDashCount <= 0) return; // 대쉬 횟수 없음
        if (dashCooldownTimer > 0f) return;

        // 아래 방향 입력 + 공중 → 슬램으로 전환 (남동/남서/수직 모두 지원)
        if (moveDir.y <= stat.SlamDashThreshold && !player.IsGrounded && slamCor == null && !hasUsedAirSlam)
        {
            onSlam?.Invoke();
            slamCor = StartCoroutine(SlamCoroutine());
            return;
        }

        curDashCount--;
        dashCooldownTimer = stat.DashCooldownTime;
        dashCor = StartCoroutine(DashCoroutine());
    }

    private IEnumerator DashCoroutine()
    {
        player.isDash = true;
        player.isPlaying = false;
        isJumpHeld = false;
        StopJumpProfile();
        player.visual?.PlayDashSquash(stat.dashDuration);
        player.visual?.StartDashTrail();
        float startTime = Time.time;
        dashToSlamGraceUntilTime = startTime + stat.DashToSlamGraceTime;
        float dashDir = moveDir.x != 0 ? Mathf.Sign(moveDir.x) : player.FacingDirection;
        float dashStartSpeed = stat.DashSpeed * stat.DashStartSpeedMultiplier;
        float dashEndSpeed = GetDashExitSpeedMagnitude() * stat.DashEndSpeedMultiplier;

        while (Time.time < startTime + stat.dashDuration)
        {
            float dashProgress = Mathf.Clamp01((Time.time - startTime) / stat.dashDuration);

            float dashSpeed = DOVirtual.EasedValue(
                dashStartSpeed,
                dashEndSpeed,
                dashProgress,
                stat.DashSpeedEase
            );
            var tempV2 = rb.linearVelocity;
            tempV2.x = dashDir * dashSpeed;
            tempV2.y = 0f;
            rb.linearVelocity = tempV2;
            yield return cachedWaitForFixedUpdate;
        }

        // 대시 종료 즉시 속도 스냅 (미끌림 방지)
        var endVel = rb.linearVelocity;
        float exitDir = moveDir.x != 0 ? Mathf.Sign(moveDir.x) : dashDir;
        endVel.x = exitDir * dashEndSpeed;
        rb.linearVelocity = endVel;
        moveElapsedTime = 0f;
        decelElapsedTime = 0f;

        player.visual?.StopDashTrail();
        dashCor = null;
        player.isDash = false;
        dashToSlamGraceUntilTime = 0f;
        player.isPlaying = true;
        UpdateJumpTimers();
    }

    private float GetDashExitSpeedMagnitude()
    {
        if (stat.LimitMaxHorizontalSpeed)
            return stat.MaxHorizontalSpeed;

        return Mathf.Max(stat.MoveSpeed, stat.MaxHorizontalSpeed);
    }

    private void RegisterBufferedJumpInput()
    {
        hasBufferedJump = true;
        jumpBufferUntilTime = Time.time + stat.JumpBufferTime;
        jumpBufferCounter = stat.JumpBufferTime;
        bufferedJumpPressedTime = Time.time;
        bufferedJumpReleasedTime = 0f;
        bufferedJumpReleased = false;
    }

    private void UpdateBufferedJumpState()
    {
        if (!hasBufferedJump)
        {
            jumpBufferCounter = 0f;
            return;
        }

        if (Time.time > jumpBufferUntilTime)
        {
            ClearBufferedJump();
            return;
        }

        jumpBufferCounter = Mathf.Max(0f, jumpBufferUntilTime - Time.time);

        if (!player.isPlaying)
            return;

        if (dashCor != null || player.isDash)
            return;

        TryConsumeBufferedJump();
    }

    private void TryConsumeBufferedJump()
    {
        if (!hasBufferedJump || !CanStartJump())
            return;

        float heldDuration = GetBufferedHeldDuration();
        StartBufferedJump(heldDuration);
        ClearBufferedJump();
    }

    private float GetBufferedHeldDuration()
    {
        float releaseTime = bufferedJumpReleased ? bufferedJumpReleasedTime : Time.time;
        float riseDuration = GetCurrentBufferedRiseDuration();
        return Mathf.Clamp(releaseTime - bufferedJumpPressedTime, 0f, riseDuration);
    }

    private void ClearBufferedJump()
    {
        jumpBufferCounter = 0f;
        jumpBufferUntilTime = 0f;
        bufferedJumpPressedTime = 0f;
        bufferedJumpReleasedTime = 0f;
        hasBufferedJump = false;
        bufferedJumpReleased = false;
    }

    private bool CanStartJump()
    {
        bool hasCoyote = coyoteTimeCounter > 0f && curJumpCount == 0;
        bool hasJumpCount = curJumpCount < stat.MaxJumpCount;

        if (!hasCoyote && !hasJumpCount) return false;
        if (!player.isPlaying) return false;
        if (player.isSlam) return false;
        if (dashCor != null || player.isDash) return false;

        // 첫 번째 점프(지상/코요테) → Jump 플래그
        // 공중 점프(더블 점프) → DoubleJump 플래그
        bool isFirstJump = curJumpCount == 0 && (player.IsGrounded || hasCoyote);
        if (isFirstJump && !Can(PlayerAbilityFlags.Jump)) return false;
        if (!isFirstJump && !Can(PlayerAbilityFlags.DoubleJump)) return false;

        return true;
    }

    private void UpdateJumpProfile()
    {
        if (!isJumpProfileActive)
        {
            jumpTimeCounter = 0f;
            return;
        }

        float targetElapsed = jumpProfileElapsed;
        bool canContinueRise = isJumpHeld || jumpProfileElapsed < jumpProfileMinDuration;

        if (canContinueRise)
            targetElapsed = Mathf.Min(jumpProfileRiseDuration, jumpProfileElapsed + Time.fixedDeltaTime);

        float desiredHeight = SampleJumpProfileHeight(targetElapsed);
        var vel = rb.linearVelocity;
        vel.y = (jumpProfileStartY + desiredHeight - rb.position.y) / Time.fixedDeltaTime;
        rb.linearVelocity = vel;

        jumpProfileElapsed = targetElapsed;
        jumpTimeCounter = Mathf.Max(0f, jumpProfileRiseDuration - jumpProfileElapsed);

        if (!canContinueRise || jumpProfileElapsed >= jumpProfileRiseDuration)
            StopJumpProfile();
    }

    private float SampleJumpProfileHeight(float elapsed)
    {
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, jumpProfileRiseDuration));
        return DOVirtual.EasedValue(0f, jumpProfilePeakHeight, progress, jumpProfileEase);
    }

    private float GetJumpProfileVelocity(float elapsed)
    {
        float clampedElapsed = Mathf.Clamp(elapsed, 0f, jumpProfileRiseDuration);
        float currentHeight = SampleJumpProfileHeight(clampedElapsed);
        float nextHeight = SampleJumpProfileHeight(Mathf.Min(jumpProfileRiseDuration, clampedElapsed + Time.fixedDeltaTime));
        return (nextHeight - currentHeight) / Time.fixedDeltaTime;
    }

    private void StopJumpProfile()
    {
        isJumpProfileActive = false;
        jumpProfileElapsed = 0f;
        jumpProfileMinDuration = 0f;
        jumpTimeCounter = 0f;
    }

    private float GetCurrentBufferedRiseDuration()
    {
        bool useGroundJumpProfile = curJumpCount == 0 && (player.IsGrounded || coyoteTimeCounter > 0f);
        return useGroundJumpProfile ? stat.GroundJumpRiseDuration : stat.AirJumpRiseDuration;
    }

    // 슬램: 아래 삼방향 (하-좌대각, 하, 하-우대각) + 적 자동 추적 보정
    private IEnumerator SlamCoroutine()
    {
        if (player.isGuard || player.isPerfactGuard)
            EndGuard();

        slamInterrupted = false;
        player.isSlam = true;
        player.isPlaying = false;
        hasUsedAirSlam = true;
        StopJumpProfile();
        isJumpHeld = false;
        player.visual.SetActiveShield(false);
        rb.mass = defaultMass * stat.SlamMassMultiplier;

        // abs(moveDir.x) < 0.5 → 수직 낙하, 이상 → 45도 대각
        Vector2 slamDir;
        if (Mathf.Abs(moveDir.x) >= 0.5f)
            slamDir = new Vector2(Mathf.Sign(moveDir.x), -1f).normalized;
        else
            slamDir = Vector2.down;

        Transform slamTarget = FindBestSlamTarget(slamDir);
        if (slamTarget != null)
        {
            Vector2 toTarget = ((Vector2)slamTarget.position - rb.position).normalized;
            slamDir = Vector2.Lerp(slamDir, toTarget, stat.SlamAutoAimBlend).normalized;
        }

        currentSlamDir = slamDir; // 실제 슬램 방향 저장 (추적 보정 후) — 착지 시 속도 프로젝션에 사용
        onSlamStart?.Invoke(transform.position, currentSlamDir);

        float slamStartSpeed = stat.SlamSpeed * stat.SlamStartSpeedMultiplier;
        float slamEndSpeed = stat.SlamSpeed * stat.SlamEndSpeedMultiplier;
        float slamEaseDuration = EstimateSlamEaseDuration(slamDir);
        bool hitWallDuringSlam = false;

        // 착지할 때까지 슬램 속도 유지 (SlamLandingWindow 안전 타임아웃)
        float elapsed = 0f;
        Time.timeScale = 0.6f;
        yield return new WaitForSecondsRealtime(0.08f);
        Time.timeScale = 1;
        while (!player.IsGrounded && elapsed < stat.SlamLandingWindow && !slamInterrupted)
        {
            float slamProgress = Mathf.Clamp01(elapsed / slamEaseDuration);
            float slamSpeed = DOVirtual.EasedValue(
                slamStartSpeed,
                slamEndSpeed,
                slamProgress,
                stat.SlamSpeedEase
            );

            if (TryGetSlamWallHit(slamDir, slamSpeed * Time.fixedDeltaTime + 0.05f, out _))
            {
                hitWallDuringSlam = true;
                rb.linearVelocity = Vector2.zero;
                break;
            }

            rb.linearVelocity = slamDir * slamSpeed;
            elapsed += Time.fixedDeltaTime;
            yield return cachedWaitForFixedUpdate;
        }

        slamCor = null;
        player.isSlam = false;
        rb.mass = defaultMass;

        // 넉백 인터럽트: 대쉬·점프 횟수 1씩 회복 (최대치 초과 불가), 슬램 재사용 허용
        if (slamInterrupted)
        {
            curDashCount = Mathf.Min(curDashCount + 1, stat.MaxDashCount);
            curJumpCount = Mathf.Max(curJumpCount - 1, 0);
            hasUsedAirSlam = false;
            slamInterrupted = false;
            player.isPlaying = true;
            yield break;
        }

        if (hitWallDuringSlam)
        {
            player.isPlaying = true;
            yield break;
        }

        slamHit = GetFirstSolidGroundHit(groundCheck.position, Vector2.down, stat.GroundCheckDistance, stat.GroundLayer);

        if (player.IsGrounded)
        {
            Vector2 impactPosition = transform.position;
            Vector2 impactDirection = rb.linearVelocity.normalized;
            impactDirection.y = -1f;

            Color impactColor = Color.white;

            if (slamHit.collider != null)
            {
                slopeNormal = slamHit.normal;
                var sv = rb.linearVelocity;
                if (sv.y < 0f) { sv.y = 0f; rb.linearVelocity = sv; }

                Platform platform = slamHit.collider.GetComponentInParent<Platform>();

                if (platform != null)
                {
                    impactColor = platform.GetPlatformColor();
                }
            }

            onSlamImpact?.Invoke(impactPosition, impactDirection, impactColor);

            yield return PerformSlamImpactStop();
        }
        player.isPlaying = true;
        // 타임아웃 종료 시 slamLanding 없이 그냥 복귀 (정상 낙하로 전환)
    }

    private Transform FindBestSlamTarget(Vector2 slamDir)
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, stat.SlamDetectRange);
        HashSet<Transform> visitedTargets = new HashSet<Transform>();

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (candidate == null) continue;

            Transform target = ResolveSlamAutoAimTarget(candidate);
            if (target == null || !visitedTargets.Add(target)) continue;

            Vector2 targetPosition = candidate.bounds.center;
            Vector2 toTarget = targetPosition - rb.position;
            if (toTarget.sqrMagnitude <= Mathf.Epsilon || toTarget.y >= 0f) continue;

            float angleDelta = Vector2.Angle(slamDir, toTarget);
            if (angleDelta > stat.SlamAutoAimAngle) continue;
            if (!CanAutoAimSlamTarget(target, toTarget)) continue;

            float distanceScore = toTarget.magnitude / Mathf.Max(0.01f, stat.SlamDetectRange);
            float angleScore = angleDelta / Mathf.Max(0.01f, stat.SlamAutoAimAngle);
            float totalScore = distanceScore + angleScore;

            if (totalScore < bestScore)
            {
                bestScore = totalScore;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private Transform ResolveSlamAutoAimTarget(Collider2D candidate)
    {
        if (candidate == null)
            return null;

        BaseEnemy enemy = candidate.GetComponentInParent<BaseEnemy>();
        if (enemy != null)
            return enemy.transform;

        return FindTaggedSlamAutoAimTarget(candidate.transform);
    }

    private Transform FindTaggedSlamAutoAimTarget(Transform candidate)
    {
        if (candidate == null || stat.SlamAutoAimTags == null || stat.SlamAutoAimTags.Length == 0)
            return null;

        Transform current = candidate;
        while (current != null)
        {
            if (HasSlamAutoAimTag(current))
                return current;

            current = current.parent;
        }

        return null;
    }

    private bool HasSlamAutoAimTag(Transform target)
    {
        if (target == null || stat.SlamAutoAimTags == null)
            return false;

        for (int i = 0; i < stat.SlamAutoAimTags.Length; i++)
        {
            string tag = stat.SlamAutoAimTags[i];
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (target.CompareTag(tag))
                return true;
        }

        return false;
    }

    private bool CanAutoAimSlamTarget(Transform target, Vector2 toTarget)
    {
        if (target == null)
            return false;

        BaseEnemy enemy = target.GetComponent<BaseEnemy>();
        if (enemy != null && !enemy.CanSlam())
            return false;

        float distance = toTarget.magnitude;
        if (distance <= Mathf.Epsilon)
            return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(rb.position, toTarget.normalized, distance + 0.05f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (IsSlamAutoAimTargetHit(hitCollider, target))
                return true;

            if (hitCollider.isTrigger)
                continue;

            if (hitCollider.transform.IsChildOf(transform))
                continue;

            if (hitCollider.gameObject.CompareTag("HalfPlatform"))
                continue;

            return false;
        }

        return false;
    }

    private bool IsSlamAutoAimTargetHit(Collider2D hitCollider, Transform target)
    {
        if (hitCollider == null || target == null)
            return false;

        BaseEnemy hitEnemy = hitCollider.GetComponentInParent<BaseEnemy>();
        if (hitEnemy != null)
            return hitEnemy.transform == target;

        return hitCollider.transform == target || hitCollider.transform.IsChildOf(target);
    }

    private float EstimateSlamEaseDuration(Vector2 slamDir)
    {
        float fallbackDuration = ClampSlamEaseDuration(stat.SlamEaseFallbackDuration);
        float referenceSpeed = GetSlamReferenceSpeed();
        if (referenceSpeed <= 0f)
            return fallbackDuration;

        float castDistance = GetSlamCastDistance();
        int slamMask = stat.GroundLayer.value | stat.EnemyLayer.value;
        RaycastHit2D hit = Physics2D.Raycast(rb.position, slamDir, castDistance, slamMask);

        if (hit.collider == null)
            return fallbackDuration;

        float estimatedDuration = hit.distance / referenceSpeed;
        return ClampSlamEaseDuration(estimatedDuration);
    }

    private float GetSlamReferenceSpeed()
    {
        float averageMultiplier = (stat.SlamStartSpeedMultiplier + stat.SlamEndSpeedMultiplier) * 0.5f;
        return Mathf.Max(0.01f, stat.SlamSpeed * averageMultiplier);
    }

    private float GetSlamCastDistance()
    {
        float maxSpeed = stat.SlamSpeed * Mathf.Max(stat.SlamStartSpeedMultiplier, stat.SlamEndSpeedMultiplier);
        return Mathf.Max(stat.SlamDetectRange, maxSpeed * stat.SlamLandingWindow);
    }

    private float ClampSlamEaseDuration(float duration)
    {
        float minDuration = Mathf.Max(MinSlamEaseDuration, Time.fixedDeltaTime * 2f);
        float maxDuration = Mathf.Min(
            stat.SlamLandingWindow,
            Mathf.Max(minDuration, stat.SlamEaseFallbackDuration * MaxSlamEaseFallbackMultiplier)
        );

        return Mathf.Clamp(duration, minDuration, maxDuration);
    }

    private bool TryGetSlamWallHit(Vector2 slamDir, float distance, out RaycastHit2D wallHit)
    {
        wallHit = default;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
            return false;

        ContactFilter2D filter = default;
        filter.SetLayerMask(stat.GroundLayer);
        filter.useTriggers = false;

        int hitCount = playerCollider.Cast(slamDir, filter, slamBlockHitResults, distance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = slamBlockHitResults[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;

            if (hit.collider.gameObject.CompareTag("HalfPlatform"))
                continue;

            // 수직에 가까운 면만 벽으로 보고 슬램을 끊는다.
            if (Mathf.Abs(hit.normal.x) < 0.5f)
                continue;

            wallHit = hit;
            return true;
        }

        return false;
    }

    private IEnumerator PerformSlamImpactStop()
    {
        if (stat.SlamImpactStopDuration <= 0f)
            yield break;

        rb.linearVelocity = Vector2.zero;
        var wait = new WaitForSeconds(stat.SlamImpactStopDuration);
        yield return wait;
    }
    #endregion

    #region 넉백
    private Coroutine knockbackCor;

    public void CancelDash()
    {
        if (dashCor == null) return;
        StopCoroutine(dashCor);
        dashCor = null;
        player.isDash = false;
        dashToSlamGraceUntilTime = 0f;
        player.isPlaying = true;
        player.visual?.StopDashTrail();
    }

    public void ResetAirSlamUsage()
    {
        hasUsedAirSlam = false;
    }

    public Player.GuardType OnKnockback(Vector2 knockbackDir, float knockbackForce, float guardKnockbackMultiplier = -1f)
    {

        // 현재 가드 상태 확인
        Player.GuardType _guardType = Player.GuardType.Normal;

        if (player.isPerfactGuard)
            _guardType = Player.GuardType.PerfectGuard;
        else if (player.isGuard)
            _guardType = Player.GuardType.Guard;

        // 일반 가드: 호출부 지정값 우선, 미지정(-1)이면 stat 사용. 퍼펙트 가드: early return으로 넉백 0
        float _guardKnockbackMultiplier = (_guardType == Player.GuardType.Guard)
            ? (guardKnockbackMultiplier >= 0f ? guardKnockbackMultiplier : stat.GuardKnockbackMultiplier)
            : 1f;

        // 퍼펙트 가드
        if (player.isPerfactGuard)
        {
            Vector2 incomingDir = knockbackDir.sqrMagnitude > 0f ? knockbackDir.normalized : Vector2.down;
            Debug.Log($"[PlayerController] Perfect Guard!");
            onPerfectGuardSuccess?.Invoke(transform.position, -incomingDir);

            if (!stat.UseGuardGaugeMode)
                _guardCooldownTimer = stat.GuardCooldownTime;
            
            EndGuard();
            return _guardType;
        }

        // 일반 가드로 막은 경우 쿨타임 시작
        if (!stat.UseGuardGaugeMode && player.isGuard)
        {
            _guardCooldownTimer = stat.GuardCooldownTime;
        }

        EndGuard();
        // 슬램 도중 넉백 → while 루프 강제 탈출
        if (slamCor != null)
            slamInterrupted = true;

        CancelDash();

        var shakeDir = rb.linearVelocity;
        shakeDir.y = -1f;
        onSlamEnemyImpact?.Invoke(transform.position, shakeDir);

        float force = (_guardType == Player.GuardType.Guard)
            ? knockbackForce * _guardKnockbackMultiplier
            : knockbackForce;

        rb.linearVelocity = knockbackDir.normalized * force;

        if (knockbackCor != null) StopCoroutine(knockbackCor);
        knockbackCor = StartCoroutine(KnockbackRecoveryCoroutine());

        return _guardType;
    }

    private IEnumerator KnockbackRecoveryCoroutine()
    {
        player.isPlaying = false;
        var wait = new WaitForSeconds(stat.KnockbackRecoveryTime);
        yield return wait;
        player.isPlaying = true;
        moveElapsedTime = 0f;
        decelElapsedTime = 0f;
        knockbackCor = null;
    }

    public void OnBounce(Vector2 reflectedVelocity)
    {
        // 슬램/대쉬 중이면 취소
        if (slamCor != null) slamInterrupted = true;
        CancelDash();

        // FallGravity로 빠르게 떨어진 만큼 RiseGravity로 보정
        // 보정 없으면 상승 높이 = 낙하 높이 × (Fall / Rise) 배가 됨
        if (reflectedVelocity.y > 0f)
            reflectedVelocity.y *= Mathf.Sqrt(stat.RiseGravityMultiplier / stat.FallGravityMultiplier);

        rb.linearVelocity = reflectedVelocity;
        moveElapsedTime = 0f;
        decelElapsedTime = 0f;
        player.isPlaying = true;
    }
    #endregion

    #region 플랫폼
    private Vector2 platformVelocity;
    private Vector2 lastAppliedPlatformVelocity;
    private Platform_Moving currentPlatform;

    /// <summary>Platform_Moving에서 호출 — 클래스/인터페이스 참조 없이 속도만 동기화</summary>
    public void OnPlatformVelocityChanged(Platform_Moving platform, Vector2 linearVelocity)
    {
        currentPlatform = platform;
        platformVelocity = linearVelocity;
    }

    private RaycastHit2D GetFirstSolidGroundHit(Vector2 origin, Vector2 direction, float distance, LayerMask layerMask)
    {
        int hitCount = Physics2D.RaycastNonAlloc(origin, direction, groundHitResults, distance, layerMask);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHitResults[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;

            return hit;
        }

        return default;
    }

    private Collider2D GetFirstSolidOverlapBox(Vector2 point, Vector2 size, float angle, LayerMask layerMask)
    {
        ContactFilter2D filter = default;
        filter.SetLayerMask(layerMask);
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapBox(point, size, angle, filter, ceilingOverlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ceilingOverlapResults[i];
            if (hit == null || hit.isTrigger)
                continue;

            return hit;
        }

        return null;
    }

    private Vector2 GetEffectivePlatformVelocity(Vector2 sourceVelocity)
    {
        if (currentPlatform != null && currentPlatform.ShouldApplyVerticalPassengerVelocity)
        {
            float verticalVelocity = sourceVelocity.y < 0f ? sourceVelocity.y : 0f;
            return new Vector2(sourceVelocity.x, verticalVelocity);
        }

        return new Vector2(sourceVelocity.x, 0f);
    }

    private void RemovePreviousPlatformVelocity()
    {
        Vector2 effectiveVelocity = GetEffectivePlatformVelocity(lastAppliedPlatformVelocity);
        if (effectiveVelocity == Vector2.zero) return;

        var vel = rb.linearVelocity;
        vel -= effectiveVelocity;
        rb.linearVelocity = vel;
        lastAppliedPlatformVelocity = Vector2.zero;
    }

    private bool ShouldApplyPlatformVelocity()
    {
        if (player == null || currentPlatform == null)
            return false;

        if (player.IsGrounded)
            return true;

        return currentPlatform.ShouldApplyVerticalPassengerVelocity &&
               !isJumpProfileActive &&
               !player.isDash &&
               !player.isSlam;
    }

    private void ApplyPlatformVelocity()
    {
        Vector2 effectiveVelocity = GetEffectivePlatformVelocity(platformVelocity);
        if (effectiveVelocity == Vector2.zero)
        {
            platformVelocity = Vector2.zero;
            return;
        }

        var vel = rb.linearVelocity;
        vel += effectiveVelocity;
        rb.linearVelocity = vel;
        lastAppliedPlatformVelocity = effectiveVelocity;
        platformVelocity = Vector2.zero; // 적용 후 초기화 (다음 프레임 중복 방지)
    }

    private void OnDrawGizmosSelected()
    {
        PlayerStatus currentStatus = stat != null ? stat : GetComponent<PlayerStatus>();
        if (currentStatus == null)
            return;

        DrawJumpHeightGizmos(currentStatus);

        Vector3 origin = Application.isPlaying && rb != null ? rb.position : transform.position;
        Vector2 slamDir = GetGizmoSlamDirection();
        float range = currentStatus.SlamDetectRange;
        float halfAngle = currentStatus.SlamAutoAimAngle;

        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(origin, range);

        Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
        Gizmos.DrawLine(origin, origin + (Vector3)(slamDir * range));

        Vector2 leftBound = Quaternion.Euler(0f, 0f, halfAngle) * slamDir;
        Vector2 rightBound = Quaternion.Euler(0f, 0f, -halfAngle) * slamDir;

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawLine(origin, origin + (Vector3)(leftBound * range));
        Gizmos.DrawLine(origin, origin + (Vector3)(rightBound * range));

        const int segmentCount = 24;
        Vector3 previousPoint = origin + (Vector3)(rightBound * range);
        for (int i = 1; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 arcDir = Quaternion.Euler(0f, 0f, angle) * slamDir;
            Vector3 nextPoint = origin + (Vector3)(arcDir * range);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private void DrawJumpHeightGizmos(PlayerStatus currentStatus)
    {
        Collider2D currentCollider = GetComponent<Collider2D>();
        if (currentCollider == null)
            return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 20f, currentStatus.GroundLayer);
        if (!hit)
            return;

        Vector3 groundPos = hit.point;
        Vector3 halfColliderHeight = Vector3.up * currentCollider.bounds.extents.y;
        Vector3 boxSize = currentCollider.bounds.size;

        float firstJumpHeight = currentStatus.GroundJumpPeakHeight;
        float doubleJumpHeight = firstJumpHeight + currentStatus.AirJumpPeakHeight;

        Vector3 firstJumpTarget = groundPos + Vector3.up * firstJumpHeight + halfColliderHeight;
        Vector3 doubleJumpTarget = groundPos + Vector3.up * doubleJumpHeight + halfColliderHeight;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundPos, firstJumpTarget);
        Gizmos.DrawWireCube(firstJumpTarget, boxSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(firstJumpTarget, doubleJumpTarget);
        Gizmos.DrawWireCube(doubleJumpTarget, boxSize);
    }

    private Vector2 GetGizmoSlamDirection()
    {
        float horizontalInput = Application.isPlaying ? moveDir.x : Mathf.Sign(transform.localScale.x);
        if (Mathf.Abs(horizontalInput) >= 0.5f)
            return new Vector2(Mathf.Sign(horizontalInput), -1f).normalized;

        return Vector2.down;
    }
    #endregion
}
