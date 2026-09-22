using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PopupManagerTests
{
    private GameObject _root;
    private PopupManager _manager;
    private readonly List<GameObject> _prefabs = new();

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Popup tests");
        var managerObject = new GameObject("Manager");
        managerObject.transform.SetParent(_root.transform);
        _manager = managerObject.AddComponent<PopupManager>();
        var popupRoot = new GameObject("Active popups").transform;
        popupRoot.SetParent(_root.transform);
        SetField("popupRoot", popupRoot);
        SetField("popupPrefabs", _prefabs);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        _prefabs.Clear();
    }

    private void SetField(string name, object value)
        => typeof(PopupManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_manager, value);

    private GameObject AddPrefab<T>(bool active = false) where T : Component
    {
        var prefab = new GameObject(typeof(T).Name);
        prefab.transform.SetParent(_root.transform);
        prefab.SetActive(false);
        prefab.AddComponent<T>();
        prefab.SetActive(active);
        _prefabs.Add(prefab);
        return prefab;
    }

    [UnityTest]
    public IEnumerator ActivePrefabAndReuseReceiveArgsBeforeEnableAndShow() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>(active: true);
        var firstArgs = new TypedPopupProbe.Args { Value = 11 };
        var first = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(firstArgs);
        Assert.That(first.Current, Is.SameAs(firstArgs));
        Assert.That(first.SeenInAwake, Is.SameAs(firstArgs));
        Assert.That(first.SeenInEnable, Is.SameAs(firstArgs));
        Assert.That(first.SeenInShow, Is.SameAs(firstArgs));
        Assert.That(first.ActiveDuringInitialize, Is.False);
        await first.CloseAsync();
        Assert.That(first.Current, Is.Null);
        Assert.That(first.gameObject.activeSelf, Is.False);
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);

        var secondArgs = new TypedPopupProbe.Args { Value = 22 };
        var second = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(secondArgs);
        Assert.That(second, Is.SameAs(first));
        Assert.That(second.Current, Is.SameAs(secondArgs));
        Assert.That(second.SeenInEnable, Is.SameAs(secondArgs));
        Assert.That(second.SeenInShow, Is.SameAs(secondArgs));
        Assert.That(second.InitializeCount, Is.EqualTo(2));
        Assert.That(second.ActiveDuringInitialize, Is.False);
    });

    [UnityTest]
    public IEnumerator ValueTypeAndNullArgumentsAreSupported() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<ValuePopupProbe>();
        var value = await _manager.ShowPopup<ValuePopupProbe, int, bool>(42);
        Assert.That(value.Current, Is.EqualTo(42));
        await value.CloseAsync();
        Assert.That(value.Current, Is.Zero);
        AddPrefab<NullablePopupProbe>();
        var nullable = await _manager.ShowPopup<NullablePopupProbe, string, bool>(null);
        Assert.That(nullable.Current, Is.Null);
    });

    [UnityTest]
    public IEnumerator ResultAndDefaultCloseArePreserved() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var result = _manager.ShowPopupForResult<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(_manager.TryGetTopPopup<TypedPopupProbe>(out var popup), Is.True);
        await popup.Finish(73);
        Assert.That(await result, Is.EqualTo(73));
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        Assert.That(popup.Current, Is.Null);

        var defaultResult = _manager.ShowPopupForResult<TypedPopupProbe, TypedPopupProbe.Args, int>(true, new TypedPopupProbe.Args());
        _manager.TryGetTopPopup<TypedPopupProbe>(out popup);
        await popup.OnClickClose();
        Assert.That(await defaultResult, Is.Zero);
    });

    [UnityTest]
    public IEnumerator WaitOpenAnimationRemainsOptional() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var gate = new UniTaskCompletionSource();
        var showing = _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(true,
            new TypedPopupProbe.Args { OpenGate = gate });
        Assert.That(showing.Status, Is.EqualTo(UniTaskStatus.Pending));
        gate.TrySetResult();
        var popup = await showing;
        await popup.CloseAsync();

        gate = new UniTaskCompletionSource();
        popup = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(false,
            new TypedPopupProbe.Args { OpenGate = gate });
        Assert.That(popup.WaitForShowAsync().Status, Is.EqualTo(UniTaskStatus.Pending));
        gate.TrySetResult();
        await popup.WaitForShowAsync();
    });

    [UnityTest]
    public IEnumerator DuplicateReleaseDoesNotRentSameObjectTwice() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var first = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        _manager.ReleasePopup(first);
        _manager.ReleasePopup(first);
        var second = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        var third = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(second, Is.SameAs(first));
        Assert.That(third, Is.Not.SameAs(second));
        await third.CloseAsync();
        Assert.That(_manager.TryGetTopPopup<TypedPopupProbe>(out var top), Is.True);
        Assert.That(top, Is.SameAs(second));
    });

    [UnityTest]
    public IEnumerator InitializationFailureIsDiscarded() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        await ExpectFailure(_manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { ThrowInitialize = true }));
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        var failed = TypedPopupProbe.LastInitialized;
        Assert.That(failed.Current, Is.Null);
        var next = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(next, Is.Not.SameAs(failed));
        await UniTask.Yield();
        Assert.That(failed == null, Is.True);
    });

    [UnityTest]
    public IEnumerator SynchronousAndAsynchronousShowFailuresAreDiscarded() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        await ExpectFailure(_manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { ThrowShow = true }));
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        var failed = TypedPopupProbe.LastInitialized;
        Assert.That(failed.Current, Is.Null);

        var gate = new UniTaskCompletionSource();
        var pending = _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { ShowGate = gate, ThrowShow = true });
        Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(_manager.TryGetTopPopup<TypedPopupProbe>(out var asyncFailed), Is.True);
        Assert.That(asyncFailed, Is.Not.SameAs(failed));
        gate.TrySetResult();
        await ExpectFailure(pending);
        Assert.That(asyncFailed.Current, Is.Null);
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        var next = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(next, Is.Not.SameAs(asyncFailed));
    });

    [UnityTest]
    public IEnumerator MissingComponentDoesNotLeaveAnActiveOrPooledObject() => UniTask.ToCoroutine(async () =>
    {
        var bad = new GameObject(nameof(TypedPopupProbe));
        bad.transform.SetParent(_root.transform);
        _prefabs.Add(bad);
        await ExpectFailure(_manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args()));
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        _prefabs.Remove(bad);
        AddPrefab<TypedPopupProbe>();
        var next = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(next, Is.Not.Null);
    });

    [UnityTest]
    public IEnumerator OldShowFailureCannotReleaseANewerRental() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var gate = new UniTaskCompletionSource();
        var pending = _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { ShowGate = gate, ThrowShow = true });
        _manager.TryGetTopPopup<TypedPopupProbe>(out var old);
        _manager.ReleasePopup(old);
        var args = new TypedPopupProbe.Args();
        var current = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(args);
        Assert.That(current, Is.SameAs(old));
        gate.TrySetResult();
        await ExpectFailure(pending);
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.True);
        Assert.That(current.Current, Is.SameAs(args));
        Assert.That(current.gameObject.activeSelf, Is.True);
    });

    [UnityTest]
    public IEnumerator ForResultKeepsOriginalResultAcrossReuseDuringShow() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var gate = new UniTaskCompletionSource();
        var pending = _manager.ShowPopupForResult<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { ShowGate = gate });
        _manager.TryGetTopPopup<TypedPopupProbe>(out var old);
        await old.Finish(17);
        var current = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(current, Is.SameAs(old));
        gate.TrySetResult();
        Assert.That(await pending, Is.EqualTo(17));
        Assert.That(current.WaitForResultAsync().Status, Is.EqualTo(UniTaskStatus.Pending));
    });

    [UnityTest]
    public IEnumerator LegacyCallsRetainArgsResultsAndPooling() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<LegacyPopupProbe>(active: true);
        var args = new object[] { "title", 123 };
        var popup = await _manager.ShowPopup<LegacyPopupProbe, int>(true, args);
        Assert.That(popup.Current, Is.SameAs(args));
        Assert.That(popup.SeenInEnable, Is.SameAs(args));
        await popup.CloseAsync();
        Assert.That(popup.Current, Is.Null);
        var result = _manager.ShowPopupForResult<LegacyPopupProbe, int>("next", 456);
        _manager.TryGetTopPopup<LegacyPopupProbe>(out var reused);
        Assert.That(reused, Is.SameAs(popup));
        await reused.Finish(9);
        Assert.That(await result, Is.EqualTo(9));
    });

    [UnityTest]
    public IEnumerator FailureAfterSelfCloseRemovesTheFailedInstanceFromPool() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        await ExpectFailure(_manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { CloseInShow = true, ThrowShow = true }));
        var failed = TypedPopupProbe.LastInitialized;
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        var next = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(next, Is.Not.SameAs(failed));
        Assert.That(next.gameObject.activeSelf, Is.True);
    });

    [UnityTest]
    public IEnumerator ClosingBeforeOpenCompletesCancelsOnlyTheOldWait() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var oldGate = new UniTaskCompletionSource();
        var showing = _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(true,
            new TypedPopupProbe.Args { OpenGate = oldGate });
        _manager.TryGetTopPopup<TypedPopupProbe>(out var old);
        await old.CloseAsync();
        try { await showing; Assert.Fail("Expected canceled open wait."); }
        catch (OperationCanceledException) { }
        var newGate = new UniTaskCompletionSource();
        var current = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { OpenGate = newGate });
        Assert.That(current, Is.SameAs(old));
        oldGate.TrySetResult();
        await UniTask.Yield();
        Assert.That(current.WaitForShowAsync().Status, Is.EqualTo(UniTaskStatus.Pending));
        newGate.TrySetResult();
        await current.WaitForShowAsync();
    });

    [UnityTest]
    public IEnumerator TopCloseReturnsDefaultAndExposesPreviousPopup() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var first = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        var second = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        var result = second.WaitForResultAsync();
        _manager.TryCloseTopPopup().Forget();
        Assert.That(await result, Is.Zero);
        Assert.That(second.Current, Is.Null);
        Assert.That(_manager.TryGetTopPopup<TypedPopupProbe>(out var top), Is.True);
        Assert.That(top, Is.SameAs(first));
    });

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator AnimatorWaitsAndLateCloseCannotCloseNextRental() => UniTask.ToCoroutine(async () =>
    {
        var prefab = AddPrefab<TypedPopupProbe>();
        var animator = prefab.AddComponent<Animator>();
        var controller = new UnityEditor.Animations.AnimatorController();
        controller.AddLayer("Base");
        controller.AddParameter("Close", AnimatorControllerParameterType.Trigger);
        var openClip = new AnimationClip();
        var closeClip = new AnimationClip();
        openClip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 0.1f, 0));
        closeClip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 0.1f, 0));
        var states = controller.layers[0].stateMachine;
        var open = states.AddState("Open");
        open.tag = "Open";
        open.motion = openClip;
        states.defaultState = open;
        var close = states.AddState("Close");
        close.tag = "Close";
        close.motion = closeClip;
        var transition = open.AddTransition(close);
        transition.hasExitTime = false;
        transition.duration = 0;
        transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Close");
        animator.runtimeAnimatorController = controller;
        typeof(PopupBase<int>).GetField("animator", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(prefab.GetComponent<TypedPopupProbe>(), animator);
        try
        {
            var opening = _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(true, new TypedPopupProbe.Args());
            Assert.That(opening.Status, Is.EqualTo(UniTaskStatus.Pending));
            var popup = await opening;
            var closing = popup.CloseAsync();
            Assert.That(closing.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.True);
            await closing;
            Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
            Assert.That(popup.Current, Is.Null);

            popup = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(true, new TypedPopupProbe.Args());
            var oldClosing = popup.CloseAsync();
            _manager.ReleasePopup(popup);
            var args = new TypedPopupProbe.Args();
            var current = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(args);
            Assert.That(current, Is.SameAs(popup));
            await oldClosing;
            Assert.That(current.Current, Is.SameAs(args));
            Assert.That(current.IsClosing, Is.False);
            Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.True);
            await current.WaitForShowAsync();
            await current.CloseAsync();
        }
        finally
        {
            Object.DestroyImmediate(controller);
            Object.DestroyImmediate(openClip);
            Object.DestroyImmediate(closeClip);
        }
    });
#endif

    [UnityTest]
    public IEnumerator ClosingDuringInitializeDoesNotReactivatePooledPopup() => UniTask.ToCoroutine(async () =>
    {
        AddPrefab<TypedPopupProbe>();
        var popup = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(
            new TypedPopupProbe.Args { CloseInInitialize = true });
        Assert.That(popup.gameObject.activeSelf, Is.False);
        Assert.That(popup.Current, Is.Null);
        Assert.That(popup.SeenInShow, Is.Null);
        Assert.That(_manager.IsOnStack<TypedPopupProbe>(), Is.False);
        var next = await _manager.ShowPopup<TypedPopupProbe, TypedPopupProbe.Args, int>(new TypedPopupProbe.Args());
        Assert.That(next, Is.SameAs(popup));
        Assert.That(next.gameObject.activeSelf, Is.True);
    });

    [Test]
    public void TypedApiRejectsMismatchedArgsAndResultsInGenericConstraints()
    {
        // Reflection enforces the same generic constraints as C# compilation.
        foreach (var method in typeof(PopupManager).GetMethods())
        {
            if ((method.Name != "ShowPopup" && method.Name != "ShowPopupForResult") ||
                method.GetGenericArguments().Length != 3) continue;
            Assert.DoesNotThrow(() => method.MakeGenericMethod(typeof(TypedPopupProbe), typeof(TypedPopupProbe.Args), typeof(int)));
            Assert.Throws<ArgumentException>(() => method.MakeGenericMethod(typeof(TypedPopupProbe), typeof(string), typeof(int)));
            Assert.Throws<ArgumentException>(() => method.MakeGenericMethod(typeof(TypedPopupProbe), typeof(TypedPopupProbe.Args), typeof(bool)));
        }
    }

    private static async UniTask ExpectFailure<T>(UniTask<T> task)
    {
        try { await task; }
        catch (InvalidOperationException) { return; }
        Assert.Fail("Expected InvalidOperationException.");
    }
}

public sealed class TypedPopupProbe : PopupBase<TypedPopupProbe.Args, int>
{
    public sealed class Args
    {
        public int Value;
        public bool ThrowInitialize;
        public bool CloseInInitialize;
        public bool ThrowShow;
        public bool CloseInShow;
        public UniTaskCompletionSource OpenGate;
        public UniTaskCompletionSource ShowGate;
    }

    public static TypedPopupProbe LastInitialized;
    public Args Current => Arguments;
    public Args SeenInAwake;
    public Args SeenInEnable;
    public Args SeenInShow;
    public bool ActiveDuringInitialize;
    public int InitializeCount;

    protected override void OnInitialize(Args args)
    {
        LastInitialized = this;
        InitializeCount++;
        ActiveDuringInitialize = gameObject.activeInHierarchy;
        if (args.ThrowInitialize) throw new InvalidOperationException("Initialize failure");
        if (args.CloseInInitialize) CloseAsync().Forget();
    }

    protected override void Awake()
    {
        base.Awake();
        SeenInAwake = Arguments;
    }

    private void OnEnable() => SeenInEnable = Arguments;

    public override async UniTask Show()
    {
        var args = Arguments;
        SeenInShow = args;
        await base.Show();
        if (args.ShowGate != null) await args.ShowGate.Task;
        if (args.CloseInShow) await CloseAsync();
        if (args.ThrowShow) throw new InvalidOperationException("Show failure");
    }

    protected override async UniTask WaitShowAni()
    {
        if (Arguments.OpenGate != null)
            await Arguments.OpenGate.Task.AttachExternalCancellation(PopupCancellationToken);
        await base.WaitShowAni();
    }

    public UniTask Finish(int result) => Complete(result);
}

public sealed class ValuePopupProbe : PopupBase<int, bool>
{
    public int Current => Arguments;
}

public sealed class NullablePopupProbe : PopupBase<string, bool>
{
    public string Current => Arguments;
}

public sealed class LegacyPopupProbe : PopupBase<int>
{
    public object[] Current => _args;
    public object[] SeenInEnable;
    private void OnEnable() => SeenInEnable = _args;
    public UniTask Finish(int result) => Complete(result);
}
