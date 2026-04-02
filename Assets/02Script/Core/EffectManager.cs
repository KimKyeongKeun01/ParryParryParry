using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [SerializeField] private ParticleSystem slamGroundEffectPrefab;
    [SerializeField] private ParticleSystem slamEnemyEffectPrefab;
    [SerializeField] private ParticleSystem jumpEffectPrefab;
    [SerializeField] private ParticleSystem slamStartEffectPrefab;
    [SerializeField] private ParticleSystem footstepEffectPrefab;

    [SerializeField] private GuardEffectController guardEffectPrefab;
    [SerializeField] private PerfectGuardEffectController perfectGuardEffectPrefab;

    private readonly Dictionary<Transform, GuardEffectController> activeGuardEffects = new();

    [SerializeField] private float slamStartEffectOffset = 0.5f;
    [SerializeField] private float slamStartEffectAngleOffset = 90f;

    private void Awake()
    {
        if(Instance != null && Instance != this)
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

        // 🔥 Material 복사 (중요)
        Material runtimeMat = new Material(renderer.material);

        // Shader에 따라 다름 (보통 이 둘 중 하나)
        if (runtimeMat.HasProperty("_BaseColor"))
            runtimeMat.SetColor("_BaseColor", color);
        else if (runtimeMat.HasProperty("_Color"))
            runtimeMat.SetColor("_Color", color);

        renderer.material = runtimeMat;
    }

    private Quaternion GetRotationFromDirection(Vector2 direction)
    {
        if(direction == Vector2.zero) return Quaternion.identity;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle);
    }
    #endregion

    #region 걷기
    public void PlayFootstepEffect(Vector2 position, int facingDir)
    {
        if(footstepEffectPrefab == null)
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
        if(target == null)
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
        if (target == null) return;

        if(!activeGuardEffects.TryGetValue(target, out GuardEffectController effect) || effect == null)
        {
            return;
        }

        effect.StopAndDestroy();
        activeGuardEffects.Remove(target);
    }

    public void PlayPerfectGuardEffect(Vector2 position)
    {
        if(perfectGuardEffectPrefab == null)
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
    public void PlaySlamGroundEffect(Vector2 position, Vector2 direction, Color color)
    {
        PlaySlamEffect(slamGroundEffectPrefab, position, direction, color);
    }

    public void PlaySlamEnemyEffect(Vector2 position, Vector2 direction)
    {
        PlaySlamEffect(slamEnemyEffectPrefab, position, direction); 
    }

    public void PlaySlamStartEffect(Vector2 playerPosition, Vector2 slamDirection)
    {
        if(slamStartEffectPrefab == null)
        {
            Debug.LogWarning("[EffectManager] slamStartEffectPrefab이 없습니다.");
            return;
        }

        Vector2 reverseDir = -slamDirection.normalized;
        Vector2 spawnPos = playerPosition + reverseDir * slamStartEffectOffset;

        //Quaternion rotation = GetRotationFromDirection(reverseDir);

        float angle = Mathf.Atan2(reverseDir.y, reverseDir.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle + slamStartEffectAngleOffset);

        ParticleSystem spawned = Instantiate(slamStartEffectPrefab, spawnPos, rotation);

        spawned.Play();

        float lifeTime = spawned.main.duration;
        if (!spawned.main.loop)
        {
            lifeTime += spawned.main.startLifetime.constantMax;
        }

        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }
    #endregion

    #region 점프
    public void PlayJumpEffect(Vector2 _position)
    {
        Vector3 position = _position;
        position.y -= 0.2f;
        PlayEffect(jumpEffectPrefab, position, Color.white);
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
        if (spawned.main.loop == false)
        {
            lifeTime += spawned.main.startLifetime.constantMax;
        }

        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }

    private void PlaySlamEffect(ParticleSystem prefab, Vector2 position, Vector2 direction, Color? color = null)
    {
        if(prefab == null)
        {
            Debug.LogWarning("[EffectManager] 재생할 ParticleSystem 프리팹이 없습니다.");
            return;
        }

        Quaternion rotation = GetRotationFromDirection(direction);
        ParticleSystem spawned = Instantiate(prefab, position, rotation);


        if (color.HasValue)
        {
            ApplyParticleColor(spawned, color.Value);
        }
        
        spawned.Play();

        float lifeTime = spawned.main.duration;
        if(spawned.main.loop == false)
        {
            lifeTime += spawned.main.startLifetime.constantMax;
        }

        Destroy(spawned.gameObject, lifeTime + 0.2f);
    }
    #endregion
}