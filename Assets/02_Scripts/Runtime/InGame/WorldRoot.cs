using UnityEngine;

public class WorldRoot : SingletonMono<WorldRoot>
{
    public GameObject PlayerObj;
    public Transform SpawnPoint;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("Awake");
    }
}
