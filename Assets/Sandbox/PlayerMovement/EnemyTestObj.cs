using UnityEngine;
using UnityEngine.AI;

namespace PaladinTest
{
    public class EnemyTestObj : MonoBehaviour
    {
        private Transform target;
        NavMeshAgent enemyAgent;
        void Start()
        {
            enemyAgent = GetComponent<NavMeshAgent>();
            enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            target = WorldObj.Instance.PlayerObj.transform;
        }

        void Update()
        {
            if (Vector3.Distance(target.position, transform.position) > 3f)
            {
                enemyAgent.SetDestination(target.position);
            }
        }



    }

}
