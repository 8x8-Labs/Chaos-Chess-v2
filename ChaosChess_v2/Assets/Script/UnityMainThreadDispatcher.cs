using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _queue = new Queue<Action>();

    // ← Awake에서 등록 (생성자 X)
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
                _queue.Dequeue().Invoke();
        }
    }

    public static UnityMainThreadDispatcher Instance()
    {
        // GameObject 생성을 여기서 하지 않음
        if (_instance == null)
            Debug.LogError("[Dispatcher] 씬에 UnityMainThreadDispatcher 오브젝트가 없습니다!");
        return _instance;
    }

    public void Enqueue(Action action)
    {
        lock (_queue) { _queue.Enqueue(action); }
    }
}