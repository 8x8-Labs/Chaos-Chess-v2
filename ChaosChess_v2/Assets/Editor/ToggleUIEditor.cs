using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ToggleUI))]
public class ToggleUIEditor : ToggleEditor
{
    private SerializedProperty _iconImage;
    private SerializedProperty _onIcon;
    private SerializedProperty _offIcon;
    private SerializedProperty _linkedSlider;

    protected override void OnEnable()
    {
        base.OnEnable();
        _iconImage = serializedObject.FindProperty("IconImage");
        _onIcon = serializedObject.FindProperty("OnIcon");
        _offIcon = serializedObject.FindProperty("OffIcon");
        _linkedSlider = serializedObject.FindProperty("LinkedSlider");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        EditorGUILayout.PropertyField(_iconImage);
        EditorGUILayout.PropertyField(_onIcon);
        EditorGUILayout.PropertyField(_offIcon);
        EditorGUILayout.PropertyField(_linkedSlider);
        serializedObject.ApplyModifiedProperties();
    }
}
