using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Platform_Moving))]
public class Platform_MovingEditor : Editor
{
    private SerializedProperty triggerProperty;
    private SerializedProperty pathProperty;
    private SerializedProperty passengerProperty;
    private SerializedProperty visualProperty;

    private SerializedProperty activationModeProperty;
    private SerializedProperty startActivatedProperty;
    private SerializedProperty elevatorReturnDelayProperty;
    private SerializedProperty activationDistanceAboveTopProperty;

    private SerializedProperty movementModeProperty;
    private SerializedProperty firstWaypointOffsetProperty;
    private SerializedProperty additionalWaypointOffsetsProperty;
    private SerializedProperty moveSpeedProperty;
    private SerializedProperty returnToStartWhenInactiveProperty;
    private SerializedProperty returnToStartWhenPlayerLeavesProperty;

    private SerializedProperty sendCallbacksProperty;
    private SerializedProperty parentToPlatformProperty;

    private SerializedProperty targetRendererProperty;
    private SerializedProperty useArrivalColorLerpProperty;
    private SerializedProperty arrivalColorProperty;
    private SerializedProperty arrivalColorLerpDistanceProperty;
    private SerializedProperty useActiveColorLerpProperty;
    private SerializedProperty activeColorProperty;
    private SerializedProperty colorLerpSpeedProperty;

    private bool IsElevatorMode => CurrentActivationMode == Platform_Moving.ActivationMode.Elevator;

    private void OnEnable()
    {
        EditorApplication.update += SyncCapsuleDirectionsInEditor;

        triggerProperty = serializedObject.FindProperty("trigger");
        pathProperty = serializedObject.FindProperty("path");
        passengerProperty = serializedObject.FindProperty("passenger");
        visualProperty = serializedObject.FindProperty("visual");

        activationModeProperty = triggerProperty.FindPropertyRelative("activationMode");
        startActivatedProperty = triggerProperty.FindPropertyRelative("startActivated");
        elevatorReturnDelayProperty = triggerProperty.FindPropertyRelative("elevatorReturnDelay");
        activationDistanceAboveTopProperty = triggerProperty.FindPropertyRelative("activationDistanceAboveTop");

        movementModeProperty = pathProperty.FindPropertyRelative("movementMode");
        firstWaypointOffsetProperty = pathProperty.FindPropertyRelative("firstWaypointOffset");
        additionalWaypointOffsetsProperty = pathProperty.FindPropertyRelative("additionalWaypointOffsets");
        moveSpeedProperty = pathProperty.FindPropertyRelative("moveSpeed");
        returnToStartWhenInactiveProperty = pathProperty.FindPropertyRelative("returnToStartWhenInactive");
        returnToStartWhenPlayerLeavesProperty = pathProperty.FindPropertyRelative("returnToStartWhenPlayerLeaves");

        sendCallbacksProperty = passengerProperty.FindPropertyRelative("sendCallbacks");
        parentToPlatformProperty = passengerProperty.FindPropertyRelative("parentToPlatform");

        targetRendererProperty = visualProperty.FindPropertyRelative("targetRenderer");
        useArrivalColorLerpProperty = visualProperty.FindPropertyRelative("useArrivalColorLerp");
        arrivalColorProperty = visualProperty.FindPropertyRelative("arrivalColor");
        arrivalColorLerpDistanceProperty = visualProperty.FindPropertyRelative("arrivalColorLerpDistance");
        useActiveColorLerpProperty = visualProperty.FindPropertyRelative("useActiveColorLerp");
        activeColorProperty = visualProperty.FindPropertyRelative("activeColor");
        colorLerpSpeedProperty = visualProperty.FindPropertyRelative("colorLerpSpeed");
    }

    private void OnDisable()
    {
        EditorApplication.update -= SyncCapsuleDirectionsInEditor;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Vector2 startPosition = ((Platform_Moving)target).transform.position;
        Platform_Moving.ActivationMode activationMode = CurrentActivationMode;

        DrawTriggerSection(activationMode);
        EditorGUILayout.Space(6f);
        DrawPathSection(startPosition);
        EditorGUILayout.Space(6f);
        DrawPassengerSection();
        EditorGUILayout.Space(6f);
        DrawVisualSection();
        EditorGUILayout.Space(6f);
        DrawBehaviorSection(activationMode);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTriggerSection(Platform_Moving.ActivationMode activationMode)
    {
        EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(activationModeProperty);

        if (activationMode == Platform_Moving.ActivationMode.Elevator)
        {
            EditorGUILayout.PropertyField(elevatorReturnDelayProperty, new GUIContent("Return Delay"));
            EditorGUILayout.PropertyField(activationDistanceAboveTopProperty, new GUIContent("Activation Distance Above Top"));
            EditorGUILayout.HelpBox(
                "Elevator mode moves from the start point to the final waypoint when a rider stands on top. " +
                "It waits at the end while occupied, returns after a short leave delay, and pauses return movement if the player blocks the path.",
                MessageType.Info);
            return;
        }

        if (activationMode == Platform_Moving.ActivationMode.PlayerOnPlatform)
            EditorGUILayout.PropertyField(activationDistanceAboveTopProperty, new GUIContent("Activation Distance Above Top"));

        if (activationMode != Platform_Moving.ActivationMode.PlayerOnPlatform)
            EditorGUILayout.PropertyField(startActivatedProperty);
    }

    private void DrawPathSection(Vector2 startPosition)
    {
        EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);

        if (IsElevatorMode)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Movement Mode", Platform_Moving.MovementMode.OneWay);
            }

            EditorGUILayout.HelpBox(
                "Elevator mode always uses a single route: Start -> Final Waypoint. " +
                "If you add more waypoint offsets, the last one becomes the elevator destination.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.PropertyField(movementModeProperty);
        }

        EditorGUILayout.PropertyField(firstWaypointOffsetProperty, new GUIContent("First Waypoint (+ Offset)"));
        EditorGUILayout.PropertyField(additionalWaypointOffsetsProperty, new GUIContent("Additional Waypoints (+ Offset)"), true);
        EditorGUILayout.PropertyField(moveSpeedProperty);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector2Field("Start Position", startPosition);
            DrawWaypointPreview("Waypoint 1", startPosition, firstWaypointOffsetProperty.vector2Value);

            Vector2 currentPoint = startPosition + firstWaypointOffsetProperty.vector2Value;
            for (int i = 0; i < additionalWaypointOffsetsProperty.arraySize; i++)
            {
                SerializedProperty waypointProperty = additionalWaypointOffsetsProperty.GetArrayElementAtIndex(i);
                DrawWaypointPreview($"Waypoint {i + 2}", currentPoint, waypointProperty.vector2Value);
                currentPoint += waypointProperty.vector2Value;
            }

            if (IsElevatorMode)
                EditorGUILayout.Vector2Field("Elevator Destination", currentPoint);
        }
    }

    private void DrawPassengerSection()
    {
        EditorGUILayout.LabelField("Passenger", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sendCallbacksProperty);
        EditorGUILayout.PropertyField(parentToPlatformProperty);

        if (IsElevatorMode)
        {
            EditorGUILayout.HelpBox(
                "Elevator riders receive both horizontal and vertical platform velocity while standing on top. " +
                "General moving platforms still send horizontal follow velocity only.",
                MessageType.None);
        }
    }

    private void DrawVisualSection()
    {
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetRendererProperty);

        EditorGUILayout.PropertyField(useArrivalColorLerpProperty);
        if (useArrivalColorLerpProperty.boolValue)
        {
            EditorGUILayout.PropertyField(arrivalColorProperty);
            EditorGUILayout.PropertyField(arrivalColorLerpDistanceProperty);
        }

        EditorGUILayout.PropertyField(useActiveColorLerpProperty);
        if (useActiveColorLerpProperty.boolValue)
            EditorGUILayout.PropertyField(activeColorProperty);

        EditorGUILayout.PropertyField(colorLerpSpeedProperty);
    }

    private void DrawBehaviorSection(Platform_Moving.ActivationMode activationMode)
    {
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);

        if (activationMode == Platform_Moving.ActivationMode.Elevator)
        {
            EditorGUILayout.HelpBox(
                "Elevator behavior:\n" +
                "1. Board from the top to start moving.\n" +
                "2. Stay on it to keep it parked at the destination.\n" +
                "3. Step off to make it return after the configured delay.\n" +
                "4. Return movement pauses if the player is in the way.",
                MessageType.Info);
        }
        else if (activationMode == Platform_Moving.ActivationMode.PlayerOnPlatform)
        {
            EditorGUILayout.PropertyField(returnToStartWhenPlayerLeavesProperty);
        }
        else
        {
            EditorGUILayout.PropertyField(returnToStartWhenInactiveProperty);
        }

        EditorGUILayout.HelpBox(
            "Passenger callbacks use IPlatformPassenger2D. Riders receive the platform Rigidbody2D.linearVelocity while on the platform.",
            MessageType.Info);
    }

    private void DrawWaypointPreview(string label, Vector2 startPosition, Vector2 offset)
    {
        EditorGUILayout.Vector2Field($"{label} Start", startPosition);
        EditorGUILayout.Vector2Field($"{label} + Offset", offset);
        EditorGUILayout.Vector2Field($"{label} Result", startPosition + offset);
        EditorGUILayout.Space(2f);
    }

    private Platform_Moving.ActivationMode CurrentActivationMode =>
        (Platform_Moving.ActivationMode)activationModeProperty.enumValueIndex;

    private void SyncCapsuleDirectionsInEditor()
    {
        if (Application.isPlaying)
            return;

        bool changedAnyCollider = false;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not Platform_Moving platform)
                continue;

            Collider2D[] colliders = platform.GetComponents<Collider2D>();
            for (int j = 0; j < colliders.Length; j++)
            {
                if (colliders[j] is not CapsuleCollider2D capsuleCollider)
                    continue;

                if (!PlatformContactUtility2D.SyncCapsuleDirection(capsuleCollider))
                    continue;

                changedAnyCollider = true;
                EditorUtility.SetDirty(capsuleCollider);
            }
        }

        if (changedAnyCollider)
            SceneView.RepaintAll();
    }
}
