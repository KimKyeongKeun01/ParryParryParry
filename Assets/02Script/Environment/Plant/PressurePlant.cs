using System.Collections;
using UnityEngine;

public class PressurePlant : MonoBehaviour
{
    [Header("Weight Condition")]
    [Tooltip("Mass required to fully press the plant.")]
    [SerializeField] private float requiredMass = 5f;
    [Tooltip("Local Y offset when fully pressed.")]
    [SerializeField] private float pressedOffsetY = -0.3f;
    [Tooltip("Local Y offset while lightly occupied.")]
    [SerializeField] private float lessOffsetY = -0.1f;
    [Tooltip("Lerp speed for the visual movement.")]
    [SerializeField] private float pressedLerpSpeed = 3f;

    [Header("State")]
    [Tooltip("Whether something is currently standing on the plant.")]
    [SerializeField] private bool _isActive;
    [Tooltip("Whether the plant has been fully pressed.")]
    [SerializeField] private bool _isPressed;
    [Tooltip("Current detected mass.")]
    [SerializeField] private float currentMass;
    private Coroutine visualCoroutine;

    [Header("Events")]
    [Tooltip("Object to notify when the plant is fully pressed.")]
    [SerializeField] private GameObject obj;

    private Vector2 defaultLocalPos;
    private ButtonPressEffect pressEffect;

    private void Awake()
    {
        defaultLocalPos = transform.localPosition;
        pressEffect = GetComponent<ButtonPressEffect>();
        _isPressed = false;
    }

    private void OnPressed()
    {
        _isPressed = true;
        PlayVisual();

        if (obj != null)
        {
            Platform_Moving platform = obj.GetComponent<Platform_Moving>();
            if (platform == null)
            {
                Debug.Log("[PressurePlant] platform null");
                return;
            }

            platform.SetExternalSignal(true);
        }

        Debug.Log("[PressurePlant] Player Pressed!");
    }

    private void PlayVisual()
    {
        if (pressEffect != null)
        {
            if (_isPressed)
                pressEffect.SetPressed();
            else if (_isActive)
                pressEffect.SetHover();
            else
                pressEffect.SetIdle();
        }

        if (visualCoroutine != null)
            StopCoroutine(visualCoroutine);

        visualCoroutine = StartCoroutine(Co_UpdateVisual());
    }

    private IEnumerator Co_UpdateVisual()
    {
        Vector2 targetPos = defaultLocalPos;

        if (_isPressed)
            targetPos.y = defaultLocalPos.y + pressedOffsetY;
        else if (_isActive)
            targetPos.y = defaultLocalPos.y + lessOffsetY;

        while (Vector2.Distance(transform.localPosition, targetPos) > 0.001f)
        {
            transform.localPosition = Vector2.Lerp(
                transform.localPosition,
                targetPos,
                pressedLerpSpeed * Time.deltaTime);

            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandlePlayerTrigger(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandlePlayerTrigger(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!TryGetPlayerBody(collision, out _))
            return;

        currentMass = 0f;
        _isActive = false;
        PlayVisual();
        Debug.Log("[PressurePlant] Player Out Board");
    }

    private void HandlePlayerTrigger(Collider2D collision)
    {
        if (!TryGetPlayerBody(collision, out Rigidbody2D playerRb))
            return;

        currentMass = playerRb.mass;

        if (playerRb.mass > requiredMass && !_isPressed)
            OnPressed();

        if (_isActive)
            return;

        _isActive = true;
        PlayVisual();
        Debug.Log("[PressurePlant] Player On Board");
    }

    private bool TryGetPlayerBody(Collider2D collision, out Rigidbody2D playerRb)
    {
        playerRb = null;

        if (collision == null)
            return false;

        playerRb = collision.attachedRigidbody != null
            ? collision.attachedRigidbody
            : collision.GetComponentInParent<Rigidbody2D>();

        if (playerRb == null)
            return false;

        return playerRb.CompareTag("Player");
    }
}
