// PopupBase.cs
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class PopupBaseBase : MonoBehaviour
{
    public abstract bool IsClosing { get; protected set; }
    public abstract UniTask CloseAsync();
}

/// <summary>
/// TResult = 팝업이 반환할 결과 타입
/// </summary>
public abstract class PopupBase<TResult> : PopupBaseBase
{
    private Action _closeCallback;

    private UniTaskCompletionSource _showUcs;
    private UniTaskCompletionSource<TResult> _resultUcs;

    protected object[] _args;

    [Header("Optional Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string closeTrigger = "Close";
    [SerializeField] private string openAnimTag = "Open";
    [SerializeField] private string closeAnimTag = "Close";

    [Header("Optional Close Button")]
    [SerializeField] private Button closeBtn;

    public override bool IsClosing { get; protected set; }

    /// <summary>
    /// PopupManager에서만 호출
    /// </summary>
    internal void Internal_Init(object[] args, Action closeCallback)
    {
        _args = args;
        _closeCallback = closeCallback;

        _showUcs = new UniTaskCompletionSource();
        _resultUcs = new UniTaskCompletionSource<TResult>();

        IsClosing = false;
    }

    protected virtual void Awake()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveListener(OnCloseBtnClicked);
            closeBtn.onClick.AddListener(OnCloseBtnClicked);
        }
    }

    private void OnCloseBtnClicked()
    {
        OnClickClose().Forget();
    }

    public UniTask WaitForShowAsync() => _showUcs.Task;
    public UniTask<TResult> WaitForResultAsync() => _resultUcs.Task;

    /// <summary>
    /// 결과를 확정하고 닫는다.
    /// (중복 호출 방지: 이미 결과가 확정/닫힘이면 무시)
    /// </summary>
    protected async UniTask SetResultAndCloseAsync(TResult result)
    {
        // 결과는 "한 번만" 확정
        _resultUcs.TrySetResult(result);

        // 닫기는 "한 번만"
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
        try
        {
            if (animator == null)
            {
                _showUcs.TrySetResult();
                return;
            }

            // 애니메이터가 활성화된 다음 상태로 들어갈 수 있으니 한 프레임 양보가 안정적
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());

            await animator.WaitForTagToCompleteAsync(openAnimTag, layer: 0, this.GetCancellationTokenOnDestroy());
            _showUcs.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            // 파괴/언로드 시 조용히 종료
        }
        catch (Exception)
        {
            // 애니 문제로 show 대기가 영원히 막히는 것 방지
            _showUcs.TrySetResult();
        }
    }

    public override async UniTask CloseAsync()
    {
        if (IsClosing)
            return;

        IsClosing = true;

        try
        {
            if (animator != null && animator.HasParameter(closeTrigger))
            {
                animator.ResetTrigger(closeTrigger);
                animator.SetTrigger(closeTrigger);

                await animator.WaitForTagToCompleteAsync(closeAnimTag, layer: 0, this.GetCancellationTokenOnDestroy());
            }
        }
        catch (OperationCanceledException)
        {
            // 파괴/언로드 시 조용히 종료
        }
        catch (Exception)
        {
            // 애니 대기 실패 시에도 release는 진행
        }

        // 매니저 반환(풀로 회수)
        _closeCallback?.Invoke();
        _closeCallback = null;

        // 결과가 아직 확정되지 않았다면 “기본값”으로 확정 (Close 버튼/ESC 닫기 등)
        _resultUcs.TrySetResult(default);
    }

    /// <summary>
    /// 기본 닫기 동작: default 결과 반환
    /// (OK/Cancel 구조면 override해서 원하는 결과를 SetResultAndCloseAsync로 반환)
    /// </summary>
    public virtual UniTask OnClickClose()
    {
        return SetResultAndCloseAsync(default);
    }

    /// <summary>
    /// 파생 클래스에서 “확정” 버튼 같은 곳에 사용
    /// </summary>
    protected UniTask Complete(TResult result)
    {
        return SetResultAndCloseAsync(result);
    }
}


