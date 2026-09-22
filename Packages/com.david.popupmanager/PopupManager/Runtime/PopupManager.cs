using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class PopupManager : SingletonMono<PopupManager>
{
    private const string ResourcesPopupPath = "Popup";
    [Header("Input")]
    [SerializeField] private InputActionReference cancelAction;

    [Header("Roots")]
    [SerializeField] private Transform popupRoot;
    [SerializeField] private Transform poolRoot;

    [Header("Fallback Prefabs (optional)")]
    [SerializeField] private List<GameObject> popupPrefabs;

    private readonly Dictionary<string, Stack<GameObject>> _pool = new();
    private readonly List<PopupBaseBase> _active = new();
    // Keep the last lease while pooled too, so a late failure can discard only its own rental.
    private readonly Dictionary<PopupBaseBase, object> _leases = new();
    private Transform _creationRoot;

    public bool IsOnStack<TPopup>() where TPopup : PopupBaseBase
        => _active.Any(p => p is TPopup);

    protected override void Awake()
    {
        base.Awake();
        InitSingleton();
    }

    public void InitSingleton()
    {
        _pool.Clear();
        _active.Clear();
        _leases.Clear();
        if (poolRoot == null) poolRoot = popupRoot;
    }

    void OnEnable()
    {
        if (cancelAction == null) return;
        cancelAction.action.performed += OnCancelPerformed;
        cancelAction.action.Enable();
    }

    void OnDisable()
    {
        if (cancelAction == null) return;
        cancelAction.action.performed -= OnCancelPerformed;
        cancelAction.action.Disable();
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
        => TryCloseTopPopup().Forget();

    public UniTask<TPopup> ShowPopup<TPopup, TResult>(params object[] args)
        where TPopup : PopupBase<TResult>
        => ShowPopup<TPopup, TResult>(waitOpenAni: false, args);

    public async UniTask<TPopup> ShowPopup<TPopup, TResult>(bool waitOpenAni, params object[] args)
        where TPopup : PopupBase<TResult>
    {
        var session = await ShowPopupCore<TPopup, TResult>(waitOpenAni,
            (popup, close) => popup.Internal_Init(args, close));
        return session.Popup;
    }

    public UniTask<TPopup> ShowPopup<TPopup, TArgs, TResult>(TArgs args)
        where TPopup : PopupBase<TArgs, TResult>
        => ShowPopup<TPopup, TArgs, TResult>(waitOpenAni: false, args);

    public async UniTask<TPopup> ShowPopup<TPopup, TArgs, TResult>(bool waitOpenAni, TArgs args)
        where TPopup : PopupBase<TArgs, TResult>
    {
        var session = await ShowPopupCore<TPopup, TResult>(waitOpenAni,
            (popup, close) => popup.Internal_InitTyped(args, close));
        return session.Popup;
    }

    public UniTask<TResult> ShowPopupForResult<TPopup, TResult>(params object[] args)
        where TPopup : PopupBase<TResult>
        => ShowPopupForResult<TPopup, TResult>(waitOpenAni: false, args);

    public async UniTask<TResult> ShowPopupForResult<TPopup, TResult>(bool waitOpenAni, params object[] args)
        where TPopup : PopupBase<TResult>
    {
        var session = await ShowPopupCore<TPopup, TResult>(waitOpenAni,
            (popup, close) => popup.Internal_Init(args, close));
        return await session.Result;
    }

    public UniTask<TResult> ShowPopupForResult<TPopup, TArgs, TResult>(TArgs args)
        where TPopup : PopupBase<TArgs, TResult>
        => ShowPopupForResult<TPopup, TArgs, TResult>(waitOpenAni: false, args);

    public async UniTask<TResult> ShowPopupForResult<TPopup, TArgs, TResult>(bool waitOpenAni, TArgs args)
        where TPopup : PopupBase<TArgs, TResult>
    {
        var session = await ShowPopupCore<TPopup, TResult>(waitOpenAni,
            (popup, close) => popup.Internal_InitTyped(args, close));
        return await session.Result;
    }

    private async UniTask<(TPopup Popup, UniTask<TResult> Result)> ShowPopupCore<TPopup, TResult>(
        bool waitOpenAni, Action<TPopup, Action> initialize)
        where TPopup : PopupBase<TResult>
    {
        if (popupRoot == null)
            throw new InvalidOperationException("[PopupManager] popupRoot is null.");

        var key = typeof(TPopup).Name;
        var go = Rent(key);
        TPopup popup = null;
        var lease = new object();
        try
        {
            popup = go.GetComponent<TPopup>();
            if (popup == null)
                throw new InvalidOperationException($"[PopupManager] {key} prefab has no component: {typeof(TPopup).Name}");

            _active.Add(popup);
            _leases[popup] = lease;
            initialize(popup, () =>
            {
                if (OwnsLease(popup, lease)) ReleasePopup(popup);
            });

            // Capture this rental's tasks before activation or Show can complete/reuse it.
            var show = popup.WaitForShowAsync();
            var result = popup.WaitForResultAsync();
            if (OwnsLease(popup, lease) && _active.Contains(popup))
            {
                go.SetActive(true);
                if (OwnsLease(popup, lease) && _active.Contains(popup))
                    await popup.Show();
            }
            if (waitOpenAni)
                await show;

            return (popup, result);
        }
        catch (OperationCanceledException) when (!ReferenceEquals(popup, null) &&
            (!OwnsLease(popup, lease) || !_active.Contains(popup)))
        {
            // Returning a popup cancels its unfinished open/show wait normally.
            throw;
        }
        catch
        {
            if (ReferenceEquals(popup, null))
                Destroy(go);
            else if (OwnsLease(popup, lease))
            {
                // Failed initialization/Show may leave subclass state partially initialized.
                // Discard this instance instead of contaminating the pool.
                _leases.Remove(popup);
                _active.Remove(popup);
                if (_pool.TryGetValue(key, out var stack) && stack.Contains(go))
                    _pool[key] = new Stack<GameObject>(stack.Where(item => item != go).Reverse());
                try
                {
                    if (go != null) go.SetActive(false);
                    popup.Internal_Release();
                }
                finally
                {
                    Destroy(go);
                }
            }
            throw;
        }
    }

    private bool OwnsLease(PopupBaseBase popup, object lease)
        => _leases.TryGetValue(popup, out var current) && ReferenceEquals(current, lease);

    private GameObject Rent(string key)
    {
        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pool[key] = stack;
        }

        GameObject instance = null;
        // Unity objects can have been destroyed externally while pooled.
        while (stack.Count > 0 && instance == null)
            instance = stack.Pop();

        try
        {
            if (instance == null)
            {
                var prefab = Resources.Load<GameObject>($"{ResourcesPopupPath}/{key}")
                          ?? popupPrefabs?.Find(p => p != null && p.name == key);
                if (prefab == null)
                    throw new InvalidOperationException($"[PopupManager] Popup prefab '{key}' not found. (Resources/{ResourcesPopupPath}/{key} or popupPrefabs)");

                // Instantiating an active prefab under popupRoot would run OnEnable before Init.
                if (_creationRoot == null)
                {
                    var root = new GameObject("Popup Creation (Inactive)");
                    root.SetActive(false);
                    root.transform.SetParent(transform, false);
                    _creationRoot = root.transform;
                }
                instance = Instantiate(prefab, _creationRoot);
                instance.name = prefab.name;
            }

            instance.SetActive(false);
            instance.transform.SetParent(popupRoot, false);
            instance.transform.SetAsLastSibling();
            return instance;
        }
        catch
        {
            if (instance != null) Destroy(instance);
            throw;
        }
    }

    /// <summary>Remove an active rental and return it to the pool exactly once.</summary>
    public void ReleasePopup(PopupBaseBase popup)
    {
        if (popup == null || !_leases.ContainsKey(popup) || !_active.Remove(popup)) return;

        var lease = _leases[popup];
        var go = popup.gameObject;
        try
        {
            go.SetActive(false);
            popup.Internal_Release();
            if (!OwnsLease(popup, lease)) return;
            var parent = poolRoot != null ? poolRoot : popupRoot;
            popup.transform.SetParent(parent, false);

            var key = popup.GetType().Name;
            if (!_pool.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>();
                _pool[key] = stack;
            }
            stack.Push(go);
        }
        catch
        {
            if (OwnsLease(popup, lease)) _leases.Remove(popup);
            Destroy(go);
            throw;
        }
    }

    public async UniTaskVoid TryCloseTopPopup()
    {
        if (_active.Count == 0) return;
        var top = _active[^1];
        if (top == null)
        {
            _active.RemoveAt(_active.Count - 1);
            if (!ReferenceEquals(top, null)) _leases.Remove(top);
            return;
        }
        if (!top.IsClosing) await top.CloseAsync();
    }

    public bool TryGetTopPopup<TPopup>(out TPopup popup) where TPopup : PopupBaseBase
    {
        popup = null;
        if (_active.Count == 0) return false;
        if (_active[^1] is TPopup casted)
        {
            popup = casted;
            return true;
        }
        return false;
    }
}
