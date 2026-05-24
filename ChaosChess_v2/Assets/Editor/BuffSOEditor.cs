using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuffSO), true)]
public class BuffSOEditor : Editor
{
    private SerializedProperty buffDescription;
    private SerializedProperty debuffDescription;

    private SerializedProperty hasBuff;
    private SerializedProperty buffWeight;
    private SerializedProperty buffValue;
    private SerializedProperty useRandomBuffValue;
    private SerializedProperty buffRange;

    private SerializedProperty hasDebuff;
    private SerializedProperty debuffWeight;
    private SerializedProperty debuffValue;
    private SerializedProperty useRandomDebuffValue;
    private SerializedProperty debuffRange;

    private SerializedProperty useTensOnly;
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;

    private void OnEnable()
    {
        buffDescription = serializedObject.FindProperty("buffDescription");
        debuffDescription = serializedObject.FindProperty("debuffDescription");

        hasBuff = serializedObject.FindProperty("hasBuff");
        buffWeight = serializedObject.FindProperty("buffWeight");
        buffValue = serializedObject.FindProperty("buffValue");
        useRandomBuffValue = serializedObject.FindProperty("useRandomBuffValue");
        buffRange = serializedObject.FindProperty("buffRange");

        hasDebuff = serializedObject.FindProperty("hasDebuff");
        debuffWeight = serializedObject.FindProperty("debuffWeight");
        debuffValue = serializedObject.FindProperty("debuffValue");
        useRandomDebuffValue = serializedObject.FindProperty("useRandomDebuffValue");
        debuffRange = serializedObject.FindProperty("debuffRange");

        useTensOnly = serializedObject.FindProperty("useTensOnly");

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12
        };
        boxStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(10, 10, 8, 8)
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSection("공통 옵션", () =>
        {
            EditorGUILayout.PropertyField(useTensOnly, new GUIContent("10 단위 값만 사용"));
        });

        DrawBuffSection();
        DrawDebuffSection();
        EditorGUILayout.HelpBox("설명의 {value} 는 실제 적용 수치로 자동 치환됩니다.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBuffSection()
    {
        DrawSection("버프 설정", () =>
        {
            EditorGUILayout.PropertyField(hasBuff, new GUIContent("버프 사용"));
            if (!hasBuff.boolValue) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(buffDescription, new GUIContent("설명"));
            EditorGUILayout.PropertyField(buffWeight, new GUIContent("등장 가중치"));
            EditorGUILayout.PropertyField(useRandomBuffValue, new GUIContent("랜덤 값 사용"));
            if (useRandomBuffValue.boolValue)
            {
                EditorGUILayout.PropertyField(buffRange, new GUIContent("값 범위 (최소/최대)"));
            }
            else
            {
                EditorGUILayout.PropertyField(buffValue, new GUIContent("고정 값"));
            }
            EditorGUI.indentLevel--;
        });
    }

    private void DrawDebuffSection()
    {
        DrawSection("디버프 설정", () =>
        {
            EditorGUILayout.PropertyField(hasDebuff, new GUIContent("디버프 사용"));
            if (!hasDebuff.boolValue) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(debuffDescription, new GUIContent("설명"));
            EditorGUILayout.PropertyField(debuffWeight, new GUIContent("등장 가중치"));
            EditorGUILayout.PropertyField(useRandomDebuffValue, new GUIContent("랜덤 값 사용"));
            if (useRandomDebuffValue.boolValue)
            {
                EditorGUILayout.PropertyField(debuffRange, new GUIContent("값 범위 (최소/최대)"));
            }
            else
            {
                EditorGUILayout.PropertyField(debuffValue, new GUIContent("고정 값"));
            }
            EditorGUI.indentLevel--;
        });
    }

    private void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.LabelField(title, headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            drawContent.Invoke();
        }
        EditorGUILayout.Space(6f);
    }
}
