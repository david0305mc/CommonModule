using UnityEngine;

public class ColliderDetection : MonoBehaviour
{
    private System.Action<Collider> _triggerEnterAction;
    private System.Action<Collider> _triggerExitAction;

    public void SetOnTriggerAction(System.Action<Collider> triggerEnterAction, System.Action<Collider> triggerExit = null)
    {
        _triggerEnterAction = triggerEnterAction;
        _triggerExitAction = triggerExit;
    }

    void OnTriggerEnter(Collider other)
    {
        _triggerEnterAction?.Invoke(other);
    }

    void OnTriggerExit(Collider other)
    {
        _triggerExitAction?.Invoke(other);
    }
}
