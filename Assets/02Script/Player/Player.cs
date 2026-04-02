using System;
using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    #region 기본 참조
    private static Player instance;
    public static Player Instance
    {
        get { return instance; }
        private set { instance = value; }
    }

    public enum GuardType { Normal, Guard, PerfectGuard }

    public PlayerController controller;
    public PlayerStatus status;
    public PlayerVisual visual;
    #endregion

    #region 이벤트
    /// <summary>데미지를 받을 때 호출 (남은 HP 전달)</summary>
    public event Action<int> onDamaged;
    /// <summary>사망 시 호출</summary>
    public event Action onDied;
    #endregion

    #region 상태체크
    /// <summary>퍼펙트 가드</summary>
    public bool isPerfactGuard;
    /// <summary>슬램 여부</summary>
    public bool isSlam;
    /// <summary>대쉬 사용 가능 여부 (사용 횟수 > 0)</summary>
    public bool isDash;
    /// <summary>플레이어 조작 가능 여부 — false이면 이동/조작 차단 (대쉬·슬램 실행 중)</summary>
    public bool isPlaying = true;
    /// <summary>가드 여부</summary>
    public bool isGuard;
    /// <summary>무적 여부</summary>
    [SerializeField] private bool isInvincible = false;
    Coroutine invincibleCor;
    /// <summary>강제 이동이 활성화될 때 수평 입력을 무시하기 위한 플래그</summary>
    public bool ForcedHorizontalReleaseActive { get; private set; }

    /// <summary>캐릭터가 바라보는 방향 (1 오른쪽, -1 왼쪽)</summary>
    public int FacingDirection { get; private set; } = 1;

    /// <summary>현재 프레임에 캐릭터가 바닥에 있는가?</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>이전 프레임에 캐릭터가 바닥에 있었는가?</summary>
    public bool WasGrounded { get; private set; }
    #endregion

    void Awake()
    {
        Instance = this;
        // 자기 자신 컴포넌트만 초기화 (다른 오브젝트 참조 없음)
        controller ??= GetComponent<PlayerController>();
        status     ??= GetComponent<PlayerStatus>();
        visual     ??= GetComponent<PlayerVisual>();
        visual.Init(this);
        controller.Init(this);
    }

    void Start()
    {
        // 다른 오브젝트(InputManager, StageManager) 참조는 Start에서
        controller.SubscribeInput();
        StageManager.OnStageClear -= RecoveryHp;
        StageManager.OnStageClear += RecoveryHp;
    }

    // 외부에서 재초기화 필요 시 호출 (Awake/Start에서 이미 처리됨)
    public void Init()
    {
        controller ??= GetComponent<PlayerController>();
        status     ??= GetComponent<PlayerStatus>();
        visual     ??= GetComponent<PlayerVisual>();
        visual.Init(this);
        controller.Init(this);
    }

    // 설정
    public void Setup(Vector2 resetPoint)
    {
        transform.position = resetPoint;
        ResetPlayerState();
        controller.Setup();
        status.Setup();
        visual.Setup();
    }

    public void ReleaseStop()
    {
        isPlaying = true;
    }

    /// <summary>PlayerController에서 매 FixedUpdate마다 지면 상태 갱신</summary>
    public void UpdateGroundState(bool isGrounded)
    {
        WasGrounded = IsGrounded;
        IsGrounded = isGrounded;
    }

    /// <summary>PlayerController에서 이동 방향 변경 시 갱신</summary>
    public void UpdateFacingDirection(int dir)
    {
        FacingDirection = dir;
    }

    private void ResetPlayerState()
    {
        isPerfactGuard = false;
        isSlam = false;
        isGuard = false;
        isDash = false;
        WasGrounded = true;
        IsGrounded = true;
        UpdateFacingDirection(1);
        SetInvincible(false);

        visual.ReleaseInvincible();
        invincibleCor = null;
        StopAllCoroutines();
    }

    /// <summary>스테이지 전환 시 호출 — HP는 유지하고 무적/방패 상태만 초기화</summary>
    public void ResetOnStageTransition()
    {
        SetInvincible(false);
        visual.ReleaseInvincible();
        if (invincibleCor != null)
        {
            StopCoroutine(invincibleCor);
            invincibleCor = null;
        }

        Shield shield = visual.ShieldInstance;
        if (shield != null && !shield.IsEquipped)
            shield.ForceEquip();
    }

    public void SetInvincible(bool _isInvincible)
    {
        isInvincible = _isInvincible;
    }

    public void RecoveryHp(int temp)
    {
        status.RecoveryHp();
    }

    public void TakeDamaged(int damage)
    {
        if (isInvincible) return;
        var curHp = status.GetCurHp();
        if (curHp <= 0 || invincibleCor != null) return;
        SetInvincible(true);

        invincibleCor = StartCoroutine(InvincibleCoolTime());
        visual.TakeDamagedVisual();
        if (status.TakeDamaged(damage))
        {
            onDamaged?.Invoke(status.GetCurHp());
            onDied?.Invoke();
            GameManager.Instance?.OnGameOver();
        }
        else
        {
            onDamaged?.Invoke(status.GetCurHp());
        }
    }

    IEnumerator InvincibleCoolTime()
    {
        yield return new WaitForSecondsRealtime(status.invincibleTime);
        SetInvincible(false);
        visual.ReleaseInvincible();
        invincibleCor = null;
    }
}
