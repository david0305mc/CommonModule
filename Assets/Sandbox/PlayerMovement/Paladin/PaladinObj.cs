using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityHFSM;

namespace PaladinTest
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CharacterController))]
    public class PaladinObj : MonoBehaviour
    {
        public enum PlayerState
        {
            Locomotion,
            Combat,
        }

        public enum CombatState
        {
            Idle,
            Attack,
        }

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [SerializeField] private TextMeshProUGUI _stateText;

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
        private StateMachine _fsm;

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
            InitFsm();
        }
        private void InitFsm()
        {
            _fsm = new StateMachine();
            _fsm.AddState(nameof(PlayerState.Locomotion), new UniTaskState(onEnterAsync: RunLocomotionState));

            var combatFsm = new HybridStateMachine(
                needsExitTime: true,
                beforeOnEnter: s =>
                {

                }, afterOnExit: s =>
                {

                });
            combatFsm.AddState(nameof(CombatState.Idle), new UniTaskState(RunCombatIdleState));
            combatFsm.AddState(nameof(CombatState.Attack), new UniTaskState(RunAttackState));
            combatFsm.SetStartState(nameof(CombatState.Idle));
            _fsm.AddState(nameof(PlayerState.Combat), combatFsm);
            _fsm.SetStartState(nameof(PlayerState.Locomotion));
            _fsm.Init();
        }
        private async UniTask RunCombatIdleState(CancellationToken ct)
        {
            float delay = 0f;
            while (!ct.IsCancellationRequested && delay < 0.3f)
            {
                Collider[] enemies = GetEnemyNearby();
                if (enemies.Length == 0)
                {
                    _fsm.RequestStateChange(nameof(PlayerState.Locomotion));
                    return;
                }
                await UniTask.Yield(cancellationToken: ct);
            }

            _fsm.RequestStateChange(nameof(CombatState.Attack));
        }
        private async UniTask RunAttackState(CancellationToken ct)
        {
            Collider[] enemies = GetEnemyNearby();
            if (enemies.Length > 0)
            {
                Attack(enemies[0].transform);
            }
            await UniTask.WaitForSeconds(1f, cancellationToken: ct);
            _fsm.RequestStateChange(nameof(CombatState.Idle));
        }

        private async UniTask RunLocomotionState(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);

                Vector2 input = Vector2.zero;
                if (GameInputManager.HasInstance)
                {
                    input = GameInputManager.Instance.MoveValue;
                }

                Vector3 moveDir = new Vector3(input.x, 0f, input.y);
                moveDir = Vector3.ClampMagnitude(moveDir, 1f);

                float speed = moveDir.magnitude;
                animator.SetFloat(SpeedHash, speed);

                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    HandleMovement(moveDir);
                }
                else
                {
                    Collider[] enemies = GetEnemyNearby();
                    if (enemies.Length > 0)
                    {
                        _fsm.RequestStateChange(nameof(PlayerState.Combat));
                    }
                }
            }
        }

        private void Update()
        {
            _stateText.SetText(_fsm.GetActiveHierarchyPath());
        }
        private void HandleMovement(Vector3 moveDir)
        {
            MoveOnNavMesh(moveDir);
            Rotate(moveDir);
            SyncAgent();
        }
        private void TryAttackNearbyEnemy()
        {
            if (Time.time - lastAttackTime < attackCooldown)
                return;
            Collider[] enemies = GetEnemyNearby();
            if (enemies.Length > 0)
            {
                Attack(enemies[0].transform);
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
            if (target != null)
            {
                Vector3 dir = target.position - transform.position;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir);
            }

            lastAttackTime = Time.time;
            animator.Play(AttackHash);
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
