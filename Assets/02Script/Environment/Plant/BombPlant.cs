using System.Collections;
using UnityEngine;

public class BombPlant : MonoBehaviour
{
    [Header("Fruit References")]
    private SpriteRenderer _sprite;
    private Collider2D _coll;
    private Transform bombVisual;
    private Vector3 defaultBombLocalPosition;
    private Vector3 defaultBombScale;

    [Header("Plant Settings")]
    [SerializeField] public bool _isActive = true;
    [SerializeField] private float reSpawnTime = 2f;
    [SerializeField] private float exPower = 50f;
    [SerializeField] private float baseExPower = 30f;
    [SerializeField] private float growthLiftFactor = 0.5f;

    [Header("Explosion")]
    [SerializeField] private GameObject _explosionEffect;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _coll = GetComponent<Collider2D>();
        bombVisual = FindBombVisual();
        CacheBombVisualDefaults();
        ApplyBombScale();
    }

    private void OnValidate()
    {
        if (baseExPower <= 0f)
            baseExPower = 1f;

        if (bombVisual == null)
            bombVisual = FindBombVisual();

        CacheBombVisualDefaults();

        ApplyBombScale();
    }

    private void Explosion()
    {
        _isActive = false;

        if (_coll != null)
            _coll.enabled = false;

        if (_sprite != null)
            _sprite.enabled = false;

        StartCoroutine(Co_Respawn());
    }

    private IEnumerator Co_Respawn()
    {
        yield return new WaitForSeconds(reSpawnTime);

        _isActive = true;

        if (_coll != null)
            _coll.enabled = true;

        if (_sprite != null)
            _sprite.enabled = true;

        ApplyBombScale();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isActive)
            return;

        Player player = collision.GetComponent<Player>();
        if (player == null || !player.isSlam || player.controller == null)
            return;

        Debug.Log("[BombPlant] Bomb!");
        player.controller.OnKnockback(Vector2.up, exPower);
        player.controller.ResetAirSlamUsage();
        Explosion();
    }

    private void ApplyBombScale()
    {
        if (bombVisual == null)
            return;

        float scaleMultiplier = Mathf.Max(0.1f, exPower / Mathf.Max(0.01f, baseExPower));
        bombVisual.localScale = defaultBombScale * scaleMultiplier;
        bombVisual.localPosition = defaultBombLocalPosition + Vector3.up * ((scaleMultiplier - 1f) * growthLiftFactor);
    }

    private Transform FindBombVisual()
    {
        Transform bombChild = transform.Find("Bomb");
        return bombChild != null ? bombChild : transform;
    }

    private void CacheBombVisualDefaults()
    {
        if (bombVisual == null)
            return;

        if (defaultBombScale == Vector3.zero)
            defaultBombScale = bombVisual.localScale;

        if (defaultBombLocalPosition == Vector3.zero)
            defaultBombLocalPosition = bombVisual.localPosition;
    }
}
