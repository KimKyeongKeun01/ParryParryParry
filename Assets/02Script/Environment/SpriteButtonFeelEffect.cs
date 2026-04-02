using DG.Tweening;
using UnityEngine;

public class SpriteButtonFeelEffect : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform visualTarget;
    [SerializeField] private bool playOnEnable = true;

    [Header("Button Feel")]
    [SerializeField] private float pressOffsetY = -0.08f;
    [SerializeField] private Vector3 pressedScale = new Vector3(1.04f, 0.94f, 1f);
    [SerializeField] private float pressDuration = 0.12f;
    [SerializeField] private float releaseDuration = 0.16f;
    [SerializeField] private Ease pressEase = Ease.OutQuad;
    [SerializeField] private Ease releaseEase = Ease.OutBack;

    [Header("Loop")]
    [SerializeField] private bool loop = true;
    [SerializeField] private float restartDelay = 0.5f;
    [SerializeField] private float startDelay = 0f;

    private Sequence sequence;
    private Tween startDelayTween;
    private Vector3 defaultLocalPosition;
    private Vector3 defaultLocalScale;

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

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
        else
            ApplyDefaultPose();
    }

    private void OnDisable()
    {
        Stop();
        ApplyDefaultPose();
    }

    private void OnValidate()
    {
        pressDuration = Mathf.Max(0f, pressDuration);
        releaseDuration = Mathf.Max(0f, releaseDuration);
        restartDelay = Mathf.Max(0f, restartDelay);
        startDelay = Mathf.Max(0f, startDelay);
    }

    public void Play()
    {
        if (visualTarget == null)
            return;

        Stop();
        ApplyDefaultPose();

        if (startDelay > 0f)
        {
            startDelayTween = DOVirtual.DelayedCall(startDelay, CreateLoopSequence)
                .SetUpdate(UpdateType.Normal)
                .SetTarget(this);
        }
        else
            CreateLoopSequence();
    }

    private void CreateLoopSequence()
    {
        Vector3 pressedPosition = defaultLocalPosition + new Vector3(0f, pressOffsetY, 0f);
        Vector3 targetScale = Vector3.Scale(defaultLocalScale, pressedScale);

        sequence = DOTween.Sequence()
            .Append(visualTarget.DOLocalMove(pressedPosition, pressDuration).SetEase(pressEase))
            .Join(visualTarget.DOScale(targetScale, pressDuration).SetEase(pressEase))
            .Append(visualTarget.DOLocalMove(defaultLocalPosition, releaseDuration).SetEase(releaseEase))
            .Join(visualTarget.DOScale(defaultLocalScale, releaseDuration).SetEase(releaseEase))
            .SetTarget(this);

        if (!loop)
            return;

        if (restartDelay > 0f)
            sequence.AppendInterval(restartDelay);

        sequence.SetLoops(-1, LoopType.Restart);
    }

    public void Stop()
    {
        DOTween.Kill(this);
        startDelayTween?.Kill();
        startDelayTween = null;
        sequence?.Kill();
        sequence = null;
    }

    public void Restart()
    {
        ApplyDefaultPose();
        Play();
    }

    private void ApplyDefaultPose()
    {
        if (visualTarget == null)
            return;

        visualTarget.localPosition = defaultLocalPosition;
        visualTarget.localScale = defaultLocalScale;
    }
}
