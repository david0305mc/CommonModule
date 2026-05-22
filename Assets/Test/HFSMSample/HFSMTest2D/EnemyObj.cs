
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityHFSM;

namespace HFSMTest2D
{
    public class EnemyObj : MonoBehaviour
    {
        public enum EnemyState
        {
            Patrol,
            Chase,
            Fight,
            Search,
        }
        public enum FightState
        {
            Wait,
            Telegraph,
            Attack,
        }
        [SerializeField] private Animator animator;
        [SerializeField] private Collider patrolCollider;
        [SerializeField] private Text stateText;
        [SerializeField] private List<Transform> patrolPoints;
        private const float _attackRange = 2f;
        private const float _attackExitRange = 2.4f;
        private const float _fightKeepDistance = 1f;
        private const float _searchRange = 7f;
        private float _minDistance = 0.5f;
        private float _moveSpeed = 3f;
        private float DistanceToTarget => _playerObj == null ? 0 : Vector3.Distance(transform.position, _playerObj.position);
        private Transform _playerObj;

        private StateMachine _fsm;
        void Start()
        {
            _playerObj = PlayerObj.Instance.transform;
            InitFsm();
        }

        private void InitFsm()
        {
            HybridStateMachine fightFsm = new HybridStateMachine(needsExitTime: true, beforeOnLogic: s =>
            {
                MoveToward(_playerObj.position, _fightKeepDistance);
            });
            fightFsm.AddState(nameof(FightState.Wait), new UniTaskState(async ct =>
            {
                animator.Play("GuardIdle");
            }));
            fightFsm.AddState(nameof(FightState.Telegraph), new UniTaskState(async ct =>
            {
                animator.Play("GuardTelegraph");
            }));
            fightFsm.AddState(nameof(FightState.Attack), new UniTaskState(async ct =>
            {
                animator.Play("GuardHit");
                await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);
                fightFsm.RequestStateChange(nameof(FightState.Wait));
            }));
            fightFsm.AddExitTransition(nameof(FightState.Wait));
            fightFsm.AddTransition(new TransitionAfter(nameof(FightState.Wait), nameof(FightState.Telegraph), 0.5f));
            fightFsm.AddTransition(new TransitionAfter(nameof(FightState.Telegraph), nameof(FightState.Attack), 0.42f));

            _fsm = new StateMachine();
            _fsm.AddState(nameof(EnemyState.Fight), fightFsm);
            _fsm.AddState(nameof(EnemyState.Patrol), new UniTaskState(
                onEnterAsync: PatrolState,
                externalCancellationToken: this.GetCancellationTokenOnDestroy()));
            _fsm.AddState(nameof(EnemyState.Chase), new UniTaskState(
                onEnterAsync: ChaseState,
                externalCancellationToken: this.GetCancellationTokenOnDestroy()));

            _fsm.AddState(nameof(EnemyState.Search), new UniTaskState(onEnterAsync: async ct =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Yield(cancellationToken: ct);
                }
            }, onLogic: state =>
            {
                state.fsm.StateCanExit();
            }, externalCancellationToken: this.GetCancellationTokenOnDestroy()));

            _fsm.AddTriggerTransition("PlayerSpotted", nameof(EnemyState.Patrol), nameof(EnemyState.Chase),
                onTransition: transition => LogTransition(transition));
            _fsm.AddTransition(nameof(EnemyState.Chase), nameof(EnemyState.Fight),
                s => { return DistanceToTarget <= _attackRange; },
                onTransition: transition => LogTransition(transition));
            _fsm.AddTransition(nameof(EnemyState.Fight), nameof(EnemyState.Chase),
                s => { return DistanceToTarget > _attackExitRange; },
                onTransition: transition => LogTransition(transition));
            _fsm.AddTransition(nameof(EnemyState.Chase), nameof(EnemyState.Search),
                s => { return DistanceToTarget > _searchRange; },
                onTransition: transition => LogTransition(transition));
            _fsm.AddTransition(nameof(EnemyState.Search), nameof(EnemyState.Chase),
                s => { return DistanceToTarget <= _searchRange; },
                onTransition: transition => LogTransition(transition));
            _fsm.AddTransition(new TransitionAfter(nameof(EnemyState.Search), nameof(EnemyState.Patrol), 2f,
                onTransition: transition => LogTransition(transition)));
            _fsm.SetStartState(nameof(EnemyState.Patrol));
            _fsm.Init();
        }

        private void LogTransition(TransitionBase<string> transition)
        {
            Debug.Log($"[EnemyObj] State Transition: {transition.from} -> {transition.to}");
        }

        private void MoveToward(Vector3 targetPos, float minDist)
        {
            var dist = Vector3.Distance(transform.position, targetPos);
            transform.position = Vector3.MoveTowards(transform.position, targetPos,
            Math.Max(0, Math.Min(_moveSpeed * Time.deltaTime, dist - minDist)));
        }

        private async UniTask PatrolState(CancellationToken ct)
        {
            if (patrolPoints == null || patrolPoints.Count == 0)
            {
                return;
            }

            int currPatrolIndex = FindClosestPatrolPoint();
            while (!ct.IsCancellationRequested)
            {
                await Patrol(patrolPoints[currPatrolIndex].position, ct, _minDistance);
                currPatrolIndex++;
                if (currPatrolIndex >= patrolPoints.Count)
                {
                    currPatrolIndex = 0;
                }
                await UniTask.Yield(cancellationToken: ct);
            }
        }
        private async UniTask ChaseState(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                MoveToward(_playerObj.position, 0.1f);
                await UniTask.Yield(cancellationToken: ct);
            }
        }
        private async UniTask Patrol(Vector3 target, CancellationToken ct, float tolerance = 0.05f)
        {
            const float arriveEpsilon = 0.01f;

            while (!ct.IsCancellationRequested && Vector3.Distance(transform.position, target) > tolerance + arriveEpsilon)
            {
                MoveToward(target, tolerance);
                await UniTask.Yield(cancellationToken: ct);
            }
        }

        void Update()
        {
            if (_fsm == null)
            {
                return;
            }

            _fsm.OnLogic();
            if (stateText != null)
            {
                stateText.text = _fsm.GetActiveHierarchyPath();
            }
        }

        void OnDestroy()
        {
            _fsm?.OnExit();
            _fsm = null;
        }


        private int FindClosestPatrolPoint()
        {
            float minDistance = Vector2.Distance(transform.position, patrolPoints[0].position);
            int minIndex = 0;

            for (int i = 1; i < patrolPoints.Count; i++)
            {
                float distance = Vector2.Distance(transform.position, patrolPoints[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = i;
                }
            }

            return minIndex;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("PlayerSpotted");
                _fsm.Trigger("PlayerSpotted");
            }
        }

    }

}
