using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class HitFlash : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Renderer _targetRender;
    [SerializeField] private float _flashTime = 1f;
    [SerializeField] private Color _hitColor = Color.red;

    private MaterialPropertyBlock _block;
    private Color _originalColor;
    private int _colorPropertyId;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();

        if (_targetRender == null)
            _targetRender = GetComponentInChildren<Renderer>();

        var mat = _targetRender.sharedMaterial;

        if (mat.HasProperty(BaseColorId))
            _colorPropertyId = BaseColorId;
        else if (mat.HasProperty(ColorId))
            _colorPropertyId = ColorId;
        else
        {
            Debug.LogWarning("이 머티리얼에는 _BaseColor / _Color 프로퍼티가 없습니다.");
            enabled = false;
            return;
        }

        _originalColor = mat.GetColor(_colorPropertyId);
    }

    public async UniTask PlayFlash(CancellationToken ct)
    {
        SetColor(_hitColor);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_flashTime), cancellationToken: ct);
        }
        finally
        {
            SetColor(_originalColor);
        }
    }

    private void SetColor(Color color)
    {
        _targetRender.GetPropertyBlock(_block);
        _block.SetColor(_colorPropertyId, color);
        _targetRender.SetPropertyBlock(_block);
    }
}