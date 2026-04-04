using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;

public class SlamAnticipationEffectController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Playback")]
    [SerializeField] private bool _cloneMaterialOnAwake = true;
    [SerializeField] private bool _flipByDirection = true;
    [SerializeField] private bool _rotateByDirection = false;
    [SerializeField] private float _rotationAngleOffset = 0f;

    [Header("Pull Tween")]
    [SerializeField] private string _pullStrengthReferenceName = "_PullStrength";
    [SerializeField] private float _pullStrengthTarget = 0.03f;
    [SerializeField] private float _pullDuration = 0.12f;
    [SerializeField] private Ease _pullEase = Ease.OutCubic;

    private Material _runtimeMaterial;
    private Vector3 _defaultLocalScale = Vector3.one;
    private Tween _pullTween;
    private Action<SlamAnticipationEffectController> _onFinished;
    private int _pullStrengthPropertyId;

    private void Reset()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        _defaultLocalScale = transform.localScale;
        _pullStrengthPropertyId = Shader.PropertyToID(_pullStrengthReferenceName);

        if (_cloneMaterialOnAwake && _spriteRenderer != null && _spriteRenderer.sharedMaterial != null)
        {
            _runtimeMaterial = new Material(_spriteRenderer.sharedMaterial);
            _spriteRenderer.material = _runtimeMaterial;
        }

        SetPullStrength(0f);
    }
    private void OnDisable()
    {
        KillPullTween();
    }

    public void Play(Vector2 worldPosition, Vector2 slamDirection, Action<SlamAnticipationEffectController> onFinished = null)
    {
        _onFinished = onFinished;

        transform.position = worldPosition;
        ApplyDirection(slamDirection);

        RestartPullTween();
    }

    public void StopAndDestroy()
    {
        KillPullTween();
        NotifyFinishedOnce();
        Destroy(gameObject);
    }


    private void RestartPullTween()
    {
        KillPullTween();

        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null)
        {
            Debug.LogWarning("[SlamAnticipationEffectController] SpriteRenderer material reference is missing.");
            NotifyFinishedOnce();
            Destroy(gameObject);
            return;
        }

        if (!targetMaterial.HasProperty(_pullStrengthPropertyId))
        {
            Debug.LogWarning($"[SlamAnticipationEffectController] Material does not have property : {_pullStrengthReferenceName}");
            NotifyFinishedOnce();
            Destroy(gameObject);
            return;
        }

        SetPullStrength(0f);

        _pullTween = DOTween
            .To(GetPullStrength, SetPullStrength, _pullStrengthTarget, _pullDuration)
            .SetEase(_pullEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _pullTween = null;
                NotifyFinishedOnce();
                Destroy(gameObject);
            });
    }

    private void KillPullTween()
    {
        if (_pullTween == null)
            return;

        _pullTween.Kill();
        _pullTween = null;
    }

    private Material GetTargetMaterial()
    {
        if (_spriteRenderer == null)
            return null;

        return _runtimeMaterial != null ? _runtimeMaterial : _spriteRenderer.material;
    }

    private float GetPullStrength()
    {
        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null || !targetMaterial.HasProperty(_pullStrengthPropertyId))
            return 0f;

        return targetMaterial.GetFloat(_pullStrengthPropertyId);
    }

    private void SetPullStrength(float value)
    {
        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null || !targetMaterial.HasProperty(_pullStrengthPropertyId))
            return;

        targetMaterial.SetFloat(_pullStrengthPropertyId, value);
    }

    private void ApplyDirection(Vector2 slamDirection)
    {
        if (_rotateByDirection && slamDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(slamDirection.y, slamDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + _rotationAngleOffset);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }

        if (_flipByDirection)
        {
            Vector3 scale = _defaultLocalScale;

            if (Mathf.Abs(slamDirection.x) > 0.01f)
                scale.x = Mathf.Abs(_defaultLocalScale.x) * Mathf.Sign(slamDirection.x);

            transform.localScale = scale;
        }
        else
        {
            transform.localScale = _defaultLocalScale;
        }
    }

    private void NotifyFinishedOnce()
    {
        Action<SlamAnticipationEffectController> callback = _onFinished;
        _onFinished = null;
        callback?.Invoke(this);
    }

    private void OnDestroy()
    {
        KillPullTween();

        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);

        NotifyFinishedOnce();
    }
}