using Unity.Cinemachine;
using UnityEngine;

[CreateAssetMenu(menuName = "Camera/Camera Shake Profile")]
public class CameraShakeProfile : ScriptableObject
{
    public enum ShapeMode { Predefined, Custom }

    [Header("Debug")]
    [SerializeField] private string profileName = "New Shake";

    [Header("Shape")]
    [Tooltip("카메라 흔들림 Shape 모드 선택"), SerializeField] private ShapeMode shapeMode = ShapeMode.Predefined;
    [Tooltip("Cinemachine 내장 Shape 사용"), SerializeField] private CinemachineImpulseDefinition.ImpulseShapes predefinedShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil;
    [Tooltip("Cinemachine 커스텀 Shape 사용"), SerializeField] private AnimationCurve customShape = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [Tooltip("카메라 흔들림 지속시간"), SerializeField, Min(0.01f)] private float duration = 0.15f;

    [Header("Force")]
    [Tooltip("카메라 흔들림 세기"), SerializeField, Min(0f)] private float force = 1f;
    [Tooltip("추가 배율 (강도 미세 조정)"), SerializeField, Min(0f)] private float forceMulti = 1f;

    [Header("Optinal Meta")]
    [Tooltip("프로필 설명"), TextArea(2, 4), SerializeField] private string note;

    #region 프로퍼티
    public string ProfileName => profileName;
    public ShapeMode Mode => shapeMode;
    public CinemachineImpulseDefinition.ImpulseShapes PredefinedShape => predefinedShape;
    public AnimationCurve CustomShape => customShape;
    public float Duration => duration;
    public float Force => force;
    public float ForceMulti => forceMulti;
    #endregion
}
