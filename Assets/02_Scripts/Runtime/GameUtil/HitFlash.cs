using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class HitFlash : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Renderer _targetRender;
    [SerializeField] private float _flashTime = 0.08f;
    [SerializeField] private Color _hitColor = Color.red;

    private MaterialPropertyBlock _block;
    private Color _originalColor;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();

        if (_targetRender == null)
            _targetRender = GetComponentInChildren<Renderer>();

        _originalColor = _targetRender.sharedMaterial.GetColor(BaseColorId);
    }

    public async UniTask PlayFlash(CancellationToken ct)
    {
        SetFlashColor(_hitColor);

        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_flashTime),
                cancellationToken: ct
            );
        }
        finally
        {
            SetFlashColor(_originalColor);
        }
    }

    private void SetFlashColor(Color color)
    {
        _targetRender.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, color);
        _targetRender.SetPropertyBlock(_block);
    }
}