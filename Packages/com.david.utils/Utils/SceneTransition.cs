using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : SingletonMono<SceneTransition>
{

    [Header("Overlay")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image overlayImage;

    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private float fadeInTime = 1f;


    private CancellationTokenSource transitionCts;

    protected override void Awake()
    {
        base.Awake();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }
    /// <summary>씬 로딩 없이 화면을 가린다. 완료 후에도 FadeIn 호출 전까지 가림과 입력 차단을 유지한다.</summary>
    public void FadeOut() => FadeOutAsync().Forget();

    /// <summary>씬 로딩 없이 화면을 드러낸다. 완료되면 오버레이 입력 차단을 해제한다.</summary>
    public void FadeIn() => FadeInAsync().Forget();

    /// <summary>화면이 완전히 가려질 때까지 기다린다. 진행 중인 페이드/자동 전환은 취소한다.</summary>
    public UniTask FadeOutAsync(CancellationToken externalCt = default)
    {
        return FadeToAsync(1f, fadeOutTime, BeginTransition(externalCt));
    }

    /// <summary>화면이 완전히 드러날 때까지 기다린다. 진행 중인 페이드/자동 전환은 취소한다.</summary>
    public UniTask FadeInAsync(CancellationToken externalCt = default)
    {
        return FadeToAsync(0f, fadeInTime, BeginTransition(externalCt));
    }

    /// <summary>
    /// 페이드 상태를 바꾸지 않고 씬 로딩/활성화만 기다린다. FadeOut/FadeIn 시점은 호출자가 정한다.
    /// 새 씬의 게임별 비동기 초기화는 별도로 기다려야 한다. 취소는 대기만 중단하며 로딩을 되돌리지 않는다.
    /// </summary>
    public async UniTask LoadSceneAsync(string sceneName, CancellationToken externalCt = default)
    {
        using var loadCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalCt, this.GetCancellationTokenOnDestroy());
        var ct = loadCts.Token;
        ct.ThrowIfCancellationRequested();
        var op = SceneManager.LoadSceneAsync(sceneName);
        await op.ToUniTask(cancellationToken: ct);
    }

    /// <summary>
    /// 페이드 아웃과 씬 로딩/활성화를 기다린 뒤 페이드 인을 시작하고 반환한다.
    /// 페이드 인 완료까지 기다리려면 LoadSceneWithFullFadeAsync를 사용한다.
    /// </summary>
    public UniTask LoadSceneWithFadeAsync(string sceneName, CancellationToken externalCt = default)
    {
        return LoadSceneWithFadeCoreAsync(sceneName, false, externalCt);
    }

    /// <summary>
    /// 페이드 아웃, 씬 로딩/활성화, 페이드 인 완료와 오버레이 입력 차단 해제까지 기다린다.
    /// 새 씬의 게임별 비동기 초기화 완료까지 보장하지는 않는다.
    /// </summary>
    public UniTask LoadSceneWithFullFadeAsync(string sceneName, CancellationToken externalCt = default)
    {
        return LoadSceneWithFadeCoreAsync(sceneName, true, externalCt);
    }

    private async UniTask LoadSceneWithFadeCoreAsync(string sceneName, bool waitForFadeIn, CancellationToken externalCt)
    {
        var ct = BeginTransition(externalCt);

        await FadeToAsync(1f, fadeOutTime, ct);

        await LoadSceneAsync(sceneName, ct);

        if (waitForFadeIn)
        {
            await FadeToAsync(0f, fadeInTime, ct);
        }
        else
        {
            FadeToAsync(0f, fadeInTime, ct).Forget();
        }
    }

    private CancellationToken BeginTransition(CancellationToken externalCt)
    {
        externalCt.ThrowIfCancellationRequested();
        transitionCts?.Cancel();
        transitionCts?.Dispose();
        transitionCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt, this.GetCancellationTokenOnDestroy());
        return transitionCts.Token;
    }

    protected override void OnDestroy()
    {
        transitionCts?.Cancel();
        transitionCts?.Dispose();
        transitionCts = null;
        base.OnDestroy();
    }

    private async UniTask FadeToAsync(float targetAlpha, float duration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
            return;
        }

        canvasGroup.blocksRaycasts = true;

        float start = canvasGroup.alpha;
        float t = 0f;

        while (t < duration)
        {
            ct.ThrowIfCancellationRequested();
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / duration);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
    }
}
