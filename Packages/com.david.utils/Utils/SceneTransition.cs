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
    /// 페이드 포함 씬 전환 (await 가능)
    /// </summary>
    public async UniTask LoadSceneWithFadeAsync(string sceneName, CancellationToken externalCt = default)
    {
        transitionCts?.Cancel();
        transitionCts?.Dispose();
        transitionCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt, this.GetCancellationTokenOnDestroy());
        var ct = transitionCts.Token;

        await FadeToAsync(1f, fadeOutTime, ct);

        var op = SceneManager.LoadSceneAsync(sceneName);
        await op.ToUniTask(cancellationToken: ct);

        // 여기서 "완전히 열림"까지 보장하고 싶으면 await
        FadeToAsync(0f, fadeInTime, ct).Forget();
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
