using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _queue = new Queue<Action>();

    // 백그라운드 스레드에서 호출해도 안전하게 null 체크만 함
    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            throw new Exception(
                "UnityMainThreadDispatcher가 씬에 없습니다. " +
                "씬에 GameObject를 만들고 컴포넌트를 추가해 주세요.");
        }
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Enqueue(Action action)
    {
        lock (_queue) { _queue.Enqueue(action); }
    }

    void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
                _queue.Dequeue()?.Invoke();
        }
    }
}