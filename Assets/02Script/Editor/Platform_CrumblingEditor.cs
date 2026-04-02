using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Platform_Crumbling))]
public class Platform_CrumblingEditor : Editor
{
    private SerializedProperty currentStateProperty;
    private SerializedProperty triggerModeProperty;
    private SerializedProperty respawnModeProperty;
    private SerializedProperty playerTagProperty;
    private SerializedProperty automaticIntervalProperty;
    private SerializedProperty playerStepDelayProperty;
    private SerializedProperty respawnDelayProperty;
    private SerializedProperty platformColliderProperty;
    private SerializedProperty respawnBlockColliderProperty;
    private SerializedProperty platformRendererProperty;
    private SerializedProperty useFadeLerpProperty;
    private SerializedProperty fadeDurationProperty;

    private void OnEnable()
    {
        EditorApplication.update += SyncCapsuleDirectionsInEditor;

        currentStateProperty = serializedObject.FindProperty("currentState");
        triggerModeProperty = serializedObject.FindProperty("triggerMode");
        respawnModeProperty = serializedObject.FindProperty("respawnMode");
        playerTagProperty = serializedObject.FindProperty("playerTag");
        automaticIntervalProperty = serializedObject.FindProperty("automaticInterval");
        playerStepDelayProperty = serializedObject.FindProperty("playerStepDelay");
        respawnDelayProperty = serializedObject.FindProperty("respawnDelay");
        platformColliderProperty = serializedObject.FindProperty("platformCollider");
        respawnBlockColliderProperty = serializedObject.FindProperty("respawnBlockCollider");
        platformRendererProperty = serializedObject.FindProperty("platformRenderer");
        useFadeLerpProperty = serializedObject.FindProperty("useFadeLerp");
        fadeDurationProperty = serializedObject.FindProperty("fadeDuration");
    }

    private void OnDisable()
    {
        EditorApplication.update -= SyncCapsuleDirectionsInEditor;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(currentStateProperty);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(triggerModeProperty);

        switch (CurrentTriggerMode)
        {
            case Platform_Crumbling.TriggerMode.AutomaticInterval:
                EditorGUILayout.PropertyField(automaticIntervalProperty, new GUIContent("Interval"));
                break;

            case Platform_Crumbling.TriggerMode.PlayerStepDelay:
                EditorGUILayout.PropertyField(playerTagProperty, new GUIContent("Player Tag"));
                EditorGUILayout.PropertyField(playerStepDelayProperty, new GUIContent("Delay"));
                break;

            case Platform_Crumbling.TriggerMode.ToggleOnJump:
                EditorGUILayout.PropertyField(playerTagProperty, new GUIContent("Player Tag"));
                break;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(platformColliderProperty);
        EditorGUILayout.PropertyField(respawnBlockColliderProperty, new GUIContent("Respawn Block Collider"));
        EditorGUILayout.PropertyField(platformRendererProperty);
        EditorGUILayout.PropertyField(useFadeLerpProperty);

        if (useFadeLerpProperty.boolValue)
        {
            EditorGUILayout.PropertyField(fadeDurationProperty, new GUIContent("Fade Duration"));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Respawn", EditorStyles.boldLabel);

        if (CurrentTriggerMode == Platform_Crumbling.TriggerMode.ToggleOnJump)
        {
            EditorGUILayout.HelpBox(
                "Toggle On Jump 모드에서는 Respawn 설정을 사용하지 않습니다.\n" +
                "숨김 상태에서 점프로 다시 켜지려다 플레이어와 겹쳐 생성이 막히면, 겹침이 해소되는 즉시 자동으로 다시 생성됩니다.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.PropertyField(respawnModeProperty);

            if (CurrentRespawnMode == Platform_Crumbling.RespawnMode.AfterDelay)
            {
                EditorGUILayout.PropertyField(respawnDelayProperty, new GUIContent("Respawn Delay"));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private Platform_Crumbling.TriggerMode CurrentTriggerMode =>
        (Platform_Crumbling.TriggerMode)triggerModeProperty.enumValueIndex;

    private Platform_Crumbling.RespawnMode CurrentRespawnMode =>
        (Platform_Crumbling.RespawnMode)respawnModeProperty.enumValueIndex;

    private void SyncCapsuleDirectionsInEditor()
    {
        if (Application.isPlaying)
            return;

        bool changedAnyCollider = false;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not Platform_Crumbling platform)
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
