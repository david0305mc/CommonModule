// PopupManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PopupManager : SingletonMono<PopupManager>
{
    private const string ResourcesPopupPath = "Popup";

    [Header("Roots")]
    [SerializeField] private Transform popupRoot;
    [SerializeField] private Transform poolRoot; // 비워두면 popupRoot를 재사용

    [Header("Fallback Prefabs (optional)")]
    [SerializeField] private List<GameObject> popupPrefabs;

    // key = typeof(TPopup).Name
    private readonly Dictionary<string, Stack<GameObject>> _pool = new();
    private readonly List<PopupBaseBase> _active = new(); // top = last

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
        if (poolRoot == null) poolRoot = popupRoot;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TryCloseTopPopup().Forget();
    }

    /// <summary>
    /// 팝업 인스턴스를 반환 (결과는 popup.WaitForResultAsync()로 대기)
    /// </summary>
    public UniTask<TPopup> ShowPopup<TPopup, TResult>(params object[] args)
        where TPopup : PopupBase<TResult>
        => ShowPopup<TPopup, TResult>(waitOpenAni: false, args);

    public async UniTask<TPopup> ShowPopup<TPopup, TResult>(bool waitOpenAni, params object[] args)
        where TPopup : PopupBase<TResult>
    {
        if (popupRoot == null)
            throw new Exception("[PopupManager] popupRoot is null.");

        var key = typeof(TPopup).Name;
        var go = Rent(key);

        var popup = go.GetComponent<TPopup>();
        if (popup == null)
            throw new Exception($"[PopupManager] {key} prefab has no component: {typeof(TPopup).Name}");

        // active push
        _active.Add(popup);

        // popup init & show
        popup.Internal_Init(args, closeCallback: () => ReleasePopup(popup));
        popup.Show().Forget();

        if (waitOpenAni)
            await popup.WaitForShowAsync();

        return popup;
    }

    /// <summary>
    /// “팝업 띄우고 결과 await” 한 줄용
    /// </summary>
    public async UniTask<TResult> ShowPopupForResult<TPopup, TResult>(params object[] args)
        where TPopup : PopupBase<TResult>
    {
        var popup = await ShowPopup<TPopup, TResult>(waitOpenAni: false, args);
        return await popup.WaitForResultAsync();
    }

    public async UniTask<TResult> ShowPopupForResult<TPopup, TResult>(bool waitOpenAni, params object[] args)
        where TPopup : PopupBase<TResult>
    {
        var popup = await ShowPopup<TPopup, TResult>(waitOpenAni, args);
        return await popup.WaitForResultAsync();
    }

    private GameObject Rent(string key)
    {
        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pool[key] = stack;
        }

        GameObject instance;
        if (stack.Count > 0)
        {
            instance = stack.Pop();
            instance.transform.SetParent(popupRoot, false);
            instance.transform.SetAsLastSibling();
            instance.SetActive(true);
            return instance;
        }

        var prefab = Resources.Load<GameObject>($"{ResourcesPopupPath}/{key}")
                  ?? popupPrefabs?.Find(p => p != null && p.name == key);

        if (prefab == null)
            throw new Exception($"[PopupManager] Popup prefab '{key}' not found. (Resources/{ResourcesPopupPath}/{key} or popupPrefabs)");

        instance = Instantiate(prefab, popupRoot);
        instance.name = prefab.name; // (Clone) 제거 목적이면 여기서 이름 정리해도 됨
        return instance;
    }

    /// <summary>
    /// Release는 “active 제거 + 풀 반환”을 보장
    /// </summary>
    public void ReleasePopup(PopupBaseBase popup)
    {
        if (popup == null) return;

        // 1) active에서 제거 (top이 아니어도 제거)
        _active.Remove(popup);

        // 2) pool push
        var key = popup.GetType().Name;
        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pool[key] = stack;
        }

        var go = popup.gameObject;
        go.SetActive(false);

        // poolRoot가 따로 있으면 그 밑으로 넣어두는 게 hierarchy 관리에 편함
        var parent = poolRoot != null ? poolRoot : popupRoot;
        popup.transform.SetParent(parent, false);

        stack.Push(go);
    }

    public async UniTaskVoid TryCloseTopPopup()
    {
        if (_active.Count == 0)
            return;

        var top = _active[^1];
        if (top == null)
        {
            _active.RemoveAt(_active.Count - 1);
            return;
        }

        if (top.IsClosing)
            return;

        await top.CloseAsync();
    }

    public bool TryGetTopPopup<TPopup>(out TPopup popup) where TPopup : PopupBaseBase
    {
        popup = null;

        if (_active.Count == 0)
            return false;

        var top = _active[^1];
        if (top is TPopup casted)
        {
            popup = casted;
            return true;
        }
        return false;
    }
}