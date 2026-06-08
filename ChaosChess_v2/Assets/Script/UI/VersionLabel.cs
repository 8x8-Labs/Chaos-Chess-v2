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

        // 런타임(빌드)은 빌드 시 각인된 Application.version을 쓴다.
        // 에디터는 빌드 훅이 돌지 않아 bundleVersion이 옛 값에 머무르므로,
        // git 태그를 직접 읽어 표시한다(표시용일 뿐 bundleVersion은 건드리지 않음).
        string version = Application.version;
#if UNITY_EDITOR
        string gitVersion = GitVersion.Resolve();
        if (!string.IsNullOrEmpty(gitVersion))
        {
            // 에디터는 작업 중이라 거의 항상 dirty/커밋앞섬 상태다.
            // 빌드 메타데이터('+' 뒤: +dirty, +2.g1a2b3c 등)는 표시 노이즈이므로 떼어내
            // 태그 버전(프리릴리스 -rc.1 등은 '+' 앞이라 보존)만 보여준다.
            int meta = gitVersion.IndexOf('+');
            version = meta >= 0 ? gitVersion.Substring(0, meta) : gitVersion;
        }
#endif

        string text = $"{prefix}{version}";
        if (label.text != text)
            label.text = text;
    }
}
