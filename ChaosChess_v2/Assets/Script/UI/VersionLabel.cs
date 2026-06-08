using TMPro;
using UnityEngine;

// 화면 구석에 현재 프로젝트 버전(Application.version = PlayerSettings.bundleVersion)을 표시한다.
// 빌드 시 값은 VersionStamp(에디터 빌드 훅)가 Git 태그로 각인한다.
// [ExecuteAlways] 라 에디터에서도 Play 없이 즉시 반영되고, 버전이 바뀌면 자동 갱신된다.
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class VersionLabel : MonoBehaviour
{
    [SerializeField] private string prefix = "v";

    private TMP_Text label;

    private void Awake() => Refresh();
    private void OnEnable() => Refresh();

#if UNITY_EDITOR
    // 인스펙터 값 변경·스크립트 리컴파일·씬 로드 시 에디터에서 갱신
    private void OnValidate() => Refresh();
#endif

    private void Refresh()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();

        if (label == null)
            return;

        string text = $"{prefix}{Application.version}";
        if (label.text != text)
            label.text = text;
    }
}
