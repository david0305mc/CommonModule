
using System;
using System.Collections.Generic;
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
        [SerializeField] private Collider patrolCollider;
        [SerializeField] private Text stateText;
        [SerializeField] private List<Transform> patrolPoints;
        private const float _attackRange = 3f;
        private const float _searchRange = 5f;

        private float _minDistance = 0.5f;
        private float _moveSpeed = 3f;
        private float DistanceToTarget => _playerObj == null ? 0 : Vector3.Distance(transform.position, _playerObj.position);
        private Transform _playerObj;

        private StateMachine fsm;
        void Start()
        {
            _playerObj = PlayerObj.Instance.transform;
            fsm = new StateMachine();
            fsm.AddState("Patrol", new UniTaskState(
                onEnterAsync: PatrolState,
                externalCancellationToken: this.GetCancellationTokenOnDestroy()));
            fsm.AddState("Chase", new UniTaskState(
                onEnterAsync: ChaseState,
                externalCancellationToken: this.GetCancellationTokenOnDestroy()));

            fsm.AddState("Fight", new UniTaskState(
                onEnterAsync: async ct =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await UniTask.Yield(cancellationToken: ct);
                        Debug.Log("Attack");
                    }

                }, externalCancellationToken: this.GetCancellationTokenOnDestroy()));
            fsm.AddState("Search", new UniTaskState(onEnterAsync: async ct =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Yield(cancellationToken: ct);
                    Debug.Log("Search");
                }
            }, externalCancellationToken: this.GetCancellationTokenOnDestroy()));

            fsm.AddTriggerTransition("PlayerSpotted", "Patrol", "Chase");
            fsm.AddTwoWayTransition("Chase", "Fight", s => { return DistanceToTarget <= _attackRange; });
            fsm.AddTransition("Chase", "Search", s => { return DistanceToTarget <= _searchRange; });
            fsm.AddTransition(new TransitionAfter("Search", "Patrol", 2f));
            fsm.SetStartState("Patrol");
            fsm.Init();
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
            if (fsm == null)
            {
                return;
            }

            fsm.OnLogic();
            if (stateText != null)
            {
                stateText.text = fsm.GetActiveHierarchyPath();
            }
        }

        void OnDestroy()
        {
            fsm?.OnExit();
            fsm = null;
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
                fsm.Trigger("PlayerSpotted");
            }
        }

    }

}
