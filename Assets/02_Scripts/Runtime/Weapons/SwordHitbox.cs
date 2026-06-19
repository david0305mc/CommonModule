using System;
using System.Collections.Generic;
using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    private readonly HashSet<IDamageable> _hitTargets = new();

    private GameObject _owner;
    private Action<HitContext> _hitAction;

    public void Initialize(GameObject owner, Action<HitContext> hitAction)
    {
        GameUtil.SetLayerRecursively(gameObject, LayerMask.NameToLayer("Default"));
        _owner = owner;
        _hitAction = hitAction;
        _hitTargets.Clear();
    }

    public void ResetHitTargets()
    {
        _hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_owner == null)
            return;

        if (other == null || other.gameObject == _owner)
            return;

        if (_owner.layer == other.gameObject.layer)
            return;

        if (!other.TryGetComponent(out IDamageable damageable))
            return;
        damageable.TakeDamage();
        // if (!_hitTargets.Add(damageable))
        //     return;

        var hitContext = new HitContext
        {
            Attacker = _owner,
            Target = other.gameObject,
            HitPoint = other.ClosestPoint(transform.position)
        };

        _hitAction?.Invoke(hitContext);
    }
}