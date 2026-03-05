
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class AnimatorExtensions
{
    public static bool HasParameter(this Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName))
            return false;

        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 특정 Tag를 가진 state에 "진입"한 뒤, 그 state가 끝날 때까지 대기.
    /// - transition 중일 때는 체크를 건너뜀
    /// - cancellationToken을 받도록 해서 씬 언로드/파괴 시 멈춤
    /// </summary>
    public static async UniTask WaitForTagToCompleteAsync(
        this Animator animator,
        string tag,
        int layer,
        CancellationToken cancellationToken)
    {
        if (animator == null)
            return;

        // tag state 진입 대기
        await UniTask.WaitUntil(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (animator.IsInTransition(layer)) return false;

            var state = animator.GetCurrentAnimatorStateInfo(layer);
            return state.IsTag(tag);
        }, PlayerLoopTiming.Update, cancellationToken);

        // state 끝날 때까지 대기
        await UniTask.WaitUntil(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (animator.IsInTransition(layer)) return false;

            var state = animator.GetCurrentAnimatorStateInfo(layer);
            return state.IsTag(tag) && state.normalizedTime >= 1f;
        }, PlayerLoopTiming.Update, cancellationToken);
    }
}