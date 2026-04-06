using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class BaseProjectile : MonoBehaviour
{
    [Header("Basic Settings")]
    [Tooltip("투사체 발사 속도"), SerializeField] protected float speed = 10f;
    [Tooltip("투사체 생존 최대 시간"), SerializeField] protected float lifeTime = 10f;
    [Tooltip("투사체 데미지"), SerializeField] protected int damage = 1;
    [Tooltip("투사체 넉백 파워"), SerializeField] protected float knockbackPower = 10f;
    [Tooltip("투사체 넉백 방향"), SerializeField] protected Vector2 knockbackDirection;
    [Tooltip("장애물(벽/바닥) 피격시 파괴 여부"), SerializeField] protected bool destroyOnGround = true;

    [Header("Gravity Settings")]
    [Tooltip("중력 물리 사용 여부"), SerializeField] protected bool useGravity = false;
    [Tooltip("중력 계수"), SerializeField] protected float gravity = 4f;

    [Header("Reflect Settings")]
    [Tooltip("투사체 반사 가능 여부"), SerializeField] protected bool canReflect = true;
    [Tooltip("투사체 반사 여부"), SerializeField] protected bool isReflect = false;
    [Tooltip("투사체 반사시 속도 배율"), SerializeField] protected float reflectSpeedMulti = 1f;

    protected Vector2 direction;
    protected Rigidbody2D _rigid;
    protected SpriteRenderer _sprite;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _rigid = GetComponent<Rigidbody2D>();
        if (useGravity) _rigid.bodyType = RigidbodyType2D.Dynamic;
        else _rigid.bodyType = RigidbodyType2D.Kinematic;

        GetComponent<Collider2D>().isTrigger = true;
    }

    /// <summary> 투사체 상태 초기화 </summary>
    public virtual void Init(Vector2 dir, int dmg, float spd = -1, float pwr = -1)
    {
        damage = dmg;
        direction = dir.normalized;
        if (spd > 0) speed = spd;
        if (pwr > 0) knockbackPower = pwr;
        
        // 방향에 맞게 투사체 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Destroy(gameObject, lifeTime);
    }

    protected virtual void FixedUpdate()
    {
        // 중력 사용시
        if (useGravity) return;

        // 중력 미사용시
        _rigid.MovePosition(_rigid.position + direction * speed * Time.fixedDeltaTime);
    }

    /// <summary> 플레이어에 의한 반사 </summary>
    protected virtual void Reflect(Vector2? dir = null, bool gravity = false)
    {
        // 상태 갱신
        isReflect = true;
        Color color;
        // #FFFFFF 형식의 Hex 코드를 컬러로 변환
        UnityEngine.ColorUtility.TryParseHtmlString("#1B011D", out color);
        if (_sprite != null) _sprite.color = color;

        // TODO: 자식 클래스에서 발사체에 따라 반사 로직 구성
    }

    public void SetGravity(bool gravity, float scale = 0)
    {
        useGravity = gravity;

        if (gravity)
        {
            _rigid.bodyType = RigidbodyType2D.Dynamic;
            _rigid.gravityScale = scale;
            _rigid.linearVelocity = direction * speed;
        }
        else _rigid.bodyType = RigidbodyType2D.Kinematic;
    }

    public void SetGround(bool ground)
    {
        destroyOnGround = ground;
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 플레이어 충돌
        if (collision.CompareTag("Player") && !isReflect)
        {
            Player player = Player.Instance;
            if (player == null) return;

            // 플레이어의 가드 상태 확인
            Player.GuardType guard = player.controller.OnKnockback(direction, knockbackPower);

            if (canReflect && guard == Player.GuardType.PerfectGuard)
            {
                Reflect();
            }
            else if (guard == Player.GuardType.Guard)
            {
                // 가드 성공 시 데미지 없음, 발사체 소멸
                Destroy(gameObject);
            }
            else // Normal Hit
            {
                player.TakeDamaged(damage);
                Destroy(gameObject);
            }
        }

        // 2. 적 충돌
        else if (isReflect && collision.CompareTag("Enemy"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
                Destroy(gameObject);
            }
        }

        // 3. 지형 충돌
        else if (destroyOnGround && collision.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
