using DG.Tweening;
using UnityEngine;

public class ButtonPressEffect : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform visualTarget;

    [Header("Press Depth")]
    [SerializeField] private float hoverOffsetY = -0.08f;
    [SerializeField] private float pressedOffsetY = -0.22f;

    [Header("Scale")]
    [SerializeField] private Vector3 hoverScale = new Vector3(1.02f, 0.96f, 1f);
    [SerializeField] private Vector3 pressedScale = new Vector3(1.05f, 0.9f, 1f);

    [Header("Timing")]
    [SerializeField] private float pressDuration = 0.12f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Ease pressEase = Ease.OutQuad;
    [SerializeField] private Ease releaseEase = Ease.OutBack;

    [Header("Feedback")]
    [SerializeField] private bool punchOnFullPress = true;
    [SerializeField] private Vector3 punchScale = new Vector3(0.03f, -0.04f, 0f);
    [SerializeField] private float punchDuration = 0.12f;

    private Tween moveTween;
    private Tween scaleTween;
    private Tween punchTween;
    private Vector3 defaultLocalPosition;
    private Vector3 defaultLocalScale;
    private VisualState currentState = VisualState.Idle;

    private enum VisualState
    {
        Idle,
        Hover,
        Pressed
    }

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
        moveTween?.Kill();
        scaleTween?.Kill();
        punchTween?.Kill();
        moveTween = null;
        scaleTween = null;
        punchTween = null;
    }

    private void OnValidate()
    {
        pressDuration = Mathf.Max(0f, pressDuration);
        releaseDuration = Mathf.Max(0f, releaseDuration);
        punchDuration = Mathf.Max(0f, punchDuration);
    }

    public void SetIdle(bool instant = false)
    {
        TransitionTo(VisualState.Idle, instant);
    }

    public void SetHover(bool instant = false)
    {
        TransitionTo(VisualState.Hover, instant);
    }

    public void SetPressed(bool instant = false)
    {
        bool shouldPunch = currentState != VisualState.Pressed;
        TransitionTo(VisualState.Pressed, instant);

        if (!instant && shouldPunch && punchOnFullPress)
        {
            punchTween?.Kill();
            punchTween = visualTarget
                .DOPunchScale(punchScale, punchDuration, 1, 0f)
                .SetUpdate(UpdateType.Normal)
                .SetTarget(this);
        }
    }

    private void TransitionTo(VisualState nextState, bool instant)
    {
        if (visualTarget == null)
            return;

        currentState = nextState;

        Vector3 targetPosition = defaultLocalPosition;
        Vector3 targetScale = defaultLocalScale;
        float duration = nextState == VisualState.Idle ? releaseDuration : pressDuration;
        Ease ease = nextState == VisualState.Idle ? releaseEase : pressEase;

        if (nextState == VisualState.Hover)
        {
            targetPosition.y += hoverOffsetY;
            targetScale = Vector3.Scale(defaultLocalScale, hoverScale);
        }
        else if (nextState == VisualState.Pressed)
        {
            targetPosition.y += pressedOffsetY;
            targetScale = Vector3.Scale(defaultLocalScale, pressedScale);
        }

        moveTween?.Kill();
        scaleTween?.Kill();

        if (instant || duration <= 0f)
        {
            visualTarget.localPosition = targetPosition;
            visualTarget.localScale = targetScale;
            return;
        }

        moveTween = visualTarget
            .DOLocalMove(targetPosition, duration)
            .SetEase(ease)
            .SetTarget(this);

        scaleTween = visualTarget
            .DOScale(targetScale, duration)
            .SetEase(ease)
            .SetTarget(this);
    }
}
