using TMPro;
using UnityEngine;

public class TempVelocity : MonoBehaviour
{
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private TMP_Text velocityText;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                targetRigidbody = player.GetComponent<Rigidbody2D>();
            }
        }
    }

    private void Update()
    {
        if (velocityText != null)
        {
            velocityText.text = GetVelocityText();
        }
    }

    public string GetVelocityText()
    {
        if (targetRigidbody == null)
        {
            return "X Velocity : 0.0\nY Velocity : 0.0";
        }

        Vector2 velocity = targetRigidbody.linearVelocity;
        return $"X Velocity : {velocity.x:0.0}\nY Velocity : {velocity.y:0.0}";
    }
}
