
using UnityEngine;
using System.Linq;

public class EnemyPatrolPoints : MonoBehaviour
{
    
    public Transform[] Points { get; private set; }
    void Awake()
    {
        Points = GetComponentsInChildren<Transform>()
        .Where(t => t != transform)
        .ToArray();
    }
}
