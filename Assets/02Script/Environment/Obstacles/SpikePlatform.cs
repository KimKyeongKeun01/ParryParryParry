using UnityEngine;

/// <summary>
/// 스파이크 플랫폼에 부착. 플레이어가 닿으면 즉사 처리.
/// Collider 방식(isTrigger 여부)에 따라 자동 대응.
/// </summary>
public class SpikePlatform : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            Kill();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            Kill();
    }

    private void Kill() => GameManager.Instance.OnGameOver();
}
