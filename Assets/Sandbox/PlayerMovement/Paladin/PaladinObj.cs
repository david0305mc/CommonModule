using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PaladinObj : MonoBehaviour
{

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float navMeshSampleDistance = 0.3f;
    private Animator animator;

    private NavMeshAgent navMeshAgent;
    private CharacterController characterController;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
        navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;

        if (GameInputManager.HasInstance)
        {
            input = GameInputManager.Instance.MoveValue;
        }

        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);
        float speed = moveDir.magnitude;
        animator.SetFloat("Speed", speed);

        MoveOnNavMesh(moveDir);
        Rotate(moveDir);
        SyncAgent();
    }

    private void MoveOnNavMesh(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude <= 0.0001f)
            return;

        Vector3 moveAmount = moveDir * moveSpeed * Time.deltaTime;
        Vector3 currentPosition = transform.position;

        if (TryGetNavMeshPosition(currentPosition + moveAmount, out Vector3 nextPosition))
        {
            transform.position = nextPosition;
            return;
        }

        Vector3 xOnly = new Vector3(moveAmount.x, 0f, 0f);
        if (xOnly.sqrMagnitude > 0.0001f &&
            TryGetNavMeshPosition(currentPosition + xOnly, out nextPosition))
        {
            transform.position = nextPosition;
            return;
        }

        Vector3 zOnly = new Vector3(0f, 0f, moveAmount.z);
        if (zOnly.sqrMagnitude > 0.0001f &&
            TryGetNavMeshPosition(currentPosition + zOnly, out nextPosition))
        {
            transform.position = nextPosition;
        }
    }

    private bool TryGetNavMeshPosition(Vector3 targetPosition, out Vector3 result)
    {
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = transform.position;
        return false;
    }

    private void Rotate(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void SyncAgent()
    {
        navMeshAgent.nextPosition = transform.position;
    }

    public void OnJump()
    {
        Debug.Log("Jump!");
    }

    public void Attack()
    {
        animator.SetTrigger("Attack");
    }
}