using System.Collections;
using UnityEngine;

/// <summary>
/// 충돌한 오브젝트의 속도를 접촉 법선 기준으로 반사시키는 슬라임 블록.
/// 충돌 대상이 IBounceable을 구현해야 반사가 적용된다.
/// 대시 중 충돌 시 x 방향 반전 전용 처리.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BounceBlock : MonoBehaviour
{
    [Header("탄성")]
    [Tooltip("탄성 배율.")]
    [SerializeField, Range(0f, 3f)] private float bounceMultiplier = 1f;

    [Tooltip("최소 탄성.")]
    [SerializeField] private float minBounceSpeed = 5f;

    [Tooltip("최대 튕김 속도 (0이면 무제한).")]
    [SerializeField] private float maxBounceSpeed = 0f;

    [Header("대시 바운스")]
    [Tooltip("대시 바운스 x 속도 배율 (1보다 크면 과장된 튕김)")]
    [SerializeField] private float dashBounceXMultiplier = 1.5f;
    [Tooltip("대시 바운스 시 추가되는 위쪽 속도")]
    [SerializeField] private float dashBounceYBoost = 5f;
    [Tooltip("대시 바운스 감속 (초당)")]
    [SerializeField] private float dashBounceDeceleration = 30f;
    [Tooltip("대시 바운스 x 밀치는 시간 (초)")]
    [SerializeField] private float dashBounceDuration = 0.3f;

    private Coroutine _dashBounceCoroutine;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        IBounceable bounceable = collision.collider.GetComponentInParent<IBounceable>();
        if (bounceable == null) return;
        if (collision.rigidbody == null) return;

        // 대시 중 충돌: x 반전 후 WaitForFixedUpdate로 PlayerMovement 덮어쓰기 방지
        Player player = collision.collider.GetComponentInParent<Player>();
        if (player != null && player.isDash)
        {
            float xSpeed = Mathf.Abs(collision.relativeVelocity.x);
            xSpeed = Mathf.Max(xSpeed, minBounceSpeed) * bounceMultiplier * dashBounceXMultiplier;
            if (maxBounceSpeed > 0f) xSpeed = Mathf.Min(xSpeed, maxBounceSpeed);
            float xDir = collision.relativeVelocity.x != 0f
                ? -Mathf.Sign(collision.relativeVelocity.x)
                : -Mathf.Sign(collision.rigidbody.linearVelocity.x);

            bounceable.OnBounce(new Vector2(xDir * xSpeed, 0f));

            if (_dashBounceCoroutine != null) StopCoroutine(_dashBounceCoroutine);
            _dashBounceCoroutine = StartCoroutine(PreserveXVelocity(collision.rigidbody, xDir * xSpeed, dashBounceYBoost));
            return;
        }

        // 플레이어가 블록 위면으로 내려오는 방향이 아니면(옆·아래 충돌) 무시
        if (Vector2.Dot(collision.relativeVelocity, -transform.up) < 0.1f) return;

        // transform.up : 블록 자체의 법선 (contacts[0].normal은 수치 오차로 축 정렬이 안 됨)
        Vector2 normal = transform.up;
        // relativeVelocity = 충돌 시점의 상대속도 (물리 해결 전 값)
        // rb.linearVelocity는 물리 해결 후라 이미 0이 됨
        Vector2 inVelocity = collision.relativeVelocity;

        // 유리창 반사: reflect = v - 2 * dot(v, n) * n
        Vector2 reflected = Vector2.Reflect(inVelocity, normal);

        // 최소 속도 보장
        if (reflected.magnitude < minBounceSpeed)
            reflected = reflected.normalized * minBounceSpeed;

        reflected *= bounceMultiplier;

        if (maxBounceSpeed > 0f && reflected.magnitude > maxBounceSpeed)
            reflected = reflected.normalized * maxBounceSpeed;

        bounceable.OnBounce(reflected);
    }

    // FixedUpdate 이후에 실행(WaitForFixedUpdate)되어 PlayerMovement의 x 덮어쓰기를 방지
    private IEnumerator PreserveXVelocity(Rigidbody2D rb, float xVelocity, float yBoost = 0f)
    {
        float elapsed = 0f;

        while (rb != null && elapsed < dashBounceDuration)
        {
            yield return new WaitForFixedUpdate();
            if (rb == null) yield break;

            elapsed += Time.fixedDeltaTime;

            var v = rb.linearVelocity;
            // 첫 프레임: 물리 스텝 이후에 y 적용 (바닥 contact가 y를 0으로 만드는 것을 회피)
            if (elapsed <= Time.fixedDeltaTime && yBoost != 0f)
                v.y = yBoost;
            if (Mathf.Abs(xVelocity) > Mathf.Abs(v.x))
                v.x = xVelocity;
            rb.linearVelocity = v;
            xVelocity = Mathf.MoveTowards(xVelocity, 0f, dashBounceDeceleration * Time.fixedDeltaTime);
        }
        _dashBounceCoroutine = null;
    }
}
