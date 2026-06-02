using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프 이름 문자열 → BuffSO 에셋 참조 역조회 레지스트리.
///
/// SaveManager가 버프를 로드할 때 JSON에 저장된 에셋 이름으로
/// 실제 BuffSO 참조를 복원하기 위해 사용한다.
///
/// Resources.Load 방식은 BuffSO 파일들을 Resources 폴더로 이동해야 하는
/// 부작용이 있으므로, 인스펙터 직접 할당 방식을 채택한다.
/// </summary>
public class BuffRegistry : MonoBehaviour
{
    public static BuffRegistry Instance;

    /// <summary>프로젝트 내 모든 BuffSO 에셋을 인스펙터에서 할당한다.</summary>
    [SerializeField] private List<BuffSO> allBuffs = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 에셋 이름으로 BuffSO를 반환한다.
    /// 일치하는 항목이 없으면 null을 반환한다.
    /// </summary>
    public BuffSO GetByName(string buffSOName)
    {
        // ScriptableObject.name은 에셋 파일명과 동일하다
        return allBuffs.Find(b => b != null && b.name == buffSOName);
    }
}
