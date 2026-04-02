using UnityEngine;

public class Projectile_Utan : BaseProjectile
{
    public override void Init(Vector2 dir, int dmg, float spd = -1, float pwr = -1)
    {
        base.Init(dir, dmg, spd, pwr);
    }

    protected override void Reflect(Vector2? dir = null, bool gravity = false)
    {
        base.Reflect(dir, gravity);

        // 상태 갱신
        SetGravity(false);
        _rigid.linearVelocity = Vector2.zero;

        // 반사 방향 계산
        direction = new Vector2(-direction.x, 0f).normalized;
        speed *= reflectSpeedMulti;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);        
    }
}
