using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SkillPrefabCreator
{
    private const string CardPrefabPath = "Assets/Prefab/Skill/Card.prefab";
    private const string SkillScriptsPath = "Assets/Script/Card/Skills";
    private const string DataSOPath = "Assets/Script/Card/SO";
    private const string OutputPath = "Assets/Prefab/Skill";

    [MenuItem("Tools/Create Skill Prefabs")]
    public static void CreateAllSkillPrefabs()
    {
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefab == null)
        {
            Debug.LogError($"[SkillPrefabCreator] Card.prefab을 찾을 수 없습니다: {CardPrefabPath}");
            return;
        }

        // CardDataSO 에셋 전체 로드 (이름 → 에셋 딕셔너리)
        var soAssets = new Dictionary<string, CardDataSO>(StringComparer.OrdinalIgnoreCase);
        foreach (string soGuid in AssetDatabase.FindAssets("t:CardDataSO", new[] { DataSOPath }))
        {
            string soAssetPath = AssetDatabase.GUIDToAssetPath(soGuid);
            string soName = Path.GetFileNameWithoutExtension(soAssetPath);
            CardDataSO so = AssetDatabase.LoadAssetAtPath<CardDataSO>(soAssetPath);
            if (so != null)
                soAssets[soName] = so;
        }

        // 유효한 스킬 타입 수집
        var skillTypes = new List<Type>();
        var validTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { SkillScriptsPath }))
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null) continue;

            Type type = script.GetClass();
            if (type == null || type.IsAbstract || type.IsInterface) continue;
            if (!typeof(ICard).IsAssignableFrom(type)) continue;
            if (!typeof(CardData).IsAssignableFrom(type)) continue;

            skillTypes.Add(type);
            validTypeNames.Add(type.Name);
        }

        // Step 1: 대응하는 스크립트가 없는 고아 프리팹 삭제
        DeleteOrphanedPrefabs(validTypeNames);

        // Step 2: 각 스킬 타입에 대해 프리팹 생성 또는 업데이트
        int created = 0, updated = 0, noDataSO = 0;

        foreach (Type type in skillTypes)
        {
            string prefabPath = $"{OutputPath}/{type.Name}.prefab";
            bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;

            // 문제가 있는 프리팹은 삭제 후 재생성
            if (existed && IsProblematic(prefabPath, type))
            {
                AssetDatabase.DeleteAsset(prefabPath);
                Debug.LogWarning($"[SkillPrefabCreator] 문제 프리팹 삭제 후 재생성: {type.Name}.prefab");
                existed = false;
            }

            // Card.prefab 인스턴스 생성 (Prefab Variant로 저장됨)
            GameObject instance = PrefabUtility.InstantiatePrefab(cardPrefab) as GameObject;
            if (instance == null)
            {
                Debug.LogWarning($"[SkillPrefabCreator] 인스턴스 생성 실패: {type.Name}");
                continue;
            }

            instance.name = type.Name;
            instance.AddComponent(type);

            // 매칭되는 DataSO 할당
            CardDataSO dataSO = FindMatchingDataSO(soAssets, type.Name);
            if (dataSO != null)
                instance.GetComponent<CardData>().DataSO = dataSO;
            else
            {
                Debug.LogWarning($"[SkillPrefabCreator] DataSO 없음: {type.Name}");
                noDataSO++;
            }

            // 기존 경로에 저장하면 GUID를 유지하며 덮어씀
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            if (existed)
            {
                Debug.Log($"[SkillPrefabCreator] 업데이트: {type.Name}.prefab" +
                          (dataSO != null ? $"  ← {dataSO.name}.asset" : "  (DataSO 미할당)"));
                updated++;
            }
            else
            {
                Debug.Log($"[SkillPrefabCreator] 생성: {type.Name}.prefab" +
                          (dataSO != null ? $"  ← {dataSO.name}.asset" : "  (DataSO 미할당)"));
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SkillPrefabCreator] 완료 — 생성: {created}개, 업데이트: {updated}개, DataSO 미할당: {noDataSO}개");
    }

    /// <summary>
    /// 출력 폴더에서 대응하는 스크립트 클래스가 없는 고아 프리팹을 삭제합니다.
    /// </summary>
    private static void DeleteOrphanedPrefabs(HashSet<string> validTypeNames)
    {
        foreach (string pguid in AssetDatabase.FindAssets("t:Prefab", new[] { OutputPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(pguid);
            string prefabName = Path.GetFileNameWithoutExtension(path);

            if (prefabName == "Card") continue; // 베이스 프리팹 보호

            if (!validTypeNames.Contains(prefabName))
            {
                AssetDatabase.DeleteAsset(path);
                Debug.LogWarning($"[SkillPrefabCreator] 고아 프리팹 삭제: {prefabName}.prefab");
            }
        }
    }

    /// <summary>
    /// 프리팹에 누락된 스크립트가 있거나 예상 컴포넌트가 없으면 true를 반환합니다.
    /// </summary>
    private static bool IsProblematic(string prefabPath, Type expectedType)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return true;

        // 누락된 스크립트(Missing Script) 감지
        foreach (Component c in prefab.GetComponents<Component>())
            if (c == null) return true;

        // 예상 스킬 컴포넌트가 없는 경우
        if (prefab.GetComponent(expectedType) == null) return true;

        return false;
    }

    [MenuItem("Tools/Populate Card Randomizer")]
    public static void PopulateCardRandomizer()
    {
        CardRandomizer[] randomizers = UnityEngine.Object.FindObjectsOfType<CardRandomizer>();
        if (randomizers.Length == 0)
        {
            Debug.LogError("[SkillPrefabCreator] 씬에서 CardRandomizer를 찾을 수 없습니다.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { OutputPath });
        var prefabs = new List<GameObject>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == "Card") continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) prefabs.Add(prefab);
        }

        foreach (CardRandomizer randomizer in randomizers)
        {
            SerializedObject so = new SerializedObject(randomizer);
            SerializedProperty prop = so.FindProperty("cardPrefabs");
            prop.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(randomizer.gameObject.scene);
        }

        Debug.Log($"[SkillPrefabCreator] CardRandomizer에 {prefabs.Count}개의 카드 프리팹을 할당했습니다. 씬을 저장해주세요.");
    }

    /// <summary>
    /// 클래스명으로 대응하는 CardDataSO 에셋을 퍼지 매칭으로 찾습니다.
    /// 1. 정확한 이름 일치 (대소문자 무시)
    /// 2. 클래스명이 에셋명을 포함하거나, 에셋명이 클래스명을 포함
    /// </summary>
    private static CardDataSO FindMatchingDataSO(Dictionary<string, CardDataSO> soAssets, string className)
    {
        if (soAssets.TryGetValue(className, out CardDataSO exact))
            return exact;

        string classLower = className.ToLower();
        foreach (var kvp in soAssets)
        {
            string assetLower = kvp.Key.ToLower();
            if (classLower.Contains(assetLower) || assetLower.Contains(classLower))
                return kvp.Value;
        }

        return null;
    }
}
