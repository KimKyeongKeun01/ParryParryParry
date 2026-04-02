using UnityEngine;
using Unity.Cinemachine;

public class CameraShake : MonoBehaviour
{
    [SerializeField] private float globalShakeForce = 1f;

    private CinemachineImpulseDefinition impulseDefinition;

    public void PlayCameraShake(CinemachineImpulseSource impulseSorce)
    {
        impulseSorce.GenerateImpulseWithForce(globalShakeForce);
    }

    public void ScreenShakeFromProfile(ScreenShakeProfile profile, CinemachineImpulseSource impulseSource, CinemachineImpulseListener listener)
    {
        if (profile == null || impulseSource == null || listener == null)
        {
            Debug.LogWarning("[CameraShake] 중단 - null 참조 존재");
            return;
        }

        SetupScreenShakeSettings(profile, impulseSource, listener);

        impulseSource.GenerateImpulseWithForce(profile.impactForce);
    }

    private void SetupScreenShakeSettings(ScreenShakeProfile profile, CinemachineImpulseSource impulseSource, CinemachineImpulseListener listener)
    {
        impulseDefinition = impulseSource.ImpulseDefinition;

        //impulse source settings
        impulseDefinition.ImpulseDuration = profile.impactTime;
        //impulseSource.DefaultVelocity = profile.defaultVelocity;
        impulseDefinition.CustomImpulseShape = profile.impulseCurve;

        //impulse listener settings
        listener.ReactionSettings.AmplitudeGain = profile.listenerAmplitude;
        listener.ReactionSettings.FrequencyGain = profile.listenerFrequency;
        listener.ReactionSettings.Duration = profile.listenerDuration;
    }
}
