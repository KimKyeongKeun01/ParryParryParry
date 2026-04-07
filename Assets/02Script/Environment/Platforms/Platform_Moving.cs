using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPlatformPassenger2D
{
    void OnPlatformVelocityChanged(Platform_Moving platform, Vector2 linearVelocity);
}

[RequireComponent(typeof(Rigidbody2D))]
public class Platform_Moving : MonoBehaviour
{
    private const float TopContactNormalThreshold = 0.5f;
    private const float TopContactVerticalTolerance = 0.1f;
    private const float TopContactHorizontalInset = 0.01f;
    private const float ElevatorRideZoneBelowTop = 0.2f;
    private const float ReturnBlockCastSkin = 0.02f;
    private const float GizmoWaypointRadius = 0.12f;
    private const float GizmoArrowHeadLength = 0.3f;
    private const float GizmoArrowHeadAngle = 25f;

    public enum ActivationMode
    {
        Automatic,
        PlayerOnPlatform,
        ExternalSignal,
        Elevator
    }

    public enum MovementMode
    {
        OneWay,
        Loop,
        PingPong
    }

    private enum ElevatorState
    {
        IdleAtStart,
        MovingToEnd,
        HoldingAtEnd,
        ReturningToStart,
        BlockedWhileReturning
    }

    [Serializable]
    private class TriggerSettings
    {
        [Tooltip("How this platform becomes active.")]
        public ActivationMode activationMode = ActivationMode.Automatic;

        [Tooltip("For Automatic and External Signal, start already active.")]
        public bool startActivated = true;

        [Tooltip("In Elevator mode, wait this long after the rider leaves before returning.")]
        public float elevatorReturnDelay = 0.15f;

        [Tooltip("How close above the platform top a rider must be before activation counts.")]
        public float activationDistanceAboveTop = 0.05f;
    }

    [Serializable]
    private class PathSettings
    {
        [Tooltip("OneWay moves forward once, Loop repeats from the start, PingPong goes back and forth.")]
        public MovementMode movementMode = MovementMode.OneWay;

        [Tooltip("Offset from the starting point to the first waypoint.")]
        public Vector2 firstWaypointOffset = new Vector2(3f, 0f);

        [Tooltip("Additional offsets chained from the previous waypoint.")]
        public List<Vector2> additionalWaypointOffsets = new List<Vector2>();

        [Tooltip("Move speed toward the current waypoint.")]
        public float moveSpeed = 2f;

        [Tooltip("When inactive, return to the start point.")]
        public bool returnToStartWhenInactive = true;

        [Tooltip("In PlayerOnPlatform mode, return when the player leaves.")]
        public bool returnToStartWhenPlayerLeaves = true;
    }

    [Serializable]
    private class PassengerSettings
    {
        [Tooltip("Send platform velocity to riders that implement IPlatformPassenger2D.")]
        public bool sendCallbacks = true;

        [Tooltip("Parent riders to the platform while they stand on it.")]
        public bool parentToPlatform = false;
    }

    [Serializable]
    private class VisualSettings
    {
        [Tooltip("SpriteRenderer to tint. Leave empty to auto-find one on this object or its children.")]
        public SpriteRenderer targetRenderer;

        [Tooltip("If enabled, lerp toward Arrival Color near the current destination.")]
        public bool useArrivalColorLerp = false;

        [Tooltip("Color reached as the platform gets close to the current destination.")]
        public Color arrivalColor = Color.white;

        [Tooltip("Distance from the destination at which arrival tinting begins.")]
        public float arrivalColorLerpDistance = 0.5f;

        [Tooltip("If enabled, lerp toward Active Color while the platform is active.")]
        public bool useActiveColorLerp = false;

        [Tooltip("Color used while the platform is active.")]
        public Color activeColor = Color.white;

        [Tooltip("Overall color lerp speed.")]
        public float colorLerpSpeed = 4f;

    }

    [SerializeField] private TriggerSettings trigger = new TriggerSettings();
    [SerializeField] private PathSettings path = new PathSettings();
    [SerializeField] private PassengerSettings passenger = new PassengerSettings();
    [SerializeField] private VisualSettings visual = new VisualSettings();

    public Vector2 CurrentLinearVelocity => currentLinearVelocity;
    public ActivationMode CurrentActivationMode => trigger.activationMode;
    public MovementMode CurrentMovementMode => path.movementMode;
    public Vector2 FirstWaypointOffset => path.firstWaypointOffset;
    public IReadOnlyList<Vector2> AdditionalWaypointOffsets => path.additionalWaypointOffsets;
    public bool SendPassengerCallbacks => passenger.sendCallbacks;
    public bool ShouldApplyVerticalPassengerVelocity => IsElevatorMode;
    public event Action<Vector2> PlatformVelocityChanged;

    private readonly List<Vector2> routePoints = new List<Vector2>();
    private readonly HashSet<Transform> passengers = new HashSet<Transform>();
    private readonly HashSet<Collider2D> passengerColliders = new HashSet<Collider2D>();
    private readonly Dictionary<Transform, int> passengerContactCounts = new Dictionary<Transform, int>();
    private readonly RaycastHit2D[] returnBlockHits = new RaycastHit2D[8];

    private Rigidbody2D platformBody;
    private Collider2D platformCollider;
    private SpriteRenderer platformRenderer;
    private Vector2 currentLinearVelocity;
    private Vector2 startPoint;
    private bool isActivated;
    private int currentRouteIndex = 1;
    private int routeDirection = 1;
    private bool holdPositionOneFixedStep;
    private Color defaultColor = Color.white;
    private ElevatorState elevatorState = ElevatorState.IdleAtStart;
    private bool hasTopPassengerNow;
    private bool hadTopPassengerLastFixedFrame;
    private bool hasEffectiveTopPassenger;
    private bool hadEffectiveTopPassengerLastFixedFrame;
    private float timeWithoutTopPassenger;
    private bool holdElevatorOnBoardingFrame;
    private bool IsElevatorMode => trigger.activationMode == ActivationMode.Elevator;

    private void Awake()
    {
        SyncCapsuleColliderDirections();

        platformBody = GetComponent<Rigidbody2D>();
        platformCollider = FindPlatformCollider();
        platformBody.bodyType = RigidbodyType2D.Kinematic;
        platformBody.gravityScale = 0f;
        platformBody.freezeRotation = true;
        platformBody.linearVelocity = Vector2.zero;

        CacheVisualTarget();
        RefreshRoute();
        platformBody.position = LocalToWorldPoint(startPoint);
        ResetRuntimeState();
    }

    private void Start()
    {
        ResetRuntimeState();
        ApplyColorImmediate(GetDesiredColor());
    }

    private void OnValidate()
    {
        SyncCapsuleColliderDirections();

        if (path.moveSpeed < 0f)
            path.moveSpeed = 0f;

        if (trigger.elevatorReturnDelay < 0f)
            trigger.elevatorReturnDelay = 0f;

        if (trigger.activationDistanceAboveTop < 0f)
            trigger.activationDistanceAboveTop = 0f;

        if (visual.arrivalColorLerpDistance < 0f)
            visual.arrivalColorLerpDistance = 0f;

        if (visual.colorLerpSpeed < 0f)
            visual.colorLerpSpeed = 0f;

        platformCollider = FindPlatformCollider();
        CacheVisualTarget();
        RefreshRoute();
    }

    private void OnDrawGizmosSelected()
    {
        List<Vector3> gizmoRoutePoints = new List<Vector3>();
        BuildGizmoWorldRoutePoints(gizmoRoutePoints);

        if (gizmoRoutePoints.Count == 0)
            return;

        Color previousColor = Gizmos.color;
        Vector3 forwardOffset = Vector3.forward * 0.02f;
        bool hasPlatformGizmoBounds = TryGetPlatformGizmoBounds(out Vector3 gizmoBoundsCenterOffset, out Vector3 gizmoBoundsSize);

        for (int i = 0; i < gizmoRoutePoints.Count; i++)
        {
            Vector3 waypointWorldPosition = gizmoRoutePoints[i] + forwardOffset;
            Gizmos.color = i == 0 ? new Color(0.35f, 0.9f, 1f, 0.95f) : new Color(1f, 0.75f, 0.2f, 0.95f);
            Gizmos.DrawSphere(waypointWorldPosition, GizmoWaypointRadius);
            Gizmos.DrawWireSphere(waypointWorldPosition, GizmoWaypointRadius * 1.35f);

            if (hasPlatformGizmoBounds)
            {
                Gizmos.color = i == 0 ? new Color(0.35f, 0.9f, 1f, 0.45f) : new Color(1f, 0.75f, 0.2f, 0.45f);
                Gizmos.DrawWireCube(waypointWorldPosition + gizmoBoundsCenterOffset, gizmoBoundsSize);
            }

            if (i >= gizmoRoutePoints.Count - 1)
                continue;

            Vector3 nextWaypointWorldPosition = gizmoRoutePoints[i + 1] + forwardOffset;
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.95f);
            Gizmos.DrawLine(waypointWorldPosition, nextWaypointWorldPosition);
            DrawGizmoArrowHead(waypointWorldPosition, nextWaypointWorldPosition);
        }

        Gizmos.color = previousColor;
    }

    private bool TryGetPlatformGizmoBounds(out Vector3 centerOffset, out Vector3 size)
    {
        centerOffset = Vector3.zero;
        size = Vector3.zero;

        Collider2D gizmoCollider = platformCollider != null ? platformCollider : FindPlatformCollider();
        if (gizmoCollider == null)
            return false;

        Bounds bounds = gizmoCollider.bounds;
        centerOffset = bounds.center - transform.position;
        size = bounds.size;
        return size.sqrMagnitude > Mathf.Epsilon;
    }

    public void ResetToStart()
    {
        // 1. 경로 및 상태 초기화
        ResetRouteState();
        ResetRuntimeState();

        // 2. 위치를 시작 지점으로 즉시 이동 (Snap)
        if (platformBody != null)
        {
            Vector2 startWorldPos = LocalToWorldPoint(startPoint);
            platformBody.position = startWorldPos;
            transform.position = startWorldPos; // 비주얼 싱크
        }

        // 3. 속도 초기화
        currentLinearVelocity = Vector2.zero;

        // 4. 색상 초기화
        ApplyColorImmediate(defaultColor);

        Debug.Log($"[Platform_Moving] {gameObject.name} Reset to Start Position.");
    }

    public void SetExternalSignal(bool active)
    {
        if (IsElevatorMode)
        {
            elevatorState = active ? ElevatorState.MovingToEnd : ElevatorState.ReturningToStart;
            return;
        }

        if (trigger.activationMode == ActivationMode.ExternalSignal)
            isActivated = active;
    }

    public void ToggleExternalSignal()
    {
        if (IsElevatorMode)
        {
            elevatorState = elevatorState == ElevatorState.IdleAtStart || elevatorState == ElevatorState.ReturningToStart
                ? ElevatorState.MovingToEnd
                : ElevatorState.ReturningToStart;
            return;
        }

        if (trigger.activationMode == ActivationMode.ExternalSignal)
            isActivated = !isActivated;
    }

    public void ActivatePlatform()
    {
        if (IsElevatorMode)
        {
            if (elevatorState == ElevatorState.IdleAtStart)
                elevatorState = ElevatorState.MovingToEnd;
            return;
        }

        isActivated = true;
    }

    public void DeactivatePlatform()
    {
        if (IsElevatorMode)
        {
            if (elevatorState != ElevatorState.IdleAtStart)
                elevatorState = ElevatorState.ReturningToStart;
            return;
        }

        isActivated = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        RegisterPassenger(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        UnregisterPassenger(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (HasTopContact(collision))
            RegisterPassenger(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTriggerPassengerStay(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTriggerPassengerStay(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        UnregisterPassenger(other);
    }

    private void RegisterPassenger(Collider2D other)
    {
        if (other == null || !passengerColliders.Add(other))
            return;

        Transform passengerRoot = GetPassengerRoot(other);
        if (passengerRoot == null || !HasPassengerListener(passengerRoot))
        {
            passengerColliders.Remove(other);
            return;
        }

        if (passengerContactCounts.TryGetValue(passengerRoot, out int currentCount))
        {
            passengerContactCounts[passengerRoot] = currentCount + 1;
            return;
        }

        passengerContactCounts[passengerRoot] = 1;
        if (!passengers.Add(passengerRoot))
            return;

        if (trigger.activationMode == ActivationMode.PlayerOnPlatform)
            isActivated = true;

        if (passenger.parentToPlatform)
            passengerRoot.SetParent(transform);

        SendPassengerVelocity(passengerRoot, CurrentLinearVelocity);
    }

    private void RegisterPassenger(Collision2D collision)
    {
        if (!HasTopContact(collision))
            return;

        RegisterPassenger(collision.collider);
    }

    private void HandleTriggerPassengerStay(Collider2D other)
    {
        if (IsTopTriggerContact(other))
            RegisterPassenger(other);
    }

    private void UnregisterPassenger(Collider2D other)
    {
        if (other == null || !passengerColliders.Remove(other))
            return;

        Transform passengerRoot = GetPassengerRoot(other);
        if (passengerRoot == null || !passengerContactCounts.TryGetValue(passengerRoot, out int currentCount))
            return;

        if (currentCount > 1)
        {
            passengerContactCounts[passengerRoot] = currentCount - 1;
            return;
        }

        passengerContactCounts.Remove(passengerRoot);
        if (!passengers.Remove(passengerRoot))
            return;

        if (passenger.parentToPlatform && passengerRoot.parent == transform)
            passengerRoot.SetParent(null);

        SendPassengerVelocity(passengerRoot, Vector2.zero);

        if (trigger.activationMode == ActivationMode.PlayerOnPlatform && passengers.Count == 0)
            isActivated = false;
    }

    private void FixedUpdate()
    {
        UpdatePassengerState();

        Vector2 nextPosition = IsElevatorMode
            ? GetElevatorNextPosition()
            : GetStandardNextPosition();

        MovePlatform(nextPosition);
        UpdateVisualColor();
        hadTopPassengerLastFixedFrame = hasTopPassengerNow;
        hadEffectiveTopPassengerLastFixedFrame = hasEffectiveTopPassenger;
    }

    private void UpdatePassengerState()
    {
        hasTopPassengerNow = IsElevatorMode
            ? IsPlayerInsideElevatorRideZone()
            : passengers.Count > 0;

        if (hasTopPassengerNow)
            timeWithoutTopPassenger = 0f;
        else
            timeWithoutTopPassenger += Time.fixedDeltaTime;

        hasEffectiveTopPassenger = hasTopPassengerNow || timeWithoutTopPassenger < trigger.elevatorReturnDelay;
    }

    private Vector2 GetStandardNextPosition()
    {
        Vector2 currentLocalPosition = GetCurrentLocalPosition();

        if (isActivated)
            return GetNextRoutePosition(currentLocalPosition);

        if (!ShouldReturnToStart())
            return platformBody.position;

        Vector2 nextLocalPosition = Vector2.MoveTowards(
            currentLocalPosition,
            startPoint,
            path.moveSpeed * Time.fixedDeltaTime);

        if (Vector2.Distance(nextLocalPosition, startPoint) <= 0.001f)
        {
            ResetRouteState();
            return LocalToWorldPoint(startPoint);
        }

        Vector2 nextWorldPosition = LocalToWorldPoint(nextLocalPosition);
        if (ShouldBlockReturnMovement(nextWorldPosition))
            return platformBody.position;

        return nextWorldPosition;
    }

    private Vector2 GetElevatorNextPosition()
    {
        UpdateElevatorStateBeforeMove();

        if (holdElevatorOnBoardingFrame)
        {
            holdElevatorOnBoardingFrame = false;
            return platformBody.position;
        }

        Vector2 currentLocalPosition = GetCurrentLocalPosition();
        Vector2 nextLocalPosition = currentLocalPosition;
        Vector2 elevatorEndPoint = GetElevatorEndPoint();

        switch (elevatorState)
        {
            case ElevatorState.MovingToEnd:
                nextLocalPosition = Vector2.MoveTowards(
                    currentLocalPosition,
                    elevatorEndPoint,
                    path.moveSpeed * Time.fixedDeltaTime);
                break;

            case ElevatorState.ReturningToStart:
            case ElevatorState.BlockedWhileReturning:
                nextLocalPosition = Vector2.MoveTowards(
                    currentLocalPosition,
                    startPoint,
                    path.moveSpeed * Time.fixedDeltaTime);
                break;
        }

        Vector2 nextWorldPosition = LocalToWorldPoint(nextLocalPosition);

        if (elevatorState == ElevatorState.ReturningToStart || elevatorState == ElevatorState.BlockedWhileReturning)
        {
            if (ShouldBlockReturnMovement(nextWorldPosition))
            {
                elevatorState = ElevatorState.BlockedWhileReturning;
                return platformBody.position;
            }

            elevatorState = ElevatorState.ReturningToStart;
        }

        UpdateElevatorStateAfterMove(nextLocalPosition);
        return nextWorldPosition;
    }

    private void UpdateElevatorStateBeforeMove()
    {
        bool boardedThisFrame = !hadEffectiveTopPassengerLastFixedFrame && hasEffectiveTopPassenger;
        bool leftThisFrame = hadEffectiveTopPassengerLastFixedFrame && !hasEffectiveTopPassenger;

        switch (elevatorState)
        {
            case ElevatorState.IdleAtStart:
                if (boardedThisFrame || hasEffectiveTopPassenger)
                {
                    elevatorState = ElevatorState.MovingToEnd;
                    if (boardedThisFrame)
                        holdElevatorOnBoardingFrame = true;
                }
                break;

            case ElevatorState.MovingToEnd:
                if (leftThisFrame || !hasEffectiveTopPassenger)
                    elevatorState = ElevatorState.ReturningToStart;
                break;

            case ElevatorState.HoldingAtEnd:
                if (leftThisFrame || !hasEffectiveTopPassenger)
                    elevatorState = ElevatorState.ReturningToStart;
                break;
        }
    }

    private void UpdateElevatorStateAfterMove(Vector2 nextLocalPosition)
    {
        Vector2 elevatorEndPoint = GetElevatorEndPoint();

        if (elevatorState == ElevatorState.MovingToEnd &&
            Vector2.Distance(nextLocalPosition, elevatorEndPoint) <= 0.001f)
        {
            elevatorState = hasEffectiveTopPassenger
                ? ElevatorState.HoldingAtEnd
                : ElevatorState.ReturningToStart;
            return;
        }

        if ((elevatorState == ElevatorState.ReturningToStart || elevatorState == ElevatorState.BlockedWhileReturning) &&
            Vector2.Distance(nextLocalPosition, startPoint) <= 0.001f)
        {
            ResetRouteState();
            elevatorState = ElevatorState.IdleAtStart;
        }
    }

    private Vector2 GetNextRoutePosition(Vector2 currentLocalPosition)
    {
        if (routePoints.Count <= 1)
            return platformBody.position;

        if (holdPositionOneFixedStep)
        {
            holdPositionOneFixedStep = false;
            return platformBody.position;
        }

        currentRouteIndex = Mathf.Clamp(currentRouteIndex, 0, routePoints.Count - 1);

        Vector2 targetPoint = routePoints[currentRouteIndex];
        Vector2 nextLocalPosition = Vector2.MoveTowards(
            currentLocalPosition,
            targetPoint,
            path.moveSpeed * Time.fixedDeltaTime);

        if (Vector2.Distance(nextLocalPosition, targetPoint) <= 0.001f)
        {
            nextLocalPosition = targetPoint;
            AdvanceRouteIndex();
            holdPositionOneFixedStep = true;
        }

        return LocalToWorldPoint(nextLocalPosition);
    }

    private void MovePlatform(Vector2 nextPosition)
    {
        Vector2 linearVelocity = (nextPosition - platformBody.position) / Time.fixedDeltaTime;
        currentLinearVelocity = linearVelocity;

        platformBody.MovePosition(nextPosition);

        PlatformVelocityChanged?.Invoke(linearVelocity);
        if (!passenger.sendCallbacks || passengers.Count == 0)
            return;

        foreach (Transform passengerRoot in passengers)
        {
            SendPassengerVelocity(passengerRoot, linearVelocity);
        }
    }

    private void CacheVisualTarget()
    {
        platformRenderer = visual.targetRenderer != null
            ? visual.targetRenderer
            : GetComponent<SpriteRenderer>();

        if (platformRenderer == null)
            platformRenderer = GetComponentInChildren<SpriteRenderer>();

        if (platformRenderer != null)
            defaultColor = platformRenderer.color;

    }

    private void UpdateVisualColor()
    {
        if (platformRenderer == null)
            return;

        Color desiredColor = GetDesiredColor();
        float lerpT = visual.colorLerpSpeed <= 0f
            ? 1f
            : Mathf.Clamp01(visual.colorLerpSpeed * Time.fixedDeltaTime);

        platformRenderer.color = Color.Lerp(platformRenderer.color, desiredColor, lerpT);
    }

    private Color GetDesiredColor()
    {
        Color targetColor = defaultColor;

        if (visual.useActiveColorLerp && IsVisualStateActive())
            targetColor = visual.activeColor;

        if (visual.useArrivalColorLerp && TryGetCurrentTargetPoint(out Vector2 targetPoint))
        {
            float distance = Vector2.Distance(platformBody.position, LocalToWorldPoint(targetPoint));
            float blendRange = visual.arrivalColorLerpDistance;
            if (blendRange > 0f)
            {
                float blend = 1f - Mathf.Clamp01(distance / blendRange);
                targetColor = Color.Lerp(targetColor, visual.arrivalColor, blend);
            }
        }

        return targetColor;
    }

    private bool IsVisualStateActive()
    {
        if (!IsElevatorMode)
            return isActivated;

        return elevatorState != ElevatorState.IdleAtStart;
    }

    private void ApplyColorImmediate(Color color)
    {
        if (platformRenderer != null)
            platformRenderer.color = color;
    }

    private bool TryGetCurrentTargetPoint(out Vector2 targetPoint)
    {
        targetPoint = startPoint;

        if (IsElevatorMode)
        {
            switch (elevatorState)
            {
                case ElevatorState.MovingToEnd:
                    targetPoint = GetElevatorEndPoint();
                    return true;

                case ElevatorState.ReturningToStart:
                case ElevatorState.BlockedWhileReturning:
                    targetPoint = startPoint;
                    return true;
            }

            return false;
        }

        if (routePoints.Count <= 1)
            return false;

        if (isActivated)
        {
            if (currentRouteIndex < 0 || currentRouteIndex >= routePoints.Count)
                return false;

            targetPoint = routePoints[currentRouteIndex];
            return true;
        }

        Vector2 currentLocalPosition = GetCurrentLocalPosition();
        if (currentRouteIndex >= 0 && currentRouteIndex < routePoints.Count)
        {
            Vector2 currentRoutePoint = routePoints[currentRouteIndex];
            if (Vector2.Distance(currentLocalPosition, currentRoutePoint) <= 0.001f)
            {
                targetPoint = currentRoutePoint;
                return true;
            }
        }

        if (ShouldReturnToStart() && Vector2.Distance(currentLocalPosition, startPoint) > 0.001f)
        {
            targetPoint = startPoint;
            return true;
        }

        if (Vector2.Distance(currentLocalPosition, startPoint) <= 0.001f)
            return false;

        targetPoint = GetClosestRoutePoint(currentLocalPosition);
        return true;
    }

    private Vector2 GetClosestRoutePoint(Vector2 currentLocalPosition)
    {
        if (routePoints.Count == 0)
            return startPoint;

        Vector2 closestPoint = routePoints[0];
        float closestDistance = Vector2.SqrMagnitude(currentLocalPosition - closestPoint);

        for (int i = 1; i < routePoints.Count; i++)
        {
            float distance = Vector2.SqrMagnitude(currentLocalPosition - routePoints[i]);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestPoint = routePoints[i];
        }

        return closestPoint;
    }

    private void RefreshRoute()
    {
        startPoint = transform.localPosition;
        BuildRoutePoints(routePoints, startPoint);

        ResetRouteState();
    }

    private void BuildRoutePoints(List<Vector2> targetRoutePoints, Vector2 originPoint)
    {
        targetRoutePoints.Clear();
        targetRoutePoints.Add(originPoint);
        targetRoutePoints.Add(originPoint + path.firstWaypointOffset);

        Vector2 currentPoint = targetRoutePoints[1];
        for (int i = 0; i < path.additionalWaypointOffsets.Count; i++)
        {
            currentPoint += path.additionalWaypointOffsets[i];
            targetRoutePoints.Add(currentPoint);
        }
    }

    private void BuildGizmoWorldRoutePoints(List<Vector3> targetRoutePoints)
    {
        targetRoutePoints.Clear();

        Vector3 currentPoint = transform.position;
        targetRoutePoints.Add(currentPoint);

        currentPoint += (Vector3)path.firstWaypointOffset;
        targetRoutePoints.Add(currentPoint);

        for (int i = 0; i < path.additionalWaypointOffsets.Count; i++)
        {
            currentPoint += (Vector3)path.additionalWaypointOffsets[i];
            targetRoutePoints.Add(currentPoint);
        }
    }

    private void DrawGizmoArrowHead(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector3 normalizedDirection = direction.normalized;
        Vector3 arrowBase = end - (normalizedDirection * GizmoArrowHeadLength);
        Vector3 rightWing = Quaternion.Euler(0f, 0f, GizmoArrowHeadAngle) * -normalizedDirection * GizmoArrowHeadLength;
        Vector3 leftWing = Quaternion.Euler(0f, 0f, -GizmoArrowHeadAngle) * -normalizedDirection * GizmoArrowHeadLength;

        Gizmos.DrawLine(end, arrowBase + rightWing);
        Gizmos.DrawLine(end, arrowBase + leftWing);
    }

    private Vector2 GetElevatorEndPoint()
    {
        return routePoints.Count > 0
            ? routePoints[routePoints.Count - 1]
            : startPoint;
    }

    private void AdvanceRouteIndex()
    {
        switch (path.movementMode)
        {
            case MovementMode.OneWay:
                if (currentRouteIndex < routePoints.Count - 1)
                {
                    currentRouteIndex++;
                }
                else
                {
                    isActivated = false;
                }
                break;

            case MovementMode.Loop:
                currentRouteIndex = (currentRouteIndex + 1) % routePoints.Count;
                break;

            case MovementMode.PingPong:
                if (currentRouteIndex >= routePoints.Count - 1)
                    routeDirection = -1;
                else if (currentRouteIndex <= 0)
                    routeDirection = 1;

                currentRouteIndex += routeDirection;
                break;
        }
    }

    private void ResetRouteState()
    {
        currentRouteIndex = routePoints.Count > 1 ? 1 : 0;
        routeDirection = 1;
        holdPositionOneFixedStep = false;
    }

    private void ResetRuntimeState()
    {
        passengerColliders.Clear();
        passengerContactCounts.Clear();
        passengers.Clear();
        hasTopPassengerNow = false;
        hadTopPassengerLastFixedFrame = false;
        hasEffectiveTopPassenger = false;
        hadEffectiveTopPassengerLastFixedFrame = false;
        timeWithoutTopPassenger = trigger.elevatorReturnDelay;
        holdElevatorOnBoardingFrame = false;
        currentLinearVelocity = Vector2.zero;

        if (IsElevatorMode)
        {
            elevatorState = ElevatorState.IdleAtStart;
            isActivated = false;
            return;
        }

        isActivated = trigger.activationMode == ActivationMode.PlayerOnPlatform
            ? false
            : trigger.startActivated;
    }

    private Vector2 GetCurrentLocalPosition()
    {
        if (platformBody == null)
            return transform.localPosition;

        if (transform.parent == null)
            return platformBody.position;

        return transform.parent.InverseTransformPoint(platformBody.position);
    }

    private Vector2 LocalToWorldPoint(Vector2 localPoint)
    {
        if (transform.parent == null)
            return localPoint;

        return transform.parent.TransformPoint(localPoint);
    }

    private bool ShouldReturnToStart()
    {
        if (trigger.activationMode == ActivationMode.PlayerOnPlatform)
            return path.returnToStartWhenPlayerLeaves;

        return path.returnToStartWhenInactive;
    }

    private bool ShouldBlockReturnMovement(Vector2 nextPosition)
    {
        if (platformCollider == null)
            return false;

        Vector2 moveDelta = nextPosition - platformBody.position;
        float moveDistance = moveDelta.magnitude;
        if (moveDistance <= Mathf.Epsilon)
            return false;

        ContactFilter2D filter = default;
        filter.useTriggers = false;

        int hitCount = platformCollider.Cast(
            moveDelta.normalized,
            filter,
            returnBlockHits,
            moveDistance + ReturnBlockCastSkin);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = returnBlockHits[i].collider;
            if (hitCollider == null)
                continue;

            Transform hitRoot = hitCollider.attachedRigidbody != null
                ? hitCollider.attachedRigidbody.transform
                : hitCollider.transform.root;

            if (hitRoot != null && hitRoot.CompareTag("Player"))
                return true;
        }

        return false;
    }

    private bool HasTopContact(Collision2D collision)
    {
        if (platformCollider == null || collision == null || !IsClearlyAbovePlatform(collision.collider))
            return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y >= TopContactNormalThreshold)
                return true;

            if (contact.point.y >= platformCollider.bounds.max.y - TopContactVerticalTolerance)
                return true;
        }

        return false;
    }

    private bool IsTopTriggerContact(Collider2D other)
    {
        return IsClearlyAbovePlatform(other);
    }

    private bool IsClearlyAbovePlatform(Collider2D other)
    {
        if (platformCollider == null || other == null)
            return false;

        Bounds platformBounds = platformCollider.bounds;
        Bounds otherBounds = other.bounds;
        float minimumFeetHeight = platformBounds.max.y - TopContactVerticalTolerance;
        float maximumFeetHeight = platformBounds.max.y + trigger.activationDistanceAboveTop;
        float leftLimit = platformBounds.min.x + TopContactHorizontalInset;
        float rightLimit = platformBounds.max.x - TopContactHorizontalInset;

        bool overlapsHorizontally = otherBounds.max.x > leftLimit &&
                                    otherBounds.min.x < rightLimit;
        bool feetAreNearTop = otherBounds.min.y >= minimumFeetHeight &&
                              otherBounds.min.y <= maximumFeetHeight;

        return overlapsHorizontally && feetAreNearTop;
    }

    private bool IsPlayerInsideElevatorRideZone()
    {
        if (platformCollider == null || Player.Instance == null)
            return false;

        Collider2D playerCollider = Player.Instance.GetComponent<Collider2D>();
        if (playerCollider == null)
            return false;

        Bounds platformBounds = platformCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        float leftLimit = platformBounds.min.x + TopContactHorizontalInset;
        float rightLimit = platformBounds.max.x - TopContactHorizontalInset;
        float zoneBottom = platformBounds.max.y - ElevatorRideZoneBelowTop;
        float zoneTop = platformBounds.max.y + trigger.activationDistanceAboveTop;

        bool overlapsHorizontally = playerBounds.max.x > leftLimit &&
                                    playerBounds.min.x < rightLimit;
        bool overlapsVertically = playerBounds.max.y >= zoneBottom &&
                                  playerBounds.min.y <= zoneTop;

        return overlapsHorizontally && overlapsVertically;
    }

    private Collider2D FindPlatformCollider()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        Collider2D fallbackCollider = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate.isTrigger)
                continue;

            if (candidate is BoxCollider2D)
                return candidate;

            if (fallbackCollider == null)
                fallbackCollider = candidate;
        }

        return fallbackCollider;
    }

    private void SyncCapsuleColliderDirections()
    {
        PlatformContactUtility2D.SyncCapsuleDirections(GetComponents<Collider2D>());
    }

    private void SendPassengerVelocity(Transform passengerRoot, Vector2 linearVelocity)
    {
        if (!passenger.sendCallbacks || passengerRoot == null)
            return;

        MonoBehaviour[] behaviours = passengerRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlatformPassenger2D rider)
            {
                rider.OnPlatformVelocityChanged(this, linearVelocity);
                return;
            }
        }
    }

    private bool HasPassengerListener(Transform passengerRoot)
    {
        MonoBehaviour[] behaviours = passengerRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlatformPassenger2D)
                return true;
        }

        return false;
    }

    private Transform GetPassengerRoot(Collider2D other)
    {
        if (!PlatformContactUtility2D.ResolveRoot(other, out Transform passengerRoot, out Rigidbody2D passengerBody))
            return null;

        return passengerBody != null ? passengerBody.transform : passengerRoot;
    }
}
