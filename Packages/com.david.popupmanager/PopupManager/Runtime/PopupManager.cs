using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : SingletonMono<PopupManager>
{
    private const string ResourcesPopupPath = "Popup";

    [SerializeField] private Transform popupRoot;
    [SerializeField] private List<GameObject> popupPrefabs;

    private Dictionary<string, Queue<GameObject>> pool;
    private Stack<PopupBaseBase> activePopups;

    public bool IsOnStack<TPopup>() where TPopup : PopupBaseBase
        => activePopups != null && activePopups.Any(p => p is TPopup);

    protected override void Awake()
    {
        base.Awake();
        InitSingleton();
    }

    public void InitSingleton()
    {
        pool = new Dictionary<string, Queue<GameObject>>();
        activePopups = new Stack<PopupBaseBase>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TryCloseTopPopup().Forget();
    }

    public async UniTask<TResult> ShowPopupAsync<TPopup, TResult>(params object[] args)
        where TPopup : PopupBase<TResult>
    {
        string key = typeof(TPopup).Name;

        if (!pool.TryGetValue(key, out var queue))
        {
            queue = new Queue<GameObject>();
            pool[key] = queue;
        }

        GameObject instance;
        if (queue.Count > 0)
        {
            instance = queue.Dequeue();
            instance.transform.SetParent(popupRoot, false);
            instance.transform.SetAsLastSibling();
            instance.SetActive(true);
        }
        else
        {
            var prefab = Resources.Load<GameObject>($"{ResourcesPopupPath}/{key}")
                      ?? popupPrefabs.Find(p => p.name == key);

            if (prefab == null)
                throw new Exception($"Popup prefab {key} not found");

            instance = Instantiate(prefab, popupRoot);
        }

        var popup = instance.GetComponent<TPopup>();
        activePopups.Push(popup);

        // WaitForResultAsync 내부에서 Close/Release가 발생할 수 있으니,
        // 결과 반환 후에도 스택/풀 정합성이 유지되도록 Release 경로를 일원화하는 게 안전합니다.
        return await popup.WaitForResultAsync(args);
    }

    // ✅ Release는 "스택에서 제거 + 풀 반환"을 보장
    public void ReleasePopup(PopupBaseBase popup)
    {
        if (popup == null) return;

        // 1) activePopups에서 제거 (top이 아니어도 제거되도록 재구성)
        if (activePopups.Count > 0)
        {
            if (ReferenceEquals(activePopups.Peek(), popup))
            {
                activePopups.Pop();
            }
            else
            {
                // top이 아닌 팝업이 먼저 닫히는 케이스 방어
                var temp = new Stack<PopupBaseBase>(activePopups.Count);
                while (activePopups.Count > 0)
                {
                    var p = activePopups.Pop();
                    if (!ReferenceEquals(p, popup))
                        temp.Push(p);
                }
                while (temp.Count > 0)
                    activePopups.Push(temp.Pop());
            }
        }

        // 2) pool enqueue
        string key = popup.GetType().Name;
        if (!pool.TryGetValue(key, out var q))
        {
            q = new Queue<GameObject>();
            pool[key] = q;
        }

        popup.transform.SetParent(popupRoot, false); // UI는 보통 local 기준 유지가 안전
        popup.gameObject.SetActive(false);
        q.Enqueue(popup.gameObject);
    }

    public async UniTaskVoid TryCloseTopPopup()
    {
        if (activePopups.Count == 0)
            return;

        var top = activePopups.Peek();
        if (top.IsClosing)
            return;

        await top.CloseAsync();

        // ⚠️ CloseAsync 내부에서 ReleasePopup이 호출될 수 있으니,
        // 여기서 Pop을 "무조건" 하면 이중 Pop 위험이 있습니다.
        // 따라서 CloseAsync가 PopupManager.ReleasePopup(top)을 부르도록 규칙을 정하고,
        // 여기서는 Pop하지 않는 방식이 더 안전합니다.
        //
        // 만약 CloseAsync가 Release를 안 한다면, 아래처럼 조건부로 제거:
        if (activePopups.Count > 0 && ReferenceEquals(activePopups.Peek(), top))
            activePopups.Pop();
    }

    public bool TryGetTopPopup<TPopup>(out TPopup popup) where TPopup : PopupBaseBase
    {
        popup = null;
        if (activePopups == null || activePopups.Count == 0)
            return false;

        if (activePopups.Peek() is TPopup casted)
        {
            popup = casted;
            return true;
        }
        return false;
    }
}



public abstract class PopupBaseBase : MonoBehaviour
{
    public abstract bool IsClosing { get; set; }
    public abstract UniTask CloseAsync();
}


public abstract class PopupBase<T> : PopupBaseBase
{
    private UniTaskCompletionSource<T> _completionSource;
    protected object[] _args;

    [Header("Optional Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string closeTrigger = "Close"; // 애니메이션 트리거
    [SerializeField] private Button closeBtn;
    public override bool IsClosing { get; set; } = false;
    private CancellationTokenSource cts;

    public virtual void Awake()
    {
        closeBtn?.onClick.AddListener(async () =>
        {
            await OnClickClose();
        });
    }
    protected virtual void OnBack()
    {
        Debug.Log("Back button pressed (default)");
        OnClickClose().Forget();
    }
    public virtual void Update()
    {
#if UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBack();
        }
#elif UNITY_EDITOR
        // 에디터 테스트용 (ESC)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBack();
        }
#endif
    }

    public UniTask<T> WaitForResultAsync(object[] args)
    {
        _args = args;
        _completionSource = new UniTaskCompletionSource<T>();
        Show().Forget();
        return _completionSource.Task;
    }

    protected async UniTask SetResult(T result)
    {
        if (IsClosing)
            return;
        _completionSource.TrySetResult(result);
        await CloseAsync();
    }

    public virtual async UniTask Show()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        IsClosing = false;
        gameObject.SetActive(true);
        await WaitForFirstClip();
    }

    protected virtual async UniTask WaitForFirstClip()
    {
        if (animator == null) return;

        // State 진입할 때까지 대기
        AnimatorStateInfo state = default;
        await UniTask.WaitUntil(() =>
        {
            state = animator.GetCurrentAnimatorStateInfo(0);
            return state.length > 0f; // 아무 state 들어간 경우
        });

        // 해당 state 길이만큼 대기
        var waitTime = state.length / animator.speed;
        await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: cts.Token);
    }

    public override async UniTask CloseAsync()
    {
        if (IsClosing)
            return;
        IsClosing = true;

        if (animator != null && animator.HasParameter(closeTrigger))
        {
            animator.SetTrigger(closeTrigger);

            // "Closed" 태그가 붙은 state에 진입할 때까지 대기
            await UniTask.WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).IsTag("Closed"),
                PlayerLoopTiming.Update);

            // "Closed" state가 끝날 때까지 대기 (normalizedTime >= 1f)
            await UniTask.WaitUntil(() =>
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                return state.IsTag("Closed") && state.normalizedTime >= 1f;
            }, PlayerLoopTiming.Update);
        }

        gameObject.SetActive(false);
        PopupManager.Instance.ReleasePopup(this);
    }

    public virtual async UniTask OnClickClose()
    {
        await SetResult(default);
    }
}

public static class AnimatorExtensions
{
    public static bool HasParameter(this Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
}