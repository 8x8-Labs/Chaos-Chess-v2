using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CollectionManager : MonoBehaviour
{
    public static CollectionManager Instance;

    // CloudSaveManager가 인스턴스 없이도 경로를 참조할 수 있도록 static으로 공개한다.
    public static string CollectionSavePath => Path.Combine(Application.persistentDataPath, "collection_save.json");

    private string SavePath => CollectionSavePath;

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

    /// <summary>
    /// 온전한 도감 세이브인지 판정한다. SafeFile이 복구 후보를 고를 때 쓴다.
    /// 잘린 JSON은 여기서 예외를 던지고, SafeFile이 이를 "손상"으로 처리한다.
    /// </summary>
    private static bool IsValidCollectionJson(string json)
        => JsonUtility.FromJson<CollectionSaveData>(json)?.discoveredCardNames != null;

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

            // 쓰기 도중 강제 종료로 도감이 통째로 날아가지 않도록 임시 파일 → rename으로 교체한다.
            if (!SafeFile.WriteAtomic(SavePath, JsonUtility.ToJson(data, true))) return;

            CloudSaveManager.Instance?.RequestUpload();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CollectionManager.Save: {e.Message}");
        }
    }

    /// <summary>
    /// 저장 파일을 다시 읽어 메모리 목록을 교체한다.
    /// 클라우드에서 내려받은 데이터를 로컬에 쓴 직후 CloudSaveManager가 호출한다
    /// (Awake에서 이미 로드한 뒤라 파일만 바꿔서는 반영되지 않기 때문).
    /// </summary>
    public void ReloadFromDisk()
    {
        _discovered.Clear();
        Load();
    }

    private void Load()
    {
        // 본 파일이 손상됐으면 SafeFile이 임시본·백업본으로 자동 폴백한다.
        if (!SafeFile.TryRead(SavePath, IsValidCollectionJson, out string json)) return;

        try
        {
            CollectionSaveData data = JsonUtility.FromJson<CollectionSaveData>(json);
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
