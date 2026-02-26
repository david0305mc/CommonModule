using UnityEngine;

public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    // Awake 기반 초기화가 끝났는지 (Awake보다 먼저 Instance가 호출되는 케이스 방어)
    private static bool initialized;

    public static bool HasInstance => instance != null && !instance.Equals(null);

    public static T Instance
    {
        get
        {
            // Unity 오브젝트는 파괴돼도 C# 참조가 남을 수 있어서 Equals(null) 방어가 필요
            if (instance != null && !instance.Equals(null))
                return instance;

            instance = FindSingletonInScene();

            if (instance == null)
            {
                Debug.LogError($"[SingletonMono] {typeof(T).Name} 인스턴스를 찾을 수 없음. " +
                               $"씬에 배치했는지/활성 상태인지(또는 비활성 포함 검색 지원 버전인지) 확인해.");
                return null;
            }

            // 아직 Awake 초기화가 안 된 상태에서 Instance가 먼저 불린 경우 경고 (선택)
            if (!initialized)
            {
                // 이 경고가 시끄럽다면 제거 가능
                Debug.LogWarning($"[SingletonMono] {typeof(T).Name} Instance가 Awake 이전에 호출됨. " +
                                 $"Awake에서 초기화되는 값에 의존하면 순서 이슈가 날 수 있어.");
            }

            return instance;
        }
    }

    [SerializeField] protected bool dontDestroyOnLoad = false;

    // 플레이 시작 시 static 초기화 (도메인 리로드 옵션 꺼져 있어도 안전)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        initialized = false;
    }

    protected virtual void Awake()
    {
        if (instance != null && !instance.Equals(null) && instance != this)
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
        if (instance == this) instance = null;
    }

    protected virtual void OnInitialize() { }

    private static T FindSingletonInScene()
    {
#if UNITY_2023_1_OR_NEWER
        // 2023.1+ : 비활성 오브젝트 포함 검색 가능
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#elif UNITY_2022_2_OR_NEWER
        // 일부 2022 LTS에서도 지원되는 케이스가 있음(프로젝트 설정/버전에 따라 다름)
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        // 구버전: 활성 오브젝트만 검색
        return Object.FindObjectOfType<T>();
#endif
    }
}