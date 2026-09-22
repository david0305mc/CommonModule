using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class PopupBaseBase : MonoBehaviour
{
    public abstract bool IsClosing { get; protected set; }
    public abstract UniTask CloseAsync();

    internal virtual void Internal_Release() { }
}

/// <summary>Legacy popup API. TResult is the result type.</summary>
public abstract class PopupBase<TResult> : PopupBaseBase
{
    private Action _closeCallback;
    private UniTaskCompletionSource _showUcs;
    private UniTaskCompletionSource<TResult> _resultUcs;
    private CancellationTokenSource _lifetime;

    protected object[] _args;

    /// <summary>Cancelled when this rental ends (including a failed Show).</summary>
    protected CancellationToken PopupCancellationToken => _lifetime?.Token ?? new CancellationToken(true);

    [Header("Optional Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string closeTrigger = "Close";
    [SerializeField] private string openAnimTag = "Open";
    [SerializeField] private string closeAnimTag = "Close";

    [Header("Optional Close Button")]
    [SerializeField] private Button closeBtn;

    public override bool IsClosing { get; protected set; }

    internal virtual void Internal_Init(object[] args, Action closeCallback)
    {
        _args = args;
        _closeCallback = closeCallback;
        _showUcs = new UniTaskCompletionSource();
        _resultUcs = new UniTaskCompletionSource<TResult>();
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        IsClosing = false;
    }

    internal override void Internal_Release()
    {
        var lifetime = _lifetime;
        var show = _showUcs;
        var result = _resultUcs;
        _lifetime = null;
        _closeCallback = null;
        _args = null;
        IsClosing = true;

        try
        {
            lifetime?.Cancel();
        }
        finally
        {
            lifetime?.Dispose();
            show?.TrySetCanceled();
            result?.TrySetResult(default);
        }
    }

    protected virtual void Awake()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveListener(OnCloseBtnClicked);
            closeBtn.onClick.AddListener(OnCloseBtnClicked);
        }
    }

    private void OnCloseBtnClicked() => OnClickClose().Forget();

    public UniTask WaitForShowAsync() => _showUcs.Task;
    public UniTask<TResult> WaitForResultAsync() => _resultUcs.Task;

    protected async UniTask SetResultAndCloseAsync(TResult result)
    {
        if (IsClosing) return;

        var session = _resultUcs;
        session.TrySetResult(result);
        // A result continuation can release and rent this popup again synchronously.
        if (ReferenceEquals(session, _resultUcs))
            await CloseAsync();
    }

    public virtual UniTask Show()
    {
        IsClosing = false;
        gameObject.SetActive(true);
        WaitShowAni().Forget();
        return UniTask.CompletedTask;
    }

    protected virtual async UniTask WaitShowAni()
    {
        var show = _showUcs;
        var cancellationToken = PopupCancellationToken;
        try
        {
            if (animator == null)
            {
                show.TrySetResult();
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            await animator.WaitForTagToCompleteAsync(openAnimTag, layer: 0, cancellationToken);
            show.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            show.TrySetCanceled(cancellationToken);
        }
        catch (Exception)
        {
            // An animation failure must not leave callers waiting forever.
            show.TrySetResult();
        }
    }

    public override async UniTask CloseAsync()
    {
        if (IsClosing) return;

        IsClosing = true;
        var result = _resultUcs;
        var closeCallback = _closeCallback;
        var cancellationToken = PopupCancellationToken;
        try
        {
            if (animator != null && animator.HasParameter(closeTrigger))
            {
                animator.ResetTrigger(closeTrigger);
                animator.SetTrigger(closeTrigger);
                await animator.WaitForTagToCompleteAsync(closeAnimTag, layer: 0, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Release even if the close animation fails.
        }

        try
        {
            // A delayed close must never release a later rental of the same object.
            if (ReferenceEquals(result, _resultUcs))
            {
                _closeCallback = null;
                closeCallback?.Invoke();
            }
        }
        finally
        {
            // Close button / ESC uses the default result.
            result.TrySetResult(default);
        }
    }

    public virtual UniTask OnClickClose() => SetResultAndCloseAsync(default);

    protected UniTask Complete(TResult result) => SetResultAndCloseAsync(result);
}

/// <summary>A popup with compile-time checked input and result types.</summary>
public abstract class PopupBase<TArgs, TResult> : PopupBase<TResult>
{
    // Named Arguments so a concrete popup can declare its own nested Args type.
    protected TArgs Arguments { get; private set; }

    internal void Internal_InitTyped(TArgs args, Action closeCallback)
    {
        base.Internal_Init(null, closeCallback);
        Arguments = args;
        OnInitialize(args);
    }

    /// <summary>Runs on every rental, while inactive and before activation/Show.</summary>
    protected virtual void OnInitialize(TArgs args) { }

    internal sealed override void Internal_Init(object[] args, Action closeCallback)
    {
        throw new InvalidOperationException("Use ShowPopup<TPopup, TArgs, TResult> for a typed popup.");
    }

    internal sealed override void Internal_Release()
    {
        Arguments = default;
        base.Internal_Release();
    }
}
