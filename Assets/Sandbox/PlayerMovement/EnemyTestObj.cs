using UnityEngine;
using UnityEngine.AI;

public class EnemyTestObj : MonoBehaviour
{
    [SerializeField] private Transform target;
    NavMeshAgent navMeshAgent;
    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        navMeshAgent.SetDestination(target.position);
    }



}
