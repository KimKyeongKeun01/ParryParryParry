using UnityEngine;

public class BossStart : MonoBehaviour
{
    [SerializeField] private BossStage bossStage;
    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        if (other.CompareTag("Player") && !other.isTrigger)
        {
            isTriggered = true;
            Debug.Log("[BossStart] Player reached the trigger point!");

            // 보스 스테이지에게 시작 신호를 보냄
            if (bossStage != null)
            {
                bossStage.StartBoss();
            }

            // 오브젝트 비활성화
            gameObject.SetActive(false);
        }
    }
}
