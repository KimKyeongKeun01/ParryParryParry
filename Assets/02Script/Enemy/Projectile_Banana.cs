using UnityEngine;

public class Projectile_Banana : BaseProjectile
{
    public override void Init(Vector2 dir, int dmg, float spd = -1, float pwr = -1)
    {
        base.Init(dir, dmg, spd, pwr);
    }

    protected override void Reflect(Vector2? dir = null, bool gravity = false)
    {
        base.Reflect(dir, gravity);

        // 반사 시 중력 제거 및 직선 비행
        SetGravity(false);
        _rigid.linearVelocity = Vector2.zero;

        // 플레이어가 친 방향으로 반전 (수평 반전)
        direction = new Vector2(-direction.x, 0f).normalized;
        speed *= reflectSpeedMulti;

        // 회전 업데이트
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        // 반사된 상태에서 적(원숭이)과 충돌했을 때
        if (isReflect && collision.CompareTag("Enemy"))
        {
            Enemy_Monkey monkey = collision.GetComponentInParent<Enemy_Monkey>();
            if (monkey != null)
            {
                monkey.OnReflectHit(damage); // 원숭이 전용 반사 피격 호출
                Destroy(gameObject);
                return;
            }
        }

        // 그 외 기본 충돌 로직 (플레이어 피격 등) 수행
        base.OnTriggerEnter2D(collision);
    }
}
