using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CardLabRegistrySO 인스펙터에 카드 프리팹 자동 스캔 버튼을 추가합니다.
/// "프리팹 스캔"을 누르면 지정 폴더의 프리팹 중 CardData가 부착된 것을 모두 수집합니다.
/// </summary>
[CustomEditor(typeof(CardLabRegistrySO))]
public class CardLabRegistryEditor : Editor
{
    private const string CardPrefabFolder = "Assets/Prefab/Skill";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        CardLabRegistrySO registry = (CardLabRegistrySO)target;
        EditorGUILayout.LabelField($"등록된 카드: {registry.Count}개", EditorStyles.boldLabel);

        if (GUILayout.Button($"프리팹 스캔 ({CardPrefabFolder})"))
        {
            ScanPrefabs(registry);
        }

        if (GUILayout.Button("목록 비우기"))
        {
            Undo.RecordObject(registry, "Clear Card Lab Registry");
            registry.Cards.Clear();
            EditorUtility.SetDirty(registry);
        }
    }

    private void ScanPrefabs(CardLabRegistrySO registry)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CardPrefabFolder });
        List<CardData> found = new List<CardData>();
        HashSet<CardData> seen = new HashSet<CardData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            CardData card = prefab.GetComponent<CardData>();
            if (card == null || !seen.Add(card))
                continue;

            found.Add(card);
        }

        found.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        Undo.RecordObject(registry, "Scan Card Lab Registry");
        registry.Cards = found;
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssetIfDirty(registry);

        Debug.Log($"[CardLabRegistry] {found.Count}개 카드 프리팹을 등록했습니다. ({CardPrefabFolder})");
    }
}
