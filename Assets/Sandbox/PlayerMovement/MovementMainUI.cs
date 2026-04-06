using UnityEngine;
using UnityEngine.UI;

namespace PaladinTest
{
    public class MovementMainUI : MonoBehaviour
    {
        [SerializeField] private EnemyObj enemyObjPrefab;

        [SerializeField] private Button addEnemyObj;
        [SerializeField] private Button removeEnemyObj;
        [SerializeField] private PaladinObj paladinObj;
        


        void Awake()
        {
            addEnemyObj.onClick.AddListener(() =>
            {
                // paladinObj.Attack(null);
                SpawnEnemy();
            });
        }

        public void SpawnEnemy()
        {
            var enemyObj = Lean.Pool.LeanPool.Spawn(enemyObjPrefab, WorldObj.Instance.SpawnPoint);
            enemyObj.transform.localPosition = Vector3.zero;
        }
    }

}
