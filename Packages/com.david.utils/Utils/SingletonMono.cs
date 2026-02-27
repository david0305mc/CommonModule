using UnityEngine;

public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static bool initialized;

    public static bool HasInstance
    {
        get
        {
            CleanupIfDestroyed();
            return instance != null;
        }
    }

    public static T Instance
    {
        get
        {
            CleanupIfDestroyed();

            if (instance != null)
                return instance;

            instance = FindSingletonInScene();

            if (instance == null)
            {
                Debug.LogError($"[SingletonMono] {typeof(T).Name} 인스턴스를 찾을 수 없음. " +
                               $"씬에 배치했는지/활성 상태인지(또는 비활성 포함 검색 지원 버전인지) 확인해.");
                return null;
            }

            if (!initialized)
            {
                Debug.LogWarning($"[SingletonMono] {typeof(T).Name} Instance가 Awake 이전에 호출됨. " +
                                 $"Awake에서 초기화되는 값에 의존하면 순서 이슈가 날 수 있어.");
            }

            return instance;
        }
    }

    [SerializeField] protected bool dontDestroyOnLoad = false;

    protected virtual void Awake()
    {
        CleanupIfDestroyed();

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this as T;

        if (dontDestroyOnLoad && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        initialized = true;
        OnInitialize();
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            initialized = false;
        }
    }

    protected virtual void OnInitialize() { }

    private static void CleanupIfDestroyed()
    {
        // UnityEngine.Object는 Destroy되면 C# 참조가 남아도 "== null"처럼 동작할 수 있어서 방어 필요
        if (instance != null && instance.Equals(null))
        {
            instance = null;
            initialized = false;
        }
    }

    private static T FindSingletonInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#elif UNITY_2022_2_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>();
#endif
    }
}