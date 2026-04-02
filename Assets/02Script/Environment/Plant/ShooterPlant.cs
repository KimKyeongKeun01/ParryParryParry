using System.Collections;
using UnityEngine;

public class ShooterPlant : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Projectile prefab to spawn.")]
    public GameObject proPrefab;
    [Tooltip("Spawn point for the projectile.")]
    public Transform firePoint;

    [Header("Fire Setting")]
    [Tooltip("Projectile recoil power on guard hit.")]
    [SerializeField] private float recoilPower;
    [Tooltip("가드 시 넉백 배율 (0 = 완전 차단, 1 = 원본, -1 = PlayerStatus 전역값 사용)"), Range(-1f, 1f)]
    [SerializeField] private float recoilPower_PMulti = -1f;
    [Tooltip("Projectile speed.")]
    [SerializeField] private float proSpeed = 20f;
    [Tooltip("Time between shots.")]
    [SerializeField] private float fireRate = 2f;
    [Tooltip("Detected fire direction.")]
    private Vector2 fireDirection = Vector2.right;

    [Header("Telegraph")]
    [SerializeField] private bool useFireTelegraph = true;
    [SerializeField] private TransformTelegraphEffect fireTelegraphEffect;

    private float _timer;
    private bool _isPreparingShot;
    private SpriteRenderer plantRenderer;

    private void Awake()
    {
        plantRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (firePoint != null)
        {
            if (firePoint.position.x > transform.position.x) fireDirection = Vector2.right;
            else if (firePoint.position.x < transform.position.x) fireDirection = Vector2.left;
        }

        if (fireTelegraphEffect == null)
            fireTelegraphEffect = GetComponent<TransformTelegraphEffect>();
    }

    private void Update()
    {
        Timer();
    }

    private void Timer()
    {
        if (_isPreparingShot)
            return;

        _timer += Time.deltaTime;

        if (_timer < fireRate)
            return;

        _timer = 0f;

        if (useFireTelegraph && fireTelegraphEffect != null && fireTelegraphEffect.TotalDuration > 0f)
            StartCoroutine(Co_PrepareShot());
        else
            Shoot();
    }

    private IEnumerator Co_PrepareShot()
    {
        _isPreparingShot = true;
        fireTelegraphEffect.Play();
        yield return new WaitForSeconds(fireTelegraphEffect.TotalDuration);
        Shoot();
        _isPreparingShot = false;
    }

    private void Shoot()
    {
        if (proPrefab == null || firePoint == null)
            return;

        GameObject obj = Instantiate(proPrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = obj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Init(fireDirection, proSpeed, recoilPower, recoilPower_PMulti);

            if (plantRenderer != null)
                projectile.SetBodyColor(plantRenderer.color);
        }
    }
}
