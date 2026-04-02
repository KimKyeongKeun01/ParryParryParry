using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CameraShake cameraShake;

    [Header("Shake Profile")]
    [SerializeField] private ScreenShakeProfile slamGroundShakeProfile;
    [SerializeField] private ScreenShakeProfile slamEnemyHitShakeProfile;
    [SerializeField] private ScreenShakeProfile perfectGuardShakeProfile;

    [Header("Impulse Source")]
    public CinemachineImpulseSource impulseSource;

    [SerializeField] private CinemachineImpulseListener currentImpulseListener;

    private readonly List<CinemachineCamera> _cameras = new();
    private CinemachineCamera _currentVirtualCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // 스테이지 초기화 시 StageManager에서 호출
    public void RegisterCameras(List<CinemachineCamera> cameras)
    {
        _cameras.Clear();
        foreach (var vcam in cameras)
        {
            _cameras.Add(vcam);
            if (vcam != null) vcam.gameObject.SetActive(false);
        }
        SwitchToCamera(0);
    }

    // 스테이지 리셋 시 해당 인덱스 카메라 교체
    public void UpdateCameraAt(int index, CinemachineCamera vcam)
    {
        if (index < 0 || index >= _cameras.Count) return;
        _cameras[index] = vcam;
    }

    // 스테이지 전환 시 호출 — 이전 카메라 끄고 새 카메라 켬
    public void SwitchToCamera(int index)
    {
        if (_currentVirtualCamera != null)
            _currentVirtualCamera.gameObject.SetActive(false);

        _currentVirtualCamera = (index >= 0 && index < _cameras.Count) ? _cameras[index] : null;

        if (_currentVirtualCamera != null)
        {
            _currentVirtualCamera.gameObject.SetActive(true);
            currentImpulseListener = _currentVirtualCamera.GetComponentInChildren<CinemachineImpulseListener>();
        }
        else
        {
            currentImpulseListener = null;
        }
    }

    public CinemachineCamera GetCurrentVirtualCamera() => _currentVirtualCamera;

    public void SnapCurrentCamera()
    {
        if (_currentVirtualCamera != null)
            _currentVirtualCamera.PreviousStateIsValid = false;
    }

    public void PlaySlamGroundShake(Vector2 impactPos, Vector2 impactDir)
    {
        PlayShake(slamGroundShakeProfile, impactPos, impactDir);
    }

    public void PlaySlamEnemyHitShake(Vector2 impactPos, Vector2 impactDir)
    {
        PlayShake(slamEnemyHitShakeProfile, impactPos, impactDir);
    }

    public void PlayPerfectGuardShake(Vector2 impactPos, Vector2 impactDir)
    {
        PlayShake(perfectGuardShakeProfile, impactPos, impactDir);
    }

    public void PlayShake(ScreenShakeProfile profile, Vector2 pos, Vector2 dir)
    {
        if (cameraShake == null || profile == null || impulseSource == null || currentImpulseListener == null)
        {
            Debug.LogWarning(
                $"[CameraManager] PlayShake 중단 | cameraShake={cameraShake}, profile={profile}, " +
                $"source={impulseSource}, listener={currentImpulseListener}"
            );
            return;
        }

        impulseSource.transform.position = pos;
        impulseSource.DefaultVelocity = new Vector3(dir.x, dir.y, 0f);
        cameraShake.ScreenShakeFromProfile(profile, impulseSource, currentImpulseListener);
    }
}
