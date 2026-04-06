using System;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("파티클 이펙트")]
    [SerializeField] private ParticleSystem slamGroundEffectPrefab;
    [SerializeField] private ParticleSystem slamEnemyEffectPrefab;
    [SerializeField] private ParticleSystem jumpEffectPrefab;
    [SerializeField] private ParticleSystem slamStartEffectPrefab;
    [SerializeField] private ParticleSystem footstepEffectPrefab;

    [SerializeField] private GuardEffectController guardEffectPrefab;
    [SerializeField] private PerfectGuardEffectController perfectGuardEffectPrefab;

    [Header("쉐이더 이펙트")]
    [SerializeField] private SlamAnticipationEffectController slamAnticipationEffectPrefab;
    [SerializeField] private FullscreenShockwaveController fullscreenShockwaveController;

    private readonly Dictionary<Transform, GuardEffectController> activeGuardEffects = new();
    private readonly Dictionary<Transform, SlamAnticipationEffectController> activeSlamAnticipationEffects = new();

    [SerializeField] private float slamStartEffectOffset = 0.5f;
    [SerializeField] private float slamStartEffectAngleOffset = 90f;
    [SerializeField] private Vector2 slamAnticipationOffset = Vector2.zero;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region setup
    private void ApplyParticleColor(ParticleSystem particle, Color color)
    {
        var renderer = particle.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
        {
            Debug.LogWarning("[EffectManager] ParticleSystemRenderer 없음");
            return;
        }

        Material runtimeMat = new Material(renderer.material);

        if (runtimeMat.HasProperty("_BaseColor"))
            runtimeMat.SetColor("_BaseColor", color);
        else if (runtimeMat.HasProperty("_Color"))
            runtimeMat.SetColor("_Color", color);

        renderer.material = runtimeMat;
    }

    private Quaternion GetRotationFromDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Quaternion.identity;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle);
    }
    #endregion

    #region 걷기
    public void PlayFootstepEffect(Vector2 position, int facingDir)
    {
        if (footstepEffectPrefab == null)
        {
            Debug.LogWarning("[EffectManager] footstepEffectPrefab이 없습니다.");
            return;
        }

        Quaternion rotation = facingDir < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        ParticleSystem spawned = Instantiate(footstepEffectPrefab, position, rotation);
        spawned.Play();

        float lifeTime = spawned.main.duration;
        if (!spawned.main.loop)
            lifeTime += spawned.main.startLifetime.constantMax;

        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }
    #endregion

    #region 가드
    public void PlayGuardEffect(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[EffectManager] PlayGuardEffect 실패 - target null");
            return;
        }

        if (guardEffectPrefab == null)
        {
            Debug.LogWarning("[EffectManager] guardEffectPrefab이 없습니다.");
            return;
        }

        if (activeGuardEffects.TryGetValue(target, out GuardEffectController existing) && existing != null)
        {
            existing.Play();
            return;
        }

        GuardEffectController spawned = Instantiate(guardEffectPrefab, target.position, Quaternion.identity, target);
        spawned.transform.localPosition = Vector3.zero;
        spawned.Play();

        activeGuardEffects[target] = spawned;
    }

    public void StopGuardEffect(Transform target)
    {
        if (target == null)
            return;

        if (!activeGuardEffects.TryGetValue(target, out GuardEffectController effect) || effect == null)
            return;

        effect.StopAndDestroy();
        activeGuardEffects.Remove(target);
    }

    public void PlayPerfectGuardEffect(Vector2 position)
    {
        if (perfectGuardEffectPrefab == null)
        {
            Debug.LogWarning("[EffectManager] perfectGuardEffectPrefab이 없습니다.");
            return;
        }

        PerfectGuardEffectController spawned = Instantiate(perfectGuardEffectPrefab, position, Quaternion.identity);
        spawned.Play();

        float lifeTime = spawned.GetLifetime();
        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }
    #endregion

    #region 슬램
    public void PlaySlamStartVisual(Transform owner, Vector2 playerPosition, Vector2 slamDirection)
    {
        PlaySlamAnticipationEffect(owner, playerPosition, slamDirection);
    }

    public void PlaySlamImpactVisual(Transform owner, Vector2 position, Vector2 direction, bool isAttack = false)
    {
        StopSlamAnticipationEffect(owner);
        if(isAttack)
        {
            PlaySlamEffect(slamEnemyEffectPrefab, position, direction);
            PlaySlamWaveEffect(position, direction);
        }
        else
        {
            PlaySlamEffect(slamGroundEffectPrefab, position, direction);
            
        }
        
    }

    ////적이 날 때렸을 때. (사용하지 않음 - 2026.04.06 김우성)
    //public void PlaySlamEnemyEffect(Vector2 position, Vector2 direction)
    //{
    //    PlaySlamEffect(slamEnemyEffectPrefab, position, direction);
    //}

    private void PlaySlamAnticipationEffect(Transform owner, Vector2 playerPosition, Vector2 slamDirection)
    {
        if (owner == null)
        {
            Debug.LogWarning("[EffectManager] PlaySlamAnticipationEffect 실패 - owner null");
            return;
        }

        if (slamAnticipationEffectPrefab == null)
            return;

        StopSlamAnticipationEffect(owner);

        Vector2 spawnPosition = playerPosition + slamAnticipationOffset;
        SlamAnticipationEffectController spawned = Instantiate(slamAnticipationEffectPrefab, spawnPosition, Quaternion.identity);
        activeSlamAnticipationEffects[owner] = spawned;

        spawned.Play(
            spawnPosition,
            slamDirection,
            effect => HandleSlamAnticipationEffectFinished(owner, effect)
        );
    }
    private void PlaySlamWaveEffect(Vector2 position, Vector2 direction)
    {
        fullscreenShockwaveController.PlayAtWorld(position);
    }

    public void StopSlamAnticipationEffect(Transform owner)
    {
        if (owner == null)
            return;

        if (!activeSlamAnticipationEffects.TryGetValue(owner, out SlamAnticipationEffectController effect) || effect == null)
        {
            activeSlamAnticipationEffects.Remove(owner);
            return;
        }

        activeSlamAnticipationEffects.Remove(owner);
        effect.StopAndDestroy();
    }

    private void HandleSlamAnticipationEffectFinished(Transform owner, SlamAnticipationEffectController effect)
    {
        if (owner == null)
            return;

        if (!activeSlamAnticipationEffects.TryGetValue(owner, out SlamAnticipationEffectController current))
            return;

        if (current != effect)
            return;

        activeSlamAnticipationEffects.Remove(owner);
    }
    #endregion

    #region 점프
    public void PlayJumpEffect(Vector2 position)
    {
        Vector3 spawnPosition = position;
        spawnPosition.y -= 0.2f;
        PlayEffect(jumpEffectPrefab, spawnPosition, Color.white);
    }
    #endregion

    #region play effect
    private void PlayEffect(ParticleSystem prefab, Vector2 position, Color color)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[EffectManager] 재생할 ParticleSystem 프리팹이 없습니다.");
            return;
        }

        ParticleSystem spawned = Instantiate(prefab, position, prefab.transform.rotation);

        ApplyParticleColor(spawned, color);
        spawned.Play();

        float lifeTime = spawned.main.duration;
        if (!spawned.main.loop)
            lifeTime += spawned.main.startLifetime.constantMax;

        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }

    private void PlaySlamEffect(ParticleSystem prefab, Vector2 position, Vector2 direction, Color? color = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[EffectManager] 재생할 ParticleSystem 프리팹이 없습니다.");
            return;
        }

        Quaternion rotation = GetRotationFromDirection(direction);
        ParticleSystem spawned = Instantiate(prefab, position, rotation);

        if (color.HasValue)
            ApplyParticleColor(spawned, color.Value);

        spawned.Play();

        float lifeTime = spawned.main.duration;
        if (!spawned.main.loop)
            lifeTime += spawned.main.startLifetime.constantMax;

        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }
    #endregion
}