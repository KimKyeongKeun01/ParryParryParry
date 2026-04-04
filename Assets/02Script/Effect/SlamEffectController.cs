using System;
using UnityEngine;
using DG.Tweening;

public class SlamEffectController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Playback")]
    [SerializeField] private bool _cloneMaterialOnAwake = true;
    [SerializeField] private bool _flipByDirection = false;
    [SerializeField] private bool _rotateByDirection = false;
    [SerializeField] private float _rotationAngleOffset = 0f;
    [SerializeField] private bool _applySpriteColor = true;

    [Header("Shader Property")]
    [SerializeField] private string _valueReferenceName = "_Value";

    [Header("Wave")]
    [SerializeField] private float _initialValue = 0f;
    [SerializeField] private float _tweenStartValue = 0.15f;
    [SerializeField] private float _targetValue = 1f;
    [SerializeField] private float _valueDuration = 0.22f;
    [SerializeField]
    private AnimationCurve _spreadCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 4.8f, 4.8f),
        new Keyframe(0.18f, 0.58f, 1.2f, 1.2f),
        new Keyframe(1f, 1f, 0.12f, 0f)
    );

    private Material _runtimeMaterial;
    private Vector3 _defaultLocalScale = Vector3.one;
    private Tween _valueTween;
    private Action<SlamEffectController> _onFinished;
    private int _valuePropertyId;

    private void Reset()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        _defaultLocalScale = transform.localScale;
        _valuePropertyId = Shader.PropertyToID(_valueReferenceName);

        if (_cloneMaterialOnAwake && _spriteRenderer != null && _spriteRenderer.sharedMaterial != null)
        {
            _runtimeMaterial = new Material(_spriteRenderer.sharedMaterial);
            _spriteRenderer.material = _runtimeMaterial;
        }

        SetValue(_initialValue);
    }

    public void Play(Vector2 worldPosition, Vector2 slamDirection, Color color, Action<SlamEffectController> onFinished = null)
    {
        _onFinished = onFinished;

        transform.position = worldPosition;
        ApplyDirection(slamDirection);
        ApplyColor(color);
        RestartValueTween();
    }

    public void StopAndDestroy()
    {
        KillValueTween();
        NotifyFinishedOnce();
        Destroy(gameObject);
    }

    private void RestartValueTween()
    {
        KillValueTween();

        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null)
        {
            Debug.LogWarning("[SlamEffectController] SpriteRenderer material reference is missing.");
            NotifyFinishedOnce();
            Destroy(gameObject);
            return;
        }

        if (!targetMaterial.HasProperty(_valuePropertyId))
        {
            Debug.LogWarning($"[SlamEffectController] Material does not have property : {_valueReferenceName}");
            NotifyFinishedOnce();
            Destroy(gameObject);
            return;
        }

        SetValue(_initialValue);

        _valueTween = DOVirtual.Float(_tweenStartValue, _targetValue, _valueDuration, SetValue)
            .SetEase(_spreadCurve)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _valueTween = null;
                NotifyFinishedOnce();
                Destroy(gameObject);
            });
    }

    private void KillValueTween()
    {
        if (_valueTween == null)
            return;

        _valueTween.Kill();
        _valueTween = null;
    }

    private Material GetTargetMaterial()
    {
        if (_spriteRenderer == null)
            return null;

        return _runtimeMaterial != null ? _runtimeMaterial : _spriteRenderer.material;
    }

    private void SetValue(float value)
    {
        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null || !targetMaterial.HasProperty(_valuePropertyId))
            return;

        targetMaterial.SetFloat(_valuePropertyId, value);
    }

    private void ApplyColor(Color color)
    {
        if (_spriteRenderer == null)
            return;

        if (_applySpriteColor)
            _spriteRenderer.color = color;
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
        Action<SlamEffectController> callback = _onFinished;
        _onFinished = null;
        callback?.Invoke(this);
    }

    private void OnDisable()
    {
        KillValueTween();
    }

    private void OnDestroy()
    {
        KillValueTween();

        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);

        NotifyFinishedOnce();
    }
}