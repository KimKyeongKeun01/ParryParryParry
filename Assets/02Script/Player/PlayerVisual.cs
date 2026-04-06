using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    #region References
    public GameObject modelObj;
    public GameObject shieldObj;
    public GameObject thrownShieldObj;
    public SpriteRenderer facingIndicatorRenderer;

    private Player player;
    private PlayerStatus stat;
    private Shield shield;

    public Shield ShieldInstance => shield;
    #endregion

    #region Inspector
    [Header("Hit Blink")]
    [SerializeField] private float blinkInterval = 0.1f;

    [Header("Squash")]
    [SerializeField] private Vector3 jumpSquashScale = new Vector3(0.7f, 1.5f, 1f);
    [SerializeField] private float jumpSquashTime = 0.08f;
    [SerializeField] private Ease jumpSquashEase = Ease.InFlash;
    [Space(5)]
    [SerializeField] private Vector3 fallingStartSquashScale = new Vector3(0.8f, 1.3f, 1f);
    [SerializeField] private float fallingStartSquashTime = 0.5f;
    [SerializeField] private Ease fallingStartSquashEase = Ease.InOutQuad;
    [Space(5)]
    [SerializeField] private Vector3 landingSquashScale = new Vector3(1.5f, 0.8f, 1f);
    [SerializeField] private float landSquashTime = 0.08f;
    [SerializeField] private Ease landingSquashEase = Ease.OutBack;
    [Space(5)]
    [SerializeField] private Vector3 slamSquashScale = new Vector3(2f, 0.2f, 1f);
    [SerializeField] private float slamSquashTime = 0.08f;
    [SerializeField] private Ease slamSquashEase = Ease.OutBack;
    [Space(5)]
    [SerializeField] private Vector3 dashSquashScale = new Vector3(1.35f, 0.72f, 1f);
    [SerializeField] private float dashSquashInTime = 0.05f;
    [SerializeField] private float dashSquashOutTime = 0.06f;
    [SerializeField] private Ease dashSquashInEase = Ease.OutCubic;
    [SerializeField] private Ease dashSquashOutEase = Ease.OutBack;

    [Header("Dash Trail")]
    [SerializeField] private Material dashTrailAfterImageMaterial;
    [SerializeField] private Color dashTrailStartColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color dashTrailEndColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private float dashTrailStartIntensity = 1f;
    [SerializeField] private float dashTrailEndIntensity = 1f;
    [SerializeField] private float dashTrailSpawnInterval = 0.03f;
    [SerializeField] private float dashTrailLifetime = 0.12f;
    [SerializeField] private int dashTrailSortingOrderOffset = -1;

    [Header("Particles")]
    [SerializeField] private ParticleSystemRenderer moveParticleRenderer;
    [SerializeField] private ParticleSystemRenderer jumpParticleRenderer;
    [SerializeField] private ParticleSystemRenderer landingParticleRenderer;

    [Header("Perfect Guard Time Slow")]
    [SerializeField] private TimeManager _timeManager;
    [SerializeField] private float perfectGuardTimeScale = 0.6f;
    [SerializeField] private float perfectGuardTimeSlowDuration = 0.08f;
    #endregion

    [SerializeField] private CameraShakeProfile slamShakeProfile;

    private sealed class DashTrailAfterImageData
    {
        public GameObject trailObject;
        public SpriteRenderer renderer;
        public Material material;
        public float fadeT;
        public float sourceAlpha;
        public float colorT;
    }

    private Coroutine blinkCor;
    private Sequence squashSequence;
    private CapsuleCollider2D capsuleCollider;
    private SpriteRenderer modelSpriteRenderer;
    private Vector3 defaultModelScale = Vector3.one;
    private Vector3 defaultModelLocalPosition = Vector3.zero;
    private Vector3 defaultFacingIndicatorLocalPosition = Vector3.zero;
    private bool isHoldingFallSquash;
    private Coroutine dashTrailCor;
    private Coroutine perfectGuardTimeSlowGateCor;
    private ParticleSystem moveParticles;
    private ParticleSystem jumpParticles;
    private ParticleSystem landingParticles;
    private readonly List<DashTrailAfterImageData> activeDashTrailAfterImages = new List<DashTrailAfterImageData>();

    #region Setup
    private void Reset()
    {
        if (_timeManager == null)
            _timeManager = FindFirstObjectByType<TimeManager>(FindObjectsInactive.Include);
    }

    public void Init(Player _player)
    {
        player = _player;
        stat = _player.status;
        CacheVisualReferences();
        ResolveTimeManager();

        if (thrownShieldObj != null)
        {
            shield = thrownShieldObj?.GetComponent<Shield>();
            shield?.Init(stat, transform);
        }

        BindControllerEvents();
    }

    public void Setup()
    {
        StopPerfectGuardTimeSlowGate();

        if (shieldObj != null)
            shieldObj.SetActive(false);

        KillSquashTween();
        isHoldingFallSquash = false;
        StopDashTrail();

        if (modelObj != null)
        {
            modelObj.transform.localRotation = Quaternion.identity;
            modelObj.transform.localPosition = defaultModelLocalPosition;
            modelObj.transform.localScale = defaultModelScale;
        }

        UpdateFacingIndicator(player != null ? player.FacingDirection : 1);

        SetMoveParticlesActive(false);
        StopParticleBurst(jumpParticleRenderer, jumpParticles);
        StopParticleBurst(landingParticleRenderer, landingParticles);
        EffectManager.Instance?.StopGuardEffect(transform);
        EffectManager.Instance?.StopSlamAnticipationEffect(transform);
    }

    private void ResolveTimeManager()
    {
        if (_timeManager == null)
            _timeManager = FindFirstObjectByType<TimeManager>(FindObjectsInactive.Include);
    }

    private void BindControllerEvents()
    {
        if (player == null || player.controller == null)
        {
            Debug.LogWarning("[PlayerVisual] BindControllerEvents 실패 - player 또는 controller가 null");
            return;
        }

        player.controller.onSlamImpact -= HandleSlamImpactVisual;
        player.controller.onSlamImpact += HandleSlamImpactVisual;

        player.controller.onSlamEnemyImpact -= HandleSlamEnemyImpactVisual;
        player.controller.onSlamEnemyImpact += HandleSlamEnemyImpactVisual;

        player.controller.onJump -= HandleJumpVisual;
        player.controller.onJump += HandleJumpVisual;

        player.controller.onGuardStart -= HandleGuardStartVisual;
        player.controller.onGuardStart += HandleGuardStartVisual;

        player.controller.onGuardEnd -= HandleGuardEndVisual;
        player.controller.onGuardEnd += HandleGuardEndVisual;

        player.controller.onPerfectGuardSuccess -= HandlePerfectGuardSuccessVisual;
        player.controller.onPerfectGuardSuccess += HandlePerfectGuardSuccessVisual;

        player.controller.onSlamStart -= HandleSlamStartVisual;
        player.controller.onSlamStart += HandleSlamStartVisual;

        player.controller.onFootstep -= HandleFootstepVisual;
        player.controller.onFootstep += HandleFootstepVisual;
    }

    private void OnDestroy()
    {
        StopPerfectGuardTimeSlowGate();

        if (player == null || player.controller == null)
            return;

        player.controller.onSlamImpact -= HandleSlamImpactVisual;
        player.controller.onSlamEnemyImpact -= HandleSlamEnemyImpactVisual;
        player.controller.onJump -= HandleJumpVisual;
        player.controller.onGuardStart -= HandleGuardStartVisual;
        player.controller.onGuardEnd -= HandleGuardEndVisual;
        player.controller.onPerfectGuardSuccess -= HandlePerfectGuardSuccessVisual;
        player.controller.onSlamStart -= HandleSlamStartVisual;
        player.controller.onFootstep -= HandleFootstepVisual;

        EffectManager.Instance?.StopGuardEffect(transform);
        EffectManager.Instance?.StopSlamAnticipationEffect(transform);
    }
    #endregion

    #region Core Visual
    public void SetFlip(bool isLeft)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        transform.localScale = scale;
        UpdateFacingIndicator(isLeft ? -1 : 1);
    }

    public void UpdateTilt(float normalizedSpeed)
    {
        if (modelObj == null)
            return;

        float targetAngle = -player.FacingDirection * stat.MaxTiltAngle * normalizedSpeed;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);
        modelObj.transform.localRotation = Quaternion.Lerp(
            modelObj.transform.localRotation,
            targetRot,
            stat.TiltSpeed * Time.deltaTime
        );
    }

    public void SetActiveShield(bool activeShield)
    {
        if (shieldObj == null)
            return;

        shieldObj.transform.localPosition = Vector3.zero;
        shieldObj.SetActive(activeShield);
    }
    #endregion

    #region Damage Visual
    public void TakeDamagedVisual()
    {
        if (blinkCor != null)
            StopCoroutine(blinkCor);

        blinkCor = StartCoroutine(BlinkCoroutine());
    }

    public void ReleaseInvincible()
    {
        if (blinkCor != null)
        {
            StopCoroutine(blinkCor);
            blinkCor = null;
        }

        if (modelObj != null)
            modelObj.SetActive(true);
    }
    #endregion

    #region Squash
    public void PlayJumpSquash()
    {
        isHoldingFallSquash = false;
        PlaySquash(jumpSquashScale, jumpSquashTime, jumpSquashEase);
    }

    public void PlayFallingStartSquash()
    {
        HoldSquash(fallingStartSquashScale, fallingStartSquashTime, fallingStartSquashEase);
    }

    public void PlayLandingSquash()
    {
        isHoldingFallSquash = false;
        PlaySquash(landingSquashScale, landSquashTime, landingSquashEase);
    }

    public void PlaySlamSquash()
    {
        isHoldingFallSquash = false;
        PlaySquash(slamSquashScale, slamSquashTime, slamSquashEase);
    }

    public void PlayDashSquash(float dashDuration)
    {
        if (modelObj == null)
            return;

        isHoldingFallSquash = false;
        KillSquashTween();

        Transform modelTransform = modelObj.transform;
        Vector3 targetScale = Vector3.Scale(defaultModelScale, dashSquashScale);
        float stretchTime = Mathf.Clamp(dashSquashInTime, 0f, dashDuration);
        float recoverTime = Mathf.Clamp(dashSquashOutTime, 0f, Mathf.Max(0f, dashDuration - stretchTime));
        float holdTime = Mathf.Max(0f, dashDuration - stretchTime - recoverTime);

        squashSequence = DOTween.Sequence()
            .Append(modelTransform.DOScale(targetScale, stretchTime).SetEase(dashSquashInEase));

        if (holdTime > 0f)
            squashSequence.AppendInterval(holdTime);

        if (recoverTime > 0f)
        {
            squashSequence.Append(modelTransform.DOScale(defaultModelScale, recoverTime).SetEase(dashSquashOutEase));
        }
        else
        {
            squashSequence.AppendCallback(() => modelTransform.localScale = defaultModelScale);
        }

        squashSequence
            .SetTarget(modelTransform)
            .OnUpdate(() =>
            {
                VisualUtil.ApplyCapsuleGroundCorrection(
                    modelTransform,
                    capsuleCollider,
                    defaultModelLocalPosition,
                    defaultModelScale
                );
            })
            .OnComplete(() =>
            {
                modelTransform.localPosition = defaultModelLocalPosition;
            })
            .OnKill(() =>
            {
                if (modelTransform != null)
                    modelTransform.localPosition = defaultModelLocalPosition;
                squashSequence = null;
            });
    }
    #endregion

    #region Walk
    private Vector2 GetFootstepSpawnPosition()
    {
        float xOffset = 0.1f * player.FacingDirection;
        float yOffset = -0.8f;
        return new Vector2(transform.position.x + xOffset, transform.position.y + yOffset);
    }

    private void HandleFootstepVisual(int facingDir)
    {
        Vector2 spawnPos = GetFootstepSpawnPosition();
        EffectManager.Instance?.PlayFootstepEffect(spawnPos, facingDir);
    }
    #endregion

    #region Slam
    private void HandleSlamStartVisual(Vector2 startPos, Vector2 slamDir)
    {
        EffectManager.Instance?.PlaySlamStartVisual(transform, startPos, slamDir);
    }

    private void HandleSlamImpactVisual(Vector2 impactPos, Vector2 impactDir, Color impactColor)
    {
        CameraManager.Instance?.Shake.Play(slamShakeProfile, impactDir);
        EffectManager.Instance?.PlaySlamImpactVisual(transform, impactPos, impactDir, impactColor);
    }

    private void HandleSlamEnemyImpactVisual(Vector2 impactPos, Vector2 impactDir)
    {
        EffectManager.Instance?.PlaySlamEnemyEffect(impactPos, impactDir);
    }
    #endregion

    #region Jump
    private void HandleJumpVisual(Vector2 impactPos)
    {
        EffectManager.Instance?.PlayJumpEffect(impactPos);
    }
    #endregion

    #region Guard
    private void HandleGuardStartVisual()
    {
        EffectManager.Instance?.PlayGuardEffect(transform);
    }

    private void HandleGuardEndVisual()
    {
        EffectManager.Instance?.StopGuardEffect(transform);
    }

    private void HandlePerfectGuardSuccessVisual(Vector2 impactPos, Vector2 shakeDir)
    {
        EffectManager.Instance?.PlayPerfectGuardEffect(impactPos);
        PlayPerfectGuardTimeSlow();
    }
    #endregion

    #region Particles
    public void SetMoveParticlesActive(bool active)
    {
        if (moveParticleRenderer != null)
            moveParticleRenderer.enabled = active;

        if (moveParticles == null)
            return;

        if (active)
        {
            if (!moveParticles.isPlaying)
                moveParticles.Play();
        }
        else if (moveParticles.isPlaying)
        {
            moveParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void PlayJumpParticles()
    {
        PlayParticleBurst(jumpParticleRenderer, jumpParticles);
    }

    public void PlayLandingParticles()
    {
        PlayParticleBurst(landingParticleRenderer, landingParticles);
    }

    public void StartDashTrail()
    {
        if (dashTrailCor != null)
            StopCoroutine(dashTrailCor);

        if (modelObj == null || modelSpriteRenderer == null || dashTrailLifetime <= 0f || dashTrailSpawnInterval <= 0f)
            return;

        dashTrailCor = StartCoroutine(DashTrailCoroutine());
    }

    public void StopDashTrail()
    {
        if (dashTrailCor != null)
        {
            StopCoroutine(dashTrailCor);
            dashTrailCor = null;
        }
    }
    #endregion

    #region Internal Helpers
    private void CacheVisualReferences()
    {
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        if (modelObj != null)
        {
            defaultModelScale = modelObj.transform.localScale;
            defaultModelLocalPosition = modelObj.transform.localPosition;
            modelSpriteRenderer = modelObj.GetComponent<SpriteRenderer>();
        }

        if (facingIndicatorRenderer != null)
            defaultFacingIndicatorLocalPosition = facingIndicatorRenderer.transform.localPosition;

        moveParticles = moveParticleRenderer != null ? moveParticleRenderer.GetComponent<ParticleSystem>() : null;
        jumpParticles = jumpParticleRenderer != null ? jumpParticleRenderer.GetComponent<ParticleSystem>() : null;
        landingParticles = landingParticleRenderer != null ? landingParticleRenderer.GetComponent<ParticleSystem>() : null;

        if (moveParticleRenderer != null)
            moveParticleRenderer.enabled = false;
        if (jumpParticleRenderer != null)
            jumpParticleRenderer.enabled = false;
        if (landingParticleRenderer != null)
            landingParticleRenderer.enabled = false;
    }

    private void PlaySquash(Vector3 squashScale, float squashTime, Ease squashEase)
    {
        if (modelObj == null)
            return;

        KillSquashTween();

        Transform modelTransform = modelObj.transform;
        Vector3 targetScale = Vector3.Scale(defaultModelScale, squashScale);

        squashSequence = DOTween.Sequence()
            .Append(modelTransform.DOScale(targetScale, squashTime).SetEase(squashEase))
            .Append(modelTransform.DOScale(defaultModelScale, squashTime).SetEase(squashEase))
            .SetTarget(modelTransform)
            .OnUpdate(() =>
            {
                VisualUtil.ApplyCapsuleGroundCorrection(
                    modelTransform,
                    capsuleCollider,
                    defaultModelLocalPosition,
                    defaultModelScale
                );
            })
            .OnComplete(() =>
            {
                modelTransform.localPosition = defaultModelLocalPosition;
            })
            .OnKill(() =>
            {
                if (modelTransform != null)
                    modelTransform.localPosition = defaultModelLocalPosition;
                squashSequence = null;
            });
    }

    private void HoldSquash(Vector3 squashScale, float squashDuration, Ease squashEase)
    {
        if (modelObj == null || isHoldingFallSquash)
            return;

        KillSquashTween();
        isHoldingFallSquash = true;

        Transform modelTransform = modelObj.transform;
        Vector3 targetScale = Vector3.Scale(defaultModelScale, squashScale);

        squashSequence = DOTween.Sequence()
            .Append(modelTransform.DOScale(targetScale, squashDuration).SetEase(squashEase))
            .SetTarget(modelTransform)
            .OnUpdate(() =>
            {
                VisualUtil.ApplyCapsuleGroundCorrection(
                    modelTransform,
                    capsuleCollider,
                    defaultModelLocalPosition,
                    defaultModelScale
                );
            })
            .OnKill(() =>
            {
                if (modelTransform != null)
                    modelTransform.localPosition = defaultModelLocalPosition;
                squashSequence = null;
            });
    }

    private void PlayParticleBurst(ParticleSystemRenderer renderer, ParticleSystem particles)
    {
        if (renderer != null)
            renderer.enabled = true;

        if (particles != null)
            particles.Play();
    }

    private void StopParticleBurst(ParticleSystemRenderer renderer, ParticleSystem particles)
    {
        if (renderer != null)
            renderer.enabled = false;

        if (particles != null && particles.isPlaying)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void KillSquashTween()
    {
        if (squashSequence == null)
            return;

        squashSequence.Kill();
        squashSequence = null;
    }

    public void UpdateFacingIndicator(int facingDirection)
    {
        if (facingIndicatorRenderer == null)
            return;

        Vector3 localPosition = defaultFacingIndicatorLocalPosition;
        float xOffset = Mathf.Abs(defaultFacingIndicatorLocalPosition.x);
        if (Mathf.Approximately(xOffset, 0f))
            xOffset = 1f;

        localPosition.x = xOffset * Mathf.Sign(facingDirection == 0 ? 1 : facingDirection);
        facingIndicatorRenderer.transform.localPosition = localPosition;
    }

    private IEnumerator DashTrailCoroutine()
    {
        var wait = new WaitForSeconds(dashTrailSpawnInterval);

        while (true)
        {
            SpawnDashTrailAfterImage();
            yield return wait;
        }
    }

    private void SpawnDashTrailAfterImage()
    {
        if (modelSpriteRenderer == null || modelSpriteRenderer.sprite == null)
            return;

        var trailObject = new GameObject("DashTrailAfterImage");
        var trailTransform = trailObject.transform;
        trailTransform.SetPositionAndRotation(modelObj.transform.position, modelObj.transform.rotation);
        trailTransform.localScale = modelObj.transform.lossyScale;

        var trailRenderer = trailObject.AddComponent<SpriteRenderer>();
        trailRenderer.sprite = modelSpriteRenderer.sprite;
        trailRenderer.sortingLayerID = modelSpriteRenderer.sortingLayerID;
        trailRenderer.sortingOrder = modelSpriteRenderer.sortingOrder + dashTrailSortingOrderOffset;
        trailRenderer.flipX = modelSpriteRenderer.flipX;
        trailRenderer.flipY = modelSpriteRenderer.flipY;
        trailRenderer.color = Color.white;

        Material runtimeMaterial = null;

        if (dashTrailAfterImageMaterial != null)
            runtimeMaterial = new Material(dashTrailAfterImageMaterial);
        else if (modelSpriteRenderer.sharedMaterial != null)
            runtimeMaterial = new Material(modelSpriteRenderer.sharedMaterial);

        if (runtimeMaterial != null)
            trailRenderer.material = runtimeMaterial;

        var afterImageData = new DashTrailAfterImageData
        {
            trailObject = trailObject,
            renderer = trailRenderer,
            material = runtimeMaterial,
            fadeT = 0f,
            sourceAlpha = modelSpriteRenderer.color.a,
            colorT = 0f
        };

        activeDashTrailAfterImages.Add(afterImageData);
        RefreshDashTrailAfterImageColors();

        DOTween.To(
                () => afterImageData.fadeT,
                value =>
                {
                    afterImageData.fadeT = value;
                    ApplyDashTrailAfterImageColor(afterImageData);
                },
                1f,
                dashTrailLifetime
            )
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                activeDashTrailAfterImages.Remove(afterImageData);
                RefreshDashTrailAfterImageColors();
                ReleaseDashTrailAfterImage(afterImageData);
            });
    }
    private void RefreshDashTrailAfterImageColors()
    {
        for (int i = activeDashTrailAfterImages.Count - 1; i >= 0; i--)
        {
            DashTrailAfterImageData afterImageData = activeDashTrailAfterImages[i];
            if (afterImageData == null || afterImageData.renderer == null)
                activeDashTrailAfterImages.RemoveAt(i);
        }

        int count = activeDashTrailAfterImages.Count;
        for (int i = 0; i < count; i++)
        {
            DashTrailAfterImageData afterImageData = activeDashTrailAfterImages[i];
            afterImageData.colorT = count <= 1 ? 0f : (float)i / (count - 1);
            ApplyDashTrailAfterImageColor(afterImageData);
        }
    }

    private void ApplyDashTrailAfterImageColor(DashTrailAfterImageData afterImageData)
    {
        if (afterImageData == null || afterImageData.renderer == null)
            return;

        Color trailColor = Color.Lerp(dashTrailStartColor, dashTrailEndColor, afterImageData.colorT);
        float trailIntensity = Mathf.Lerp(dashTrailStartIntensity, dashTrailEndIntensity, afterImageData.colorT);

        trailColor.a *= afterImageData.sourceAlpha * (1f - afterImageData.fadeT);

        Color hdrTrailColor = new Color(
            trailColor.r * trailIntensity,
            trailColor.g * trailIntensity,
            trailColor.b * trailIntensity,
            trailColor.a
        );

        afterImageData.renderer.color = Color.white;

        if (afterImageData.material != null && afterImageData.material.HasProperty("_NeonColor"))
            afterImageData.material.SetColor("_NeonColor", hdrTrailColor);
    }
    private void ReleaseDashTrailAfterImage(DashTrailAfterImageData afterImageData)
    {
        if (afterImageData == null)
            return;

        if (afterImageData.material != null)
            Destroy(afterImageData.material);

        if (afterImageData.trailObject != null)
            Destroy(afterImageData.trailObject);
    }

    private IEnumerator BlinkCoroutine()
    {
        if (modelObj == null)
            yield break;

        var wait = new WaitForSeconds(blinkInterval);
        while (true)
        {
            modelObj.SetActive(!modelObj.activeSelf);
            yield return wait;
        }
    }

    private void PlayPerfectGuardTimeSlow()
    {
        if (perfectGuardTimeSlowDuration <= 0f)
            return;

        if (perfectGuardTimeScale >= 1f)
            return;

        if (perfectGuardTimeSlowGateCor != null)
            return;

        ResolveTimeManager();
        if (_timeManager == null)
        {
            Debug.LogWarning("[PlayerVisual] TimeManager reference is missing.");
            return;
        }

        _timeManager.SetActionTime(perfectGuardTimeScale, perfectGuardTimeSlowDuration);
        perfectGuardTimeSlowGateCor = StartCoroutine(Co_PerfectGuardTimeSlowGate());
    }

    private IEnumerator Co_PerfectGuardTimeSlowGate()
    {
        yield return new WaitForSecondsRealtime(perfectGuardTimeSlowDuration);
        perfectGuardTimeSlowGateCor = null;
    }

    private void StopPerfectGuardTimeSlowGate()
    {
        if (perfectGuardTimeSlowGateCor != null)
        {
            StopCoroutine(perfectGuardTimeSlowGateCor);
            perfectGuardTimeSlowGateCor = null;
        }
    }

    private void OnDisable()
    {
        isHoldingFallSquash = false;
        StopDashTrail();
        KillSquashTween();
        StopPerfectGuardTimeSlowGate();
        EffectManager.Instance?.StopSlamAnticipationEffect(transform);
    }
    #endregion

    #region Internal Static Helper
    private static class VisualUtil
    {
        public static void ApplyCapsuleGroundCorrection(
            Transform visualTarget,
            CapsuleCollider2D capsuleCollider,
            Vector3 defaultLocalPosition,
            Vector3 defaultLocalScale)
        {
            if (visualTarget == null || capsuleCollider == null)
                return;

            Vector3 correctedLocalPosition = defaultLocalPosition;
            correctedLocalPosition.y += GetCapsuleGroundCorrectionY(capsuleCollider, defaultLocalScale.y, visualTarget.localScale.y);
            visualTarget.localPosition = correctedLocalPosition;
        }

        private static float GetCapsuleGroundCorrectionY(CapsuleCollider2D capsuleCollider, float defaultScaleY, float currentScaleY)
        {
            if (capsuleCollider == null || Mathf.Approximately(defaultScaleY, 0f))
                return 0f;

            float normalizedScaleY = currentScaleY / defaultScaleY;
            float halfHeight = capsuleCollider.size.y * 0.5f;
            return halfHeight * (normalizedScaleY - 1f);
        }
    }
    #endregion
}