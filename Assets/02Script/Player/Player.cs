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
    public event Action<int> onDamaged;
    public event Action onDied;
    #endregion

    #region 상태체크
    public bool isPerfactGuard;
    public bool isSlam;
    public bool isDash;
    public bool isPlaying = true;
    public bool isGuard;
    [SerializeField] private bool isInvincible = false;
    Coroutine invincibleCor;
    public bool ForcedHorizontalReleaseActive { get; private set; }

    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded { get; private set; }
    public bool WasGrounded { get; private set; }
    #endregion

    void Awake()
    {
        Instance = this;
        controller ??= GetComponent<PlayerController>();
        status ??= GetComponent<PlayerStatus>();
        visual ??= GetComponent<PlayerVisual>();
        visual.Init(this);
        controller.Init(this);
    }

    void Start()
    {
        controller.SubscribeInput();
        StageManager.OnStageClear -= RecoveryHp;
        StageManager.OnStageClear += RecoveryHp;
    }

    public void Init()
    {
        controller ??= GetComponent<PlayerController>();
        status ??= GetComponent<PlayerStatus>();
        visual ??= GetComponent<PlayerVisual>();
        visual.Init(this);
        controller.Init(this);
    }

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
        SetPlayingState(true);
    }

    public void UpdateGroundState(bool isGrounded)
    {
        WasGrounded = IsGrounded;
        IsGrounded = isGrounded;
    }

    public void UpdateFacingDirection(int dir)
    {
        FacingDirection = dir;
    }

    #region 상태 변경 메서드
    public void SetPlayingState(bool value)
    {
        isPlaying = value;
    }

    public void SetDashState(bool value)
    {
        isDash = value;
    }

    public void SetSlamState(bool value)
    {
        isSlam = value;
    }

    public void SetGuardState(bool value)
    {
        isGuard = value;
    }

    public void SetPerfactGuardState(bool value)
    {
        isPerfactGuard = value;
    }

    public void ClearGuardStates()
    {
        isGuard = false;
        isPerfactGuard = false;
    }

    public void ResetActionStates()
    {
        ClearGuardStates();
        SetSlamState(false);
        SetDashState(false);
        SetPlayingState(true);
    }
    #endregion

    public void ResetPlayerState()
    {
        ResetActionStates();
        WasGrounded = true;
        IsGrounded = true;
        UpdateFacingDirection(1);
        SetInvincible(false);

        visual.ReleaseInvincible();
        invincibleCor = null;
        StopAllCoroutines();
    }

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