using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Tooltip("Projectile movement direction")] private Vector2 dir;
    [Tooltip("Projectile speed")] private float _speed;
    [Tooltip("Normal guard recoil power")] private float _recoilPower;
    [Tooltip("Guard knockback multiplier (0 = 완전 차단, 1 = 원본)")] private float _guardKnockbackMultiplier;
    private float _lifeTime = 5f;
    private SpriteRenderer bodyRenderer;

    private void Awake()
    {
        bodyRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(Vector2 dir, float speed, float recoilPower, float guardKnockbackMultiplier = -1f)
    {
        this.dir = dir;
        this._speed = speed;
        this._recoilPower = recoilPower;
        this._guardKnockbackMultiplier = guardKnockbackMultiplier;

        Destroy(gameObject, _lifeTime);
    }

    public void SetBodyColor(Color color)
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();

        if (bodyRenderer != null)
            bodyRenderer.color = color;
    }

    private void Update()
    {
        transform.Translate(dir * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = Player.Instance;
            if (player == null || player.controller == null)
                return;

            Player.GuardType guardType = player.controller.OnKnockback(dir, _recoilPower, _guardKnockbackMultiplier);
            /*
            if (guardType == Player.GuardType.Normal)
            {
                Debug.Log("[Projectile] Player Take Damaged");
                player.TakeDamaged(1);
            }
            */
            Destroy(gameObject);
        }

        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
