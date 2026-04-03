using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyObj : MonoBehaviour
{
    private Transform target;
    private NavMeshAgent enemyAgent;

    [Header("Separation")]
    [SerializeField] private float separationRadius = 1.2f;
    [SerializeField] private float separationStrength = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Chase")]
    [SerializeField] private float stopDistance = 3f;
    [SerializeField] private float destinationUpdateInterval = 0.1f;

    private float destinationUpdateTimer;

    private void Start()
    {
        enemyAgent = GetComponent<NavMeshAgent>();
        enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        target = WorldObj.Instance.PlayerObj.transform;
    }

    private void Update()
    {
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= stopDistance)
        {
            enemyAgent.ResetPath();
            return;
        }

        destinationUpdateTimer -= Time.deltaTime;
        if (destinationUpdateTimer <= 0f)
        {
            destinationUpdateTimer = destinationUpdateInterval;
            SetDestivation();
        }
    }

    private void SetDestivation()
    {
        Vector3 separationOffset = CalculateSeparationOffset();
        Vector3 desiredDestination = target.position + separationOffset;

        if (NavMesh.SamplePosition(desiredDestination, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
        {
            enemyAgent.SetDestination(hit.position);
        }
        else
        {
            enemyAgent.SetDestination(target.position);
        }
    }

    private Vector3 CalculateSeparationOffset()
    {
        Vector3 separation = Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, enemyLayer);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 diff = transform.position - hit.transform.position;
            diff.y = 0f;

            float dist = diff.magnitude;
            if (dist < 0.001f) continue;

            float weight = 1f - (dist / separationRadius);
            separation += diff.normalized * weight;
        }

        separation.y = 0f;

        if (separation.sqrMagnitude > 0.001f)
        {
            separation = separation.normalized * separationStrength;
        }

        return separation;
    }
}
