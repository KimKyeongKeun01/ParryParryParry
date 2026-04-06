using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraAssist : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private Player _player;
    private CinemachinePositionComposer _composer;

    #region Y Snapping
    [Header(" = Y Snapping = ")]
    [Header("Damping")]
    [SerializeField] private float groundYHeight;
    [SerializeField] private float airboneYHeight;

    [Header("Landing Timing")]
    [SerializeField] private float landingDelay;
    [SerializeField] private float landingDuration;

    private bool wasGrounded;
    private Coroutine landingRoutine;
    #endregion

    #region Look a head
    private enum LookAheadState { Center, Left, Right };
    private LookAheadState currentState = LookAheadState.Center;
    private float currentScreenX;

    [Space(5)]
    [Header(" = Look a head = ")]
    [Header("Screen Position")]
    [SerializeField] private float centerScreenX;
    [SerializeField] private float screenLookAheadX;
    [SerializeField] private float screenSpeed;

    [Header("Timer")]
    [SerializeField] private float enterDelay;
    [SerializeField] private float returnDelay;
    [SerializeField] private float dirChangeDelay;
    private int lastMoveDir = 1;
    private float sameDirTimer;
    private float idleTimer;
    #endregion 



    private void Awake()
    {
        if (_camera == null) _camera = GetComponent<CinemachineCamera>();
        _composer = _camera.GetComponent<CinemachinePositionComposer>();
    }

    private void Start()
    {
        if (_player == null) _player = Player.Instance;

        #region Y Snapping
        // 기본 데드존 높이 받아오기
        groundYHeight = _composer.Composition.DeadZone.Size.y;
        #endregion

        #region Look a head
        // 기본 카메라 위치 받아오기
        currentScreenX = _composer.Composition.ScreenPosition.x;
        currentState = LookAheadState.Center;

        lastMoveDir = 0;
        sameDirTimer = 0;
        idleTimer = 0;
        #endregion
    }

    private void Update()
    {
        if (_composer == null || _player == null) return;

        #region Y Snapping
        bool isGrounded = _player.IsGrounded;

        // 공중에서
        if (!isGrounded && wasGrounded)
        {
            if (landingRoutine != null)
            {
                StopCoroutine(landingRoutine);
                landingRoutine = null;
            }

            // 댐핑 늘려서 카메라 y축 이동 방지
            SetYDamping(airboneYHeight);
        }

        // 착지시
        if (isGrounded && !wasGrounded)
        {
            if (landingRoutine != null) StopCoroutine(landingRoutine);

            // 댐핑 줄여서 카메라 y축 이동
            landingRoutine = StartCoroutine(Co_Landing());
        }

        wasGrounded = isGrounded;
        #endregion

        #region Look a head
        int moveDir = GetMoveDirection();
        UpdateState(moveDir);

        float targetScreenX = GetTargetScreenX();
        currentScreenX = Mathf.Lerp(currentScreenX, targetScreenX, Time.unscaledDeltaTime * screenSpeed);

        ScreenComposerSettings composition = _composer.Composition;
        composition.ScreenPosition.x = currentScreenX;
        _composer.Composition = composition;
        #endregion
    }

    #region Damping
    private void SetYDamping(float value)
    {
        if (_composer == null) return;

        var deadZone = _composer.Composition.DeadZone.Size;
        
        deadZone.y = value;
        _composer.Composition.DeadZone.Size = deadZone;
    }

    private IEnumerator Co_Landing()
    {
        // 착지 직후 잠깐 딜레이
        yield return new WaitForSecondsRealtime(landingDelay);

        // 데드존 복구
        SetYDamping(groundYHeight);
        landingRoutine = null;
    }
    #endregion

    #region Look a head
    private void UpdateState(int moveDir)
    {
        // 이동 없을 때
        if (moveDir == 0)
        {
            sameDirTimer = 0f;
            idleTimer += Time.unscaledDeltaTime;

            if (idleTimer >= returnDelay) currentState = LookAheadState.Center;

            lastMoveDir = 0;
            return;
        }

        idleTimer = 0f;

        // 이동 중
        if (moveDir == lastMoveDir) sameDirTimer += Time.unscaledDeltaTime;

        // 방향 변경시
        else
        {
            lastMoveDir = moveDir;
            sameDirTimer = 0f;
        }

        float requireDelay = currentState == LookAheadState.Center ? enterDelay : dirChangeDelay;

        if (sameDirTimer >= requireDelay) currentState = moveDir > 0 ? LookAheadState.Right : LookAheadState.Left;
    }

    private float GetTargetScreenX()    // 목표 Screen Position
    {
        switch (currentState)
        {
            case LookAheadState.Right:
                return -screenLookAheadX;
            case LookAheadState.Left:
                return screenLookAheadX;
            default:
                return centerScreenX;
        }
    }

    private int GetMoveDirection()      // 방향 계산
    {
        float velocityX = _player.controller.rb.linearVelocityX;

        if (velocityX > 0.1) return 1;
        if (velocityX < -0.1) return -1;
        return 0;
    }
    #endregion
}
