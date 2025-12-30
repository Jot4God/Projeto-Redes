using System;
using System.Collections.Concurrent;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private static readonly ConcurrentQueue<Action> _queue = new();

    // ✅ Corre depois da scene carregar (assim encontra o dispatcher da tua scene)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;

        var existing = FindObjectOfType<MainThreadDispatcher>();
        if (existing != null)
        {
            _instance = existing;
            DontDestroyOnLoad(_instance.gameObject);
            return;
        }

        var go = new GameObject("MainThreadDispatcher");
        _instance = go.AddComponent<MainThreadDispatcher>();
        DontDestroyOnLoad(go);
    }

    public static void Post(Action action)
    {
        if (action == null) return;
        EnsureInstance();
        _queue.Enqueue(action);
    }

    private void Awake()
    {
        // ✅ Se já existe um dispatcher, destrói SÓ este componente (não o GameObject)
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        while (_queue.TryDequeue(out var a))
        {
            try { a(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
