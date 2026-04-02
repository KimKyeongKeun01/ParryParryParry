using DG.Tweening;
using UnityEngine;

public class TransformTelegraphEffect : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform visualTarget;

    [Header("Scale")]
    [SerializeField] private bool useScaleEffect = true;
    [SerializeField] private Vector3 anticipationScale = new Vector3(0.96f, 1.05f, 1f);
    [SerializeField] private float anticipationDuration = 0.08f;
    [SerializeField] private Vector3 releaseScale = Vector3.one;
    [SerializeField] private float releaseDuration = 0.08f;
    [SerializeField] private Ease anticipationEase = Ease.OutQuad;
    [SerializeField] private Ease releaseEase = Ease.OutBack;

    [Header("Shake")]
    [SerializeField] private bool useShakeEffect = true;
    [SerializeField] private float shakeStrength = 0.04f;
    [SerializeField] private int shakeVibrato = 10;
    [SerializeField] private float shakeRandomness = 30f;
    [SerializeField] private bool shakeSnapping = false;

    private Sequence sequence;
    private Vector3 defaultLocalPosition;
    private Vector3 defaultLocalScale;

    public float TotalDuration => Mathf.Max(0f, anticipationDuration) + Mathf.Max(0f, releaseDuration);

    private void Reset()
    {
        if (visualTarget == null)
            visualTarget = transform;
    }

    private void Awake()
    {
        if (visualTarget == null)
            visualTarget = transform;

        defaultLocalPosition = visualTarget.localPosition;
        defaultLocalScale = visualTarget.localScale;
    }

    private void OnDisable()
    {
        Stop();
        ApplyDefaultPose();
    }

    private void OnValidate()
    {
        anticipationDuration = Mathf.Max(0f, anticipationDuration);
        releaseDuration = Mathf.Max(0f, releaseDuration);
        shakeStrength = Mathf.Max(0f, shakeStrength);
        shakeVibrato = Mathf.Max(1, shakeVibrato);
        shakeRandomness = Mathf.Max(0f, shakeRandomness);
    }

    public void Play()
    {
        if (visualTarget == null)
            return;

        Stop();
        ApplyDefaultPose();

        Vector3 anticipationTargetScale = Vector3.Scale(defaultLocalScale, anticipationScale);
        Vector3 releaseTargetScale = Vector3.Scale(defaultLocalScale, releaseScale);

        sequence = DOTween.Sequence()
            .SetUpdate(UpdateType.Normal)
            .SetTarget(this);

        if (useScaleEffect && anticipationDuration > 0f)
        {
            sequence.Append(visualTarget
                .DOScale(anticipationTargetScale, anticipationDuration)
                .SetEase(anticipationEase));
        }
        else if (anticipationDuration > 0f)
        {
            sequence.AppendInterval(anticipationDuration);
        }

        if (useShakeEffect && TotalDuration > 0f)
        {
            sequence.Join(visualTarget.DOShakePosition(
                TotalDuration,
                new Vector3(shakeStrength, shakeStrength, 0f),
                shakeVibrato,
                shakeRandomness,
                shakeSnapping,
                fadeOut: true));
        }

        if (useScaleEffect && releaseDuration > 0f)
        {
            sequence.Append(visualTarget
                .DOScale(releaseTargetScale, releaseDuration)
                .SetEase(releaseEase));
        }
        else if (releaseDuration > 0f)
        {
            sequence.AppendInterval(releaseDuration);
        }

        sequence.OnComplete(ApplyDefaultPose);
    }

    public void Stop()
    {
        DOTween.Kill(this);
        sequence?.Kill();
        sequence = null;
    }

    private void ApplyDefaultPose()
    {
        if (visualTarget == null)
            return;

        visualTarget.localPosition = defaultLocalPosition;
        visualTarget.localScale = defaultLocalScale;
    }
}
