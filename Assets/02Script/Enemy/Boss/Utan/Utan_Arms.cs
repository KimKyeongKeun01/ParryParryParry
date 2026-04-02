using UnityEngine;

public class Utan_Arms : MonoBehaviour
{
    public Boss_Utan owner;

    [SerializeField] private Collider2D[] _armColliders;
    [SerializeField] private Collider2D[] _handColliders;

    private void Awake()
    {
        owner = GetComponentInParent<Boss_Utan>();

        foreach (var arm in _armColliders) arm.enabled = false;
        foreach (var hand in _handColliders) hand.enabled = false;
    }

    public void SetAttack(bool active)
    {
        foreach (var arm in _armColliders) arm.enabled = active;
        foreach (var hand in _handColliders) hand.enabled = active;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            owner.OnArmHit();
            // 한 번 휘두를 때 여러 번 맞지 않도록 끄기 (선택 사항)
            SetAttack(false);
        }
    }
}
