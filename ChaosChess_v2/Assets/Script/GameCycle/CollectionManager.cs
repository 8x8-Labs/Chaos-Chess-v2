using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CollectionManager : MonoBehaviour
{
    public static CollectionManager Instance;

    private string SavePath => Path.Combine(Application.persistentDataPath, "collection_save.json");

    private readonly HashSet<string> _discovered = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool IsDiscovered(string cardName) => _discovered.Contains(cardName);

    public void Discover(string cardName)
    {
        if (_discovered.Add(cardName))
            Save();
    }

    private void Save()
    {
        try
        {
            CollectionSaveData data = new CollectionSaveData();
            data.discoveredCardNames.AddRange(_discovered);
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CollectionManager.Save: {e.Message}");
        }
    }

    private void Load()
    {
        if (!File.Exists(SavePath)) return;

        try
        {
            CollectionSaveData data = JsonUtility.FromJson<CollectionSaveData>(File.ReadAllText(SavePath));
            if (data?.discoveredCardNames == null) return;

            foreach (string name in data.discoveredCardNames)
                _discovered.Add(name);

            Debug.Log($"CollectionManager: 로드된 카드 목록 ({_discovered.Count}개) - {string.Join(", ", _discovered)}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CollectionManager.Load: {e.Message}");
        }
    }
}
