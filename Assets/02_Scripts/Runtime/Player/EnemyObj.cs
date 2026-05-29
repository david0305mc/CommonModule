using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityHFSM;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyObj : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Combat,
        Search,
    }

    public enum CombatState
    {
        Wait,
        Telegraph,
        Attack,
    }

    [Header("References")]
    [SerializeField] private ColliderDetection _detectionRange;
    [SerializeField] private TextMeshProUGUI _stateText;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 2f;

    [Header("Ranges")]
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _attackExitRange = 2.4f;
    [SerializeField] private float _searchRange = 3f;

    [Header("Fight Timing")]
    [SerializeField] private float _attackCooldown = 1.0f;
    [SerializeField] private float _telegraphTime = 0.25f;
    [SerializeField] private float _damageNormalizedTime = 0.45f;

    private const string _zombieIdleAnimation = "ZombieIdle";
    private const string _zombieWalkAnimation = "ZombieWalk";
    private const string _zombieRunAnimation = "ZombieRun";
    private const string _zombieAttackAnimation = "ZombieAttack";

    private Transform[] patrolPoints;
    private Transform target;
    private NavMeshAgent enemyAgent;

    private StateMachine _fsm;
    private HybridStateMachine _combatFsm;
    private bool _damageApplied;

    private float DistanceToTarget =>
        target == null
            ? float.PositiveInfinity
            : Vector3.Distance(transform.position, target.position);

    private void Start()
    {
        enemyAgent = GetComponent<NavMeshAgent>();

        ApplyAgentSettings();

        target = WorldRoot.Instance.PlayerObj.transform;
        patrolPoints = WorldRoot.Instance.EnemyPatrolPoints.Points;

        InitFsm();
        InitDetection();
    }

    private void OnValidate()
    {
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.speed = _moveSpeed;
            agent.updateRotation = false;
        }
    }

    private void ApplyAgentSettings()
    {
        enemyAgent.speed = _moveSpeed;
        enemyAgent.updateRotation = false;
        enemyAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void InitDetection()
    {
        _detectionRange.SetOnTriggerAction(other =>
        {
            if (other.CompareTag("Player"))
            {
                _fsm.Trigger("PlayerSpotted");
            }
        });
    }

    private void InitFsm()
    {
        _combatFsm = new HybridStateMachine(
            needsExitTime: true,
            beforeOnEnter: s =>
            {
                StopAgent();
                _damageApplied = false;
            },
            afterOnExit: s =>
            {
                _damageApplied = false;
                ResumeAgent();
            }
        );

        _combatFsm.AddState(nameof(CombatState.Wait), new UniTaskState(async ct =>
        {
            animator.Play(_zombieIdleAnimation);

            float elapsed = 0f;

            while (elapsed < _attackCooldown)
            {
                ct.ThrowIfCancellationRequested();

                FaceTargetImmediately();

                elapsed += Time.deltaTime;
                await UniTask.Yield(cancellationToken: ct);
            }

            _combatFsm.RequestStateChange(nameof(CombatState.Telegraph));
        }));

        _combatFsm.AddState(nameof(CombatState.Telegraph), new UniTaskState(async ct =>
        {
            animator.Play(_zombieIdleAnimation);

            float elapsed = 0f;

            while (elapsed < _telegraphTime)
            {
                ct.ThrowIfCancellationRequested();

                FaceTargetImmediately();

                elapsed += Time.deltaTime;
                await UniTask.Yield(cancellationToken: ct);
            }

            // 공격 직전 방향 확정
            FaceTargetImmediately();

            _combatFsm.RequestStateChange(nameof(CombatState.Attack));
        }));

        _combatFsm.AddState(nameof(CombatState.Attack), new UniTaskState(async ct =>
        {
            _damageApplied = false;

            StopAgent();

            animator.Play(_zombieAttackAnimation, 0, 0f);

            await WaitForAttackAnimationAsync(ct);

            _combatFsm.RequestStateChange(nameof(CombatState.Wait));
        }));

        _combatFsm.AddExitTransition(nameof(CombatState.Wait));

        _fsm = new StateMachine();

        _fsm.AddState(nameof(EnemyState.Combat), _combatFsm);

        _fsm.AddState(nameof(EnemyState.Patrol), new UniTaskState(
            onEnterAsync: PatrolState,
            externalCancellationToken: this.GetCancellationTokenOnDestroy()));

        _fsm.AddState(nameof(EnemyState.Chase), new UniTaskState(
            onEnterAsync: ChaseState,
            externalCancellationToken: this.GetCancellationTokenOnDestroy()));

        _fsm.AddState(nameof(EnemyState.Search), new UniTaskState(
            onEnterAsync: SearchState,
            externalCancellationToken: this.GetCancellationTokenOnDestroy()));

        _fsm.AddTriggerTransition(
            "PlayerSpotted",
            nameof(EnemyState.Patrol),
            nameof(EnemyState.Chase),
            onTransition: transition => LogTransition(transition));

        _fsm.AddTransition(
            nameof(EnemyState.Chase),
            nameof(EnemyState.Combat),
            s => DistanceToTarget <= _attackRange,
            onTransition: transition => LogTransition(transition));

        _fsm.AddTransition(
            nameof(EnemyState.Combat),
            nameof(EnemyState.Chase),
            s => DistanceToTarget > _attackExitRange,
            onTransition: transition => LogTransition(transition));

        _fsm.AddTransition(
            nameof(EnemyState.Chase),
            nameof(EnemyState.Search),
            s => DistanceToTarget > _searchRange,
            onTransition: transition => LogTransition(transition));

        _fsm.AddTransition(
            nameof(EnemyState.Search),
            nameof(EnemyState.Chase),
            s => DistanceToTarget <= _searchRange,
            onTransition: transition => LogTransition(transition));

        _fsm.AddTransition(new TransitionAfter(
            nameof(EnemyState.Search),
            nameof(EnemyState.Patrol),
            2f,
            onTransition: transition => LogTransition(transition)));

        _fsm.SetStartState(nameof(EnemyState.Patrol));
        _fsm.Init();
    }

    private async UniTask PatrolState(CancellationToken ct)
    {
        ResumeAgent();
        animator.Play(_zombieWalkAnimation);

        int patrolIndex = 0;

        while (!ct.IsCancellationRequested)
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                await UniTask.Yield(cancellationToken: ct);
                continue;
            }

            Vector3 targetPos = patrolPoints[patrolIndex].position;

            await MoveToAsync(targetPos, ct);

            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private async UniTask ChaseState(CancellationToken ct)
    {
        ResumeAgent();
        animator.Play(_zombieRunAnimation);

        enemyAgent.stoppingDistance = 0f;

        while (!ct.IsCancellationRequested)
        {
            if (target == null)
            {
                await UniTask.Yield(cancellationToken: ct);
                continue;
            }

            MoveToward(target.position, 0f);

            await UniTask.Yield(cancellationToken: ct);
        }
    }

    private async UniTask SearchState(CancellationToken ct)
    {
        ResumeAgent();
        animator.Play(_zombieWalkAnimation);

        while (!ct.IsCancellationRequested)
        {
            await UniTask.Yield(cancellationToken: ct);
        }
    }

    private async UniTask MoveToAsync(Vector3 targetPos, CancellationToken ct)
    {
        ResumeAgent();

        enemyAgent.SetDestination(targetPos);

        while (!ct.IsCancellationRequested)
        {
            if (!enemyAgent.pathPending &&
                enemyAgent.remainingDistance <= enemyAgent.stoppingDistance + 0.1f)
            {
                break;
            }

            await UniTask.Yield(cancellationToken: ct);
        }
    }

    private void MoveToward(Vector3 targetPos, float minDist)
    {
        FacePositionImmediately(targetPos);

        enemyAgent.stoppingDistance = minDist;

        if (Vector3.Distance(transform.position, targetPos) <= minDist)
        {
            enemyAgent.ResetPath();
            return;
        }

        enemyAgent.SetDestination(targetPos);
    }

    private async UniTask WaitForAttackAnimationAsync(CancellationToken ct)
    {
        int stateHash = Animator.StringToHash(_zombieAttackAnimation);

        await UniTask.Yield(cancellationToken: ct);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            bool isTargetState =
                stateInfo.shortNameHash == stateHash ||
                stateInfo.IsName(_zombieAttackAnimation);

            if (isTargetState)
            {
                if (!_damageApplied && stateInfo.normalizedTime >= _damageNormalizedTime)
                {
                    _damageApplied = true;
                    TryApplyAttackDamage();
                }

                if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
                {
                    break;
                }
            }

            await UniTask.Yield(cancellationToken: ct);
        }
    }

    private void TryApplyAttackDamage()
    {
        if (target == null)
        {
            return;
        }

        if (DistanceToTarget > _attackRange)
        {
            return;
        }

        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Vector3.Angle(transform.forward, directionToTarget.normalized);

        // 정면 약 120도 범위
        if (angle > 60f)
        {
            return;
        }

        Debug.Log("공격 적중!");

        // TODO:
        // 여기서 Player 체력 감소 처리
        // 예: target.GetComponent<PlayerHealth>()?.TakeDamage(1);
    }

    private void FaceTargetImmediately()
    {
        if (target == null)
        {
            return;
        }

        FacePositionImmediately(target.position);
    }

    private void FacePositionImmediately(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void StopAgent()
    {
        if (enemyAgent == null)
        {
            return;
        }

        enemyAgent.ResetPath();
        enemyAgent.velocity = Vector3.zero;
        enemyAgent.isStopped = true;
    }

    private void ResumeAgent()
    {
        if (enemyAgent == null)
        {
            return;
        }

        if (enemyAgent.isStopped)
        {
            enemyAgent.isStopped = false;
        }
    }

    private void LogTransition(TransitionBase<string> transition)
    {
        Debug.Log($"[EnemyObj] State Transition: {transition.from} -> {transition.to}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("플레이어 발견!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("플레이어 놓침!");
        }
    }

    private void Update()
    {
        if (_fsm == null)
        {
            return;
        }

        _fsm.OnLogic();

        if (_stateText != null)
        {
            _stateText.text = _fsm.GetActiveHierarchyPath();
        }
    }

    private void OnDestroy()
    {
        _fsm?.OnExit();
        _fsm = null;
    }
}