using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(UIButton), true)]
[CanEditMultipleObjects]
public class UIButtonEditor : ButtonEditor
{
    // 프로퍼티 변수들
    SerializedProperty clickSoundProp;
    SerializedProperty hoverSoundProp;
    SerializedProperty typeProp;
    SerializedProperty disableObjectProp;
    SerializedProperty enableObjectProp;
    SerializedProperty disablePanelProp;
    SerializedProperty enablePanelProp;
    SerializedProperty startAnimationProp;
    SerializedProperty endAnimationProp;
    SerializedProperty uIAnimationProp;
    SerializedProperty nextSceneNameProp;
    SerializedProperty practiceDifficultyProp;
    SerializedProperty practiceSceneNameProp;
    SerializedProperty rewardSceneNameProp;
    SerializedProperty resultSceneNameProp;
    SerializedProperty mainSceneNameProp;

    //[SerializeField] private bool isStartAnimation = false;
    //[SerializeField] private bool isEndAnimation = false;

    protected override void OnEnable()
    {
        base.OnEnable();
        // 실제 UIButton 클래스의 변수명과 일치해야 합니다.
        clickSoundProp = serializedObject.FindProperty("clickSound");
        hoverSoundProp = serializedObject.FindProperty("hoverSound");
        disableObjectProp = serializedObject.FindProperty("disableCanvas");
        enableObjectProp = serializedObject.FindProperty("enableCanvas");
        disablePanelProp = serializedObject.FindProperty("disablePanel");
        enablePanelProp = serializedObject.FindProperty("enablePanel");
        typeProp = serializedObject.FindProperty("buttonType");
        startAnimationProp = serializedObject.FindProperty("isStartAnimation");
        endAnimationProp = serializedObject.FindProperty("isEndAnimation");
        uIAnimationProp = serializedObject.FindProperty("uIAnimationObject");
        nextSceneNameProp = serializedObject.FindProperty("nextSceneName");
        practiceDifficultyProp = serializedObject.FindProperty("practiceDifficulty");
        practiceSceneNameProp = serializedObject.FindProperty("practiceSceneName");
        rewardSceneNameProp = serializedObject.FindProperty("rewardSceneName");
        resultSceneNameProp = serializedObject.FindProperty("resultSceneName");
        mainSceneNameProp = serializedObject.FindProperty("mainSceneName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. 사운드 설정 (공통)
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(clickSoundProp);
        EditorGUILayout.PropertyField(hoverSoundProp);

        // 2. 버튼 타입 및 타입별 특수 설정
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Button Logic", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(typeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Manage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(uIAnimationProp);
        EditorGUILayout.PropertyField(startAnimationProp);
        EditorGUILayout.PropertyField(endAnimationProp);

        serializedObject.ApplyModifiedProperties();

        // 현재 선택된 Enum 값에 따라 다른 필드를 표시합니다.
        ButtonType currentType = (ButtonType)typeProp.intValue;



        switch (currentType)
        {
            case ButtonType.ChangeCanvas:
                EditorGUILayout.HelpBox("현재 버튼의 캔버스를 끄고 새로운 캔버스를 킵니다.", MessageType.Info);
                EditorGUILayout.PropertyField(disableObjectProp, new GUIContent("Canvas to Disable"));
                EditorGUILayout.PropertyField(enableObjectProp, new GUIContent("Canvas to Enable"));
                break;
            case ButtonType.GameStart:
                EditorGUILayout.HelpBox("게임 시작 데이터를 초기화한 뒤 현재 캔버스를 끄고 새로운 캔버스를 킵니다.", MessageType.Info);
                EditorGUILayout.PropertyField(disableObjectProp, new GUIContent("Canvas to Disable"));
                EditorGUILayout.PropertyField(enableObjectProp, new GUIContent("Canvas to Enable"));
                break;
            case ButtonType.ChangePanel:
                EditorGUILayout.HelpBox("현재 버튼의 패널을 끄고 새로운 패널을 킵니다.", MessageType.Info);
                EditorGUILayout.PropertyField(disablePanelProp, new GUIContent("Panel to Disable"));
                EditorGUILayout.PropertyField(enablePanelProp, new GUIContent("Panel to Enable"));
                break;

            case ButtonType.OpenPopup:
                EditorGUILayout.HelpBox("현재 창은 유지하고 팝업 패널을 엽니다.", MessageType.Info);
                EditorGUILayout.PropertyField(enablePanelProp, new GUIContent("Popup to Open"));
                break;

            case ButtonType.ClosePopup:
                EditorGUILayout.HelpBox("현재 팝업 패널을 닫습니다.", MessageType.Info);
                EditorGUILayout.PropertyField(disablePanelProp, new GUIContent("Popup to Close"));
                break;

            case ButtonType.GoScene:
                EditorGUILayout.HelpBox("지정한 씬으로 이동합니다.", MessageType.Info);
                EditorGUILayout.PropertyField(nextSceneNameProp, new GUIContent("Next Scene Name"));
                break;
            case ButtonType.PracticeStart:
                EditorGUILayout.PropertyField(practiceDifficultyProp, new GUIContent("Practice Difficulty"));
                EditorGUILayout.PropertyField(practiceSceneNameProp, new GUIContent("Practice Scene Name"));
                break;
            case ButtonType.EndGame:
                EditorGUILayout.HelpBox("연습 모드는 MainScene, 일반 모드는 상황에 따라 Reward/Result 씬으로 이동합니다.", MessageType.Info);
                EditorGUILayout.PropertyField(rewardSceneNameProp, new GUIContent("Reward Scene Name"));
                EditorGUILayout.PropertyField(resultSceneNameProp, new GUIContent("Result Scene Name"));
                EditorGUILayout.PropertyField(mainSceneNameProp, new GUIContent("Main Scene Name"));
                break;

            case ButtonType.Submit:
                EditorGUILayout.HelpBox("Button 상단의 onClick 이벤트를 사용하세요.", MessageType.None);
                break;

                // 필요한 경우 다른 Case들도 추가 가능
        }
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Base Button Settings", EditorStyles.boldLabel);

        // 3. 기존 Button의 속성들 (Transition, Navigation, onClick 등)
        base.OnInspectorGUI();

        serializedObject.ApplyModifiedProperties();
    }
}
