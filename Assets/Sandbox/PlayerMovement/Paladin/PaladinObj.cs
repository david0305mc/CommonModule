using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace PaladinTest
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CharacterController))]
    public class PaladinObj : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float navMeshSampleDistance = 0.3f;

        [Header("Combat")]
        [SerializeField] private float enemyDetectRadius = 3f;
        [SerializeField] private float attackCooldown = 3f;

        private Animator animator;
        private NavMeshAgent navMeshAgent;
        private CharacterController characterController;

        private float lastAttackTime = -999f;

        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            characterController = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogError($"{nameof(PaladinObj)}: Animator not found in children.", this);
            }
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
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, speed);
            }

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                MoveOnNavMesh(moveDir);
                Rotate(moveDir);
                SyncAgent();
            }
            else
            {
                Collider[] enemies = GetEnemyNearby();
                if (enemies.Length > 0)
                {
                    Attack(enemies[0].transform);
                }
            }
        }

        private void MoveOnNavMesh(Vector3 moveDir)
        {
            Vector3 moveAmount = moveDir * moveSpeed * Time.deltaTime;
            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = currentPosition + moveAmount;

            if (!TryGetNavMeshPosition(targetPosition, out Vector3 nextPosition))
            {
                return;
            }

            Vector3 delta = nextPosition - currentPosition;
            characterController.Move(delta);
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
            navMeshAgent.Warp(transform.position);
        }

        public void Attack(Transform target)
        {
            if (animator == null)
                return;

            if (Time.time - lastAttackTime < attackCooldown)
                return;
            if (target != null)
            {
                Vector3 dir = target.position - transform.position;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir);
            }

            lastAttackTime = Time.time;
            animator.SetTrigger(AttackHash);
        }

        private Collider[] GetEnemyNearby()
        {
            int enemyLayer = LayerMask.NameToLayer(GameDefine.EnemyLayerName);

            if (enemyLayer < 0)
            {
                Debug.LogWarning($"{nameof(PaladinObj)}: Enemy layer '{GameDefine.EnemyLayerName}' not found.", this);
                return null;
            }

            int layerMask = 1 << enemyLayer;
            Collider[] hits = Physics.OverlapSphere(transform.position, enemyDetectRadius, layerMask);
            return hits;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyDetectRadius);
        }
#endif
    }
}