using Unity.Cinemachine;
using UnityEngine;

public class CameraShakeController : MonoBehaviour
{
    #region Predefined Profiles
    [Header("Predefined Profiles")]
    [SerializeField] private CameraShakeProfile recoilProfile;
    [SerializeField] private CameraShakeProfile bumpProfile;
    [SerializeField] private CameraShakeProfile explosionProfile;
    [SerializeField] private CameraShakeProfile rumbleProfile;
    #endregion

    [Header("Default Source")]
    [Tooltip("Source 미 호출시 사용할 기본 Source")]
    [SerializeField] private CinemachineImpulseSource defaultSource;

    [Header("Global Muliplier")]
    [Tooltip("모든 흔들림에 공통으로 곱해지는 전역 배율.")]
    [SerializeField, Min(0f)] private float globalForceMulti = 1f;
    [Tooltip("direction이 0일 때 대신 사용할 기본 2D 방향.")]
    [SerializeField] private Vector2 fallbackDirection = Vector2.down;


    #region Core
    /// <summary>
    /// 가장 기본적인 재생.
    /// source가 null일 경우 defaultSource 사용.
    /// </summary>
    /// <param name="profile">적용할 흔들림 프로필</param>
    /// <param name="direction">흔들림 방향. normalized 권장</param>
    /// <param name="source">흔들림을 방출할 Impulse Source. null이면 defaultSource 사용</param>
    /// <param name="extraForceScale">추가 세기 배율</param>
    public void Play(CameraShakeProfile profile, Vector2 direction, CinemachineImpulseSource source = null, float extraForceScale = 1f)
    {
        source ??= defaultSource;

        if (profile == null || source == null)
        {
            Debug.LogWarning("[CameraShakeController] Play interruption - profile 또는 source가 null");
            return;
        }

        ApplyProfileToSource(profile, source);

        Vector2 finalDirection = ResolveDirection(direction);
        float finalForce = CalculateFinalForce(profile, extraForceScale);

        source.GenerateImpulse(new Vector3(finalDirection.x, finalDirection.y, 0f) * finalForce);
    }

    /// <summary>
    /// 방향 없이 재생.
    /// fallback 방향 사용(Vector2.down)
    /// </summary>
    /// <param name="profile">적용할 흔들림 프로필</param>
    /// <param name="source">흔들림을 방출할 Impulse Source. null이면 defaultSource 사용</param>
    /// <param name="extraForceScale">추가 세기 배율</param>
    public void Play(CameraShakeProfile profile, CinemachineImpulseSource source = null, float extraForceScale = 1f)
    {
        Play(profile, fallbackDirection, source, extraForceScale);
    }
    #endregion

    #region Predefined
    /// <summary>
    /// 기본 반동 흔들림을 재생한다.
    /// </summary>
    public void PlayRecoil(Vector2 direction, CinemachineCollisionImpulseSource source = null, float extraForceScale = 1f)
    {
        Play(recoilProfile, direction, source, extraForceScale);
    }

    /// <summary>
    /// 기본 충격 흔들림을 재생한다.
    /// </summary>
    public void PlayBump(Vector2 direction, CinemachineCollisionImpulseSource source = null, float extraForceScale = 1f)
    {
        Play(bumpProfile, direction, source, extraForceScale);
    }

    /// <summary>
    /// 기본 폭발 흔들림을 재생한다.
    /// fallbackDirection 사용.
    /// </summary>
    public void PlayExplosion(CinemachineCollisionImpulseSource source = null, float extraForceScale = 1f)
    {
        Play(explosionProfile, fallbackDirection, source, extraForceScale);
    }

    /// <summary>
    /// 기본 지진 흔들림을 재생한다.
    /// </summary>
    public void PlayRumble(Vector2 direction, CinemachineCollisionImpulseSource source = null, float extraForceScale = 1f)
    {
        Play(rumbleProfile, direction, source, extraForceScale);
    }
    #endregion


    /// <summary>
    /// 프로필의 Source 설정을 Impulse Source에 적용한다.
    /// </summary>
    private void ApplyProfileToSource(CameraShakeProfile profile, CinemachineImpulseSource source)
    {
        CinemachineImpulseDefinition definition = source.ImpulseDefinition;
        definition.ImpulseDuration = profile.Duration;

        if (profile.Mode == CameraShakeProfile.ShapeMode.Predefined)
        {
            definition.ImpulseShape = profile.PredefinedShape;
        }
        else
        {
            definition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Custom;
            definition.CustomImpulseShape = profile.CustomShape;
        }

        // 기본값 세팅. 실제 방향은 호출시 넘긴 벡터 사용.
        Vector2 fallback = ResolveDirection(fallbackDirection);
        source.DefaultVelocity = new Vector3(fallback.x, fallback.y, 0f);
    }

    private float CalculateFinalForce(CameraShakeProfile profile, float extraForceScale)
    {
        return profile.Force * profile.ForceMulti * globalForceMulti * extraForceScale;
    }

    /// <summary>
    /// 흔들림 방향 보정.
    /// normalized된 방향 반환.
    /// </summary>
    private Vector2 ResolveDirection(Vector2 direction)
    {
        Vector2 resolved = direction.sqrMagnitude > 0.0001f ? direction : fallbackDirection;

        if (resolved.sqrMagnitude <= 0.0001f)
            resolved = Vector2.down;

        return resolved.normalized;
    }
}
