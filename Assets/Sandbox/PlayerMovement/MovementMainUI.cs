using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace PaladinTest
{
    public class MovementMainUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button addEnemyObj;
        [SerializeField] private Button removeEnemyObj;

        [Header("References")]
        [SerializeField] private PaladinObj paladinObj;

        private GameObject spawnedEnemy;
        private bool isInitialized;

        private void Awake()
        {
            if (addEnemyObj != null)
                addEnemyObj.onClick.AddListener(OnClickAddEnemy);

            if (removeEnemyObj != null)
                removeEnemyObj.onClick.AddListener(OnClickRemoveEnemy);
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private void OnDestroy()
        {
            if (addEnemyObj != null)
                addEnemyObj.onClick.RemoveListener(OnClickAddEnemy);

            if (removeEnemyObj != null)
                removeEnemyObj.onClick.RemoveListener(OnClickRemoveEnemy);
        }

        private async UniTask InitializeAsync()
        {
            if (DataManager.Instance == null)
            {
                Debug.LogError("[MovementMainUI] DataManager.Instance is null.");
                return;
            }

            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[MovementMainUI] ResourceManager.Instance is null.");
                return;
            }

            await DataManager.Instance.LoadDataAsync();
            await ResourceManager.Instance.PreLoading();

            isInitialized = true;
        }

        private void OnClickAddEnemy()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[MovementMainUI] 아직 초기화가 끝나지 않았습니다.");
                return;
            }

            SpawnEnemy();
        }

        private void OnClickRemoveEnemy()
        {
            if (spawnedEnemy == null)
            {
                Debug.LogWarning("[MovementMainUI] 제거할 적 오브젝트가 없습니다.");
                return;
            }

            Lean.Pool.LeanPool.Despawn(spawnedEnemy);
            spawnedEnemy = null;
        }

        public void SpawnEnemy()
        {
            if (DataManager.Instance?.UnitArray == null || DataManager.Instance.UnitArray.Length == 0)
            {
                Debug.LogError("[MovementMainUI] UnitArray is null or empty.");
                return;
            }

            var unit = DataManager.Instance.UnitArray[0];
            if (unit == null)
            {
                Debug.LogError("[MovementMainUI] Unit data is null.");
                return;
            }

            var enemyPrefab = ResourceManager.Instance.GetUnitPrefab(unit.id);
            if (enemyPrefab == null)
            {
                Debug.LogError($"[MovementMainUI] Enemy prefab not found. UnitId: {unit.id}");
                return;
            }

            if (WorldObj.Instance == null || WorldObj.Instance.SpawnPoint == null)
            {
                Debug.LogError("[MovementMainUI] SpawnPoint is null.");
                return;
            }

            spawnedEnemy = Lean.Pool.LeanPool.Spawn(enemyPrefab, WorldObj.Instance.SpawnPoint);
            spawnedEnemy.transform.localPosition = Vector3.zero;
            spawnedEnemy.transform.localRotation = Quaternion.identity;
        }
    }
}