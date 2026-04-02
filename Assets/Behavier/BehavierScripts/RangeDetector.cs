using UnityEngine;

public class RangeDetector : MonoBehaviour
{
    public float detectionRadius = 7f;
    public LayerMask detectionMask;
    private bool showDebugVisuals = true;

    public GameObject DetectedTarget 
    { 
        get; 
        set; 
    }

    public GameObject UpdateDetector() 
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius,detectionMask);

        if(colliders.Length > 0) 
        {
            DetectedTarget = colliders[0].gameObject;
            return DetectedTarget;
        }
        else 
        {
            DetectedTarget = null;
            return null;
        }
    }
}
