
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityHFSM;

namespace HFSMTest2D
{
    public class EnemyObj : MonoBehaviour
    {
        [SerializeField] private List<Transform> patrolPoints;
        private Transform _target;

        private StateMachine fsm;
        void Start()
        {
            _target = PlayerObj.Instance.transform;
            fsm = new StateMachine();
            fsm.AddState("Patrol", new UniTaskState(
                onEnterAsync:
                async ct =>
                {
                    await UniTask.Delay(1000, cancellationToken: ct);
                },
                onLogic: s =>
                {
                    Debug.Log("Patrol");
                }));
            fsm.AddState("Fight");
            fsm.AddState("Chase");
            fsm.AddState("Search");

            fsm.SetStartState("Patrol");
            fsm.Init();
        }

        void Update()
        {
            fsm.OnLogic();
            // var dist = _target.position - transform.position;
            // if (dist.magnitude > 1f)
            // {

            //     transform.position += dist * Time.deltaTime;      
            // }

        }

    }

}
