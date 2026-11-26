using System;
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
        transitionCts?.Cancel();
        transitionCts?.Dispose();
        transitionCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt, this.GetCancellationTokenOnDestroy());
        var ct = transitionCts.Token;

        await FadeToAsync(1f, fadeOutTime, ct);

        var op = SceneManager.LoadSceneAsync(sceneName);
        await op.ToUniTask(cancellationToken: ct);

        if (waitForFadeIn)
        {
            await FadeToAsync(0f, fadeInTime, ct);
        }
        else
        {
            FadeToAsync(0f, fadeInTime, ct).Forget();
        }
    }

    private async UniTask FadeToAsync(float targetAlpha, float duration, CancellationToken ct)
    {
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
