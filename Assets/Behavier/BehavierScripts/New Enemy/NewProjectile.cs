using UnityEngine;

public class NewProjectile : MonoBehaviour
{
    public float speed = 8f;
    public float damage = 10f;
    public float lifetime = 4f;
    private Vector2 direction;

    public void Init(Vector2 dir, float dmg)
    {
        direction = dir.normalized;
        damage = dmg;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어에게 데미지 (PlayerHealth 컴포넌트가 있다고 가정)
            //other.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Destroy(gameObject);
        }
    }
}