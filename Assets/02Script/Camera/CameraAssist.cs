using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraAssist : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private Player _player;
    private CinemachinePositionComposer _composer;

    [Header("Y Damping")]
    [SerializeField] private float groundYHeight;
    [SerializeField] private float airboneYHeight;
    [SerializeField] private float landingYHeight;

    [Header("Landing Timing")]
    [SerializeField] private float landingDelay;
    [SerializeField] private float landingDuration;


    private bool wasGrounded;
    private Coroutine landingRoutine;

    private void Awake()
    {
        if (_camera == null) _camera = GetComponent<CinemachineCamera>();
        _composer = _camera.GetComponent<CinemachinePositionComposer>();

        // 기본 데드존 높이 받아오기
        groundYHeight = _composer.Composition.DeadZoneRect.y;
    }

    private void Start()
    {
        if (_player == null) _player = Player.Instance;
    }

    private void Update()
    {
        if (_composer == null || _player == null) return;

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
    }

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
}
