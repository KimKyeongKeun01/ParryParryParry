using UnityEngine;
using DG.Tweening;

public class PlayerStatus : MonoBehaviour
{
    Player player;

    #region
    [field: Header("Ground Movement")]
    [field: Tooltip("지상에서의 기본 이동 속도"), Min(0f), SerializeField]
    public float MoveSpeed { get; private set; } = 11f;

    [field: Tooltip("최대 수평 이동 속도 제한 여부"), SerializeField]
    public bool LimitMaxHorizontalSpeed { get; private set; } = true;

    [field: Tooltip("도달할 수 있는 최대 수평 이동 속도"), Min(0f), SerializeField]
    public float MaxHorizontalSpeed { get; private set; } = 11f;

    [field: Tooltip("지상에서 이동할 때 가속도"), Min(0f), SerializeField]
    public float Acceleration { get; private set; } = 120f;

    [field: Tooltip("지상에서 이동을 멈출 때 감속도"), Min(0f), SerializeField]
    public float Deceleration { get; private set; } = 150f;

    [field: Tooltip("공중에서 이동 방향을 제어할 수 있는 비율"), Range(0f, 1f), SerializeField]
    public float AirControlPercent { get; private set; } = 1f;

    [field: Tooltip("공중에서의 공기 저항"), Min(0f), SerializeField]
    public float AirResistance { get; private set; } = 80f;

    [field: Tooltip("착지 직후 기존의 이동 관성이 유지되는 시간"), Min(0f), SerializeField]
    public float LandingMomentumTime { get; private set; } = 0.05f;

    [field: Tooltip("착지 시 적용되는 감속 배율"), Min(0f), SerializeField]
    public float LandingDecelerationMultiplier { get; private set; } = 0.1f;
    #endregion

    #region
    [field: Space(8f), Header("Jump")]
    [field: HideInInspector, SerializeField]
    public float JumpForce { get; private set; } = 16f;

    [field: Tooltip("연속으로 점프할 수 있는 최대 횟수"), Min(1), SerializeField]
    public int MaxJumpCount { get; private set; } = 2;

    [field: Tooltip("지상 점프 최고점 높이"), Min(0f), SerializeField]
    public float GroundJumpPeakHeight { get; private set; } = 4.4f;

    [field: Tooltip("공중 점프 최고점 높이"), Min(0f), SerializeField]
    public float AirJumpPeakHeight { get; private set; } = 3.4f;

    [field: Tooltip("지상 점프가 최고점까지 도달하는 시간"), Min(0.01f), SerializeField]
    public float GroundJumpRiseDuration { get; private set; } = 0.55f;

    [field: Tooltip("공중 점프가 최고점까지 도달하는 시간"), Min(0.01f), SerializeField]
    public float AirJumpRiseDuration { get; private set; } = 0.48f;

    [field: Tooltip("지상 점프 상승 Ease"), SerializeField]
    public Ease GroundJumpEase { get; private set; } = Ease.OutCubic;

    [field: Tooltip("공중 점프 상승 Ease"), SerializeField]
    public Ease AirJumpEase { get; private set; } = Ease.OutCubic;

    [field: Tooltip("점프 버튼을 빨리 떼었을 때 보장되는 최소 점프 비율"), Range(0f, 1f), SerializeField]
    public float JumpCutMinHeightRatio { get; private set; } = 0.35f;

    [field: HideInInspector, SerializeField]
    public float MaxJumpTime { get; private set; } = 0.1f;

    [field: HideInInspector, SerializeField]
    public float JumpHoldForce { get; private set; } = 18f;

    [field: HideInInspector, SerializeField]
    public float InAirJumpForce { get; private set; } = 14f;

    [field: HideInInspector, SerializeField]
    public float JumpCutMultiplier { get; private set; } = 0.3f;

    [field: Tooltip("상승 중일 때 적용되는 중력 배율"), Min(0f), SerializeField]
    public float RiseGravityMultiplier { get; private set; } = 3f;

    [field: Tooltip("하강 중일 때 적용되는 중력 배율"), Min(0f), SerializeField]
    public float FallGravityMultiplier { get; private set; } = 6f;

    [field: Tooltip("하강 시 가속도"), Min(0f), SerializeField]
    public float FallAcceleration { get; private set; } = 50f;

    [field: Tooltip("도달 가능한 최대 하강 속도"), Min(0f), SerializeField]
    public float MaxFallSpeed { get; private set; } = 30f;

    [field: Tooltip("지상에 있을 때 바닥에 밀착하기 위해 유지되는 수직 속도"), SerializeField]
    public float GroundedVerticalVelocity { get; private set; } = -2f;

    [field: Tooltip("바닥 레이어 마스크"), SerializeField]
    public LayerMask GroundLayer { get; private set; }

    [field: Tooltip("바닥 감지 거리"), Min(0f), SerializeField]
    public float GroundCheckDistance { get; private set; } = 0.1f;

    [field: Tooltip("바닥 감지 범위의 반경 비율"), Range(0f, 1f), SerializeField]
    public float GroundCheckRadiusScale { get; private set; } = 0.45f;
    #endregion

    #region 대시
    [field: Space(8f), Header("Dash")]
    [field: Tooltip("대시 속도"), Min(0f), SerializeField]
    public float DashSpeed { get; private set; } = 18f;
    [field: Tooltip("대시 지속 시간"), Min(0f), SerializeField]
    public float dashDuration { get; private set; } = 0.2f;
    [field: Min(0f), SerializeField]
    public float DashToSlamGraceTime { get; private set; } = 0.12f;
    [field: Tooltip("대시 쿨타임 (공중에서도 감소하지만 충전은 착지 시에만)"), Min(0f), SerializeField]
    public float DashCooldownTime { get; private set; } = 1f;
    [field: Tooltip("최대 대쉬 사용 횟수 (착지 시 전량 충전)"), Min(1), SerializeField]
    public int MaxDashCount { get; private set; } = 1;
    [field: SerializeField]
    public Ease DashSpeedEase { get; private set; } = Ease.OutExpo;
    [field: Min(0f), SerializeField]
    public float DashStartSpeedMultiplier { get; private set; } = 1f;
    [field: Min(0f), SerializeField]
    public float DashEndSpeedMultiplier { get; private set; } = 1f;
    #endregion

    #region 이동
    [field: Space(8f), Header("Movement Curves")]
    [field: Tooltip("이동 시작 시 속도 가속 곡선 (X축: 0~1 정규화 시간, Y축: Lerp 비율 0~1)"), SerializeField]
    public AnimationCurve AccelerationCurve { get; private set; }
    [field: Tooltip("가속 도달 시간 (초) — 이 시간 동안 AccelerationCurve X축 0→1 진행"), Min(0.01f), SerializeField]
    public float AccelerationTime { get; private set; } = 0.3f;
    [field: Tooltip("공중 가속 도달 시간 (초) — 이 시간 동안 AccelerationCurve X축 0→1 진행"), Min(0.01f), SerializeField]
    public float InAirAccelerationTime { get; private set; } = 0.3f;
    [field: Tooltip("이동 멈출 시 속도 감속 곡선 (X축: 0~1 정규화 시간, Y축: Lerp 비율 0~1)"), SerializeField]
    public AnimationCurve DecelerationCurve { get; private set; }
    [field: Tooltip("감속 도달 시간 (초) — 이 시간 동안 DecelerationCurve X축 0→1 진행"), Min(0.01f), SerializeField]
    public float DecelerationTime { get; private set; } = 0.15f;
    [field: Tooltip("공중 감속 도달 시간 (초) — 이 시간 동안 DecelerationCurve X축 0→1 진행"), Min(0.01f), SerializeField]
    public float InAirDecelerationTime { get; private set; } = 0.15f;
    #endregion

    #region 가드
    [field: Space(8f), Header("Guard")]
    [field: Tooltip("퍼펙트 가드 판정 시간"), Min(0f), SerializeField]
    public float PerfectGuardWindow { get; private set; } = 0.25f;
    [field: Tooltip("버튼을 떼도 가드가 유지되는 최소 시간 (초)"), Min(0f), SerializeField]
    public float GuardMinDuration { get; private set; } = 1f;
    [field: Tooltip("가드 중 넉백 감소 비율 (0 = 완전 차단, 1 = 원본)"), Range(0f, 1f), SerializeField]
    public float GuardKnockbackMultiplier { get; private set; } = 0.2f;
    [field: Tooltip("방패 가드 시 질량 배율 (무게감/저항력 증가)"), Min(1f), SerializeField]
    public float GuardMassMultiplier { get; private set; } = 2f;
    [field: Tooltip("넉백 후 플레이어 조작이 복구되는 시간"), Min(0f), SerializeField]
    public float KnockbackRecoveryTime { get; private set; } = 0.25f;

    [field: Space(4f), Tooltip("가드 제한 방식 — 비활성: 쿨타임 / 활성: 게이지"), SerializeField]
    public bool UseGuardGaugeMode { get; private set; } = false;

    [field: Header("Guard - 쿨타임 모드")]
    [field: Tooltip("가드 해제 후 재사용 가능까지 대기 시간"), Min(0f), SerializeField]
    public float GuardCooldownTime { get; private set; } = 0.5f;

    [field: Header("Guard - 게이지 모드")]
    [field: Tooltip("최대 가드 게이지"), Min(0f), SerializeField]
    public float MaxGuardGauge { get; private set; } = 100f;
    [field: Tooltip("가드 중 게이지 소모량 (초당)"), Min(0f), SerializeField]
    public float GuardDrainRate { get; private set; } = 20f;
    [field: Tooltip("가드 해제 후 회복량 (초당)"), Min(0f), SerializeField]
    public float GuardRecoveryRate { get; private set; } = 30f;
    [field: Tooltip("가드 해제 후 회복 시작 대기 시간"), Min(0f), SerializeField]
    public float GuardRecoveryDelay { get; private set; } = 1f;
    #endregion

    #region
    [field: Space(8f), Header("Shield")]
    [field: Tooltip("방패 던지기 속도"), Min(0f), SerializeField]
    public float ShieldThrowSpeed { get; private set; } = 20f;
    [field: Tooltip("방패 회수 홀드 시간 (n초 이상 누르면 회수)"), Min(0f), SerializeField]
    public float ShieldRecallHoldTime { get; private set; } = 1f;
    [field: Tooltip("방패 회수 비행 속도"), Min(0f), SerializeField]
    public float ShieldRecallSpeed { get; private set; } = 25f;
    [field: Tooltip("방패가 박히는 레이어"), SerializeField]
    public LayerMask ShieldStickLayer { get; private set; }
    [field: Tooltip("방패가 닿자마자 즉시 회수되는 벽 레이어"), SerializeField]
    public LayerMask ShieldAutoRecallLayer { get; private set; }
    [field: Tooltip("방패 자동 회수 시간"), Min(0f), SerializeField]
    public float ShieldAutoRecallDelay { get; private set; } = 2f;
    [field: Tooltip("방패 흔들림 지속 시간"), Min(0f), SerializeField]
    public float ShieldWobbleDuration { get; private set; } = 0.5f;
    #endregion

    #region
    [field: Space(8f), Header("Slam")]
    [field: Tooltip("슬램 낙하 속도"), Min(0f), SerializeField]
    public float SlamSpeed { get; private set; } = 25f;
    [field: Tooltip("슬램 속도 Ease"), SerializeField]
    public Ease SlamSpeedEase { get; private set; } = Ease.InQuad;
    [field: Tooltip("슬램 시작 속도 배율"), Min(0f), SerializeField]
    public float SlamStartSpeedMultiplier { get; private set; } = 0.6f;
    [field: Tooltip("슬램 종료 속도 배율"), Min(0f), SerializeField]
    public float SlamEndSpeedMultiplier { get; private set; } = 1f;
    [field: Tooltip("예상 착지 시간을 못 구했을 때 사용할 Ease 지속 시간"), Min(0.01f), SerializeField]
    public float SlamEaseFallbackDuration { get; private set; } = 0.22f;
    [field: Tooltip("슬램 전용 쿨타임 (대쉬 쿨타임과 독립)"), Min(0f), SerializeField]
    public float SlamCooldownTime { get; private set; } = 1f;
    [field: Tooltip("슬램 시 질량 배율 (관통력/착지 무게감 증가)"), Min(1f), SerializeField]
    public float SlamMassMultiplier { get; private set; } = 3f;
    [field: Tooltip("슬램 착지 판정 유효 시간 — 초과 시 slamLanding 자동 취소"), Min(0f), SerializeField]
    public float SlamLandingWindow { get; private set; } = 1.5f;
    [field: Tooltip("대쉬 키 + 이 값 이하의 y 입력 시 수평 대쉬 대신 슬램 실행 (공중 한정)"), Range(-1f, 0f), SerializeField]
    public float SlamDashThreshold { get; private set; } = -0.5f;
    [field: Tooltip("슬램 자동 추적 최대 각도"), Range(0f, 90f), SerializeField]
    public float SlamAutoAimAngle { get; private set; } = 50f;
    [field: Tooltip("슬램 자동 추적 보정 강도"), Range(0f, 1f), SerializeField]
    public float SlamAutoAimBlend { get; private set; } = 0.3f;
    [field: SerializeField]
    public string[] SlamAutoAimTags { get; private set; } = new string[0];
    [field: Tooltip("슬램 적 자동 탐지 레이어"), SerializeField]
    public LayerMask EnemyLayer { get; private set; }
    [field: Tooltip("슬램 적 자동 탐지 범위"), Min(0f), SerializeField]
    public float SlamDetectRange { get; private set; } = 5f;
    [field: Tooltip("슬램 착지 직후 잠깐 멈추는 시간"), Min(0f), SerializeField]
    public float SlamImpactStopDuration { get; private set; } = 0.05f;
    [field: Tooltip("경사면으로 판정하는 최소 경사 각도 (경사 이동 프로젝션에 사용)"), Range(0f, 90f), SerializeField]
    public float SlopeAngleThreshold { get; private set; } = 15f;

    [field: Tooltip("슬램 착지 직후 적용할 액션 타임스케일 (0 = 완전 정지, 1 = 변화 없음)"), Range(0f, 1f), SerializeField]
    public float SlamImpactStopTimeScale { get; private set; } = 1f;
    #endregion

    #region
    [field: Space(8f), Header("Visual")]
    [field: Tooltip("이동 시 최대 기울기 각도"), Range(0f, 90f), SerializeField]
    public float MaxTiltAngle { get; private set; } = 30f;
    [field: Tooltip("기울기 보간 속도"), Min(0f), SerializeField]
    public float TiltSpeed { get; private set; } = 10f;
    #endregion

    #region
    [field: Space(8f), Header("Input Forgiveness")]
    [field: Tooltip("코요테 타임"), Min(0f), SerializeField]
    public float CoyoteTime { get; private set; } = 0.1f;

    [field: Tooltip("점프 버퍼 시간"), Min(0f), SerializeField]
    public float JumpBufferTime { get; private set; } = 0.1f;
    #endregion

    #region 체력
    [field: Tooltip("체력"), Min(1), SerializeField]
    public int maxHp { get; private set; }
    [SerializeField] private int curHp;
    [field: Tooltip("무적 시간"), Min(1), SerializeField]
    public float invincibleTime { get; private set; } = 0.5f;
    #endregion
    public void Init(Player _player)
    {
        player = _player;
    }

    public void Setup()
    {
        curHp = maxHp;
    }

    public int GetCurHp()
    {
        return curHp;
    }
    public bool TakeDamaged(int damage)
    {
        curHp -= damage;
        UIManager.Instance?.UpdateHealth();
        return curHp <= 0;
    }

    public void RecoveryHp()
    {
        curHp = maxHp;
        UIManager.Instance?.UpdateHealth(true);
    }
}
