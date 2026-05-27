using UnityEngine;

[DisallowMultipleComponent]
public class BillboardText : MonoBehaviour
{
    private enum FacingMode
    {
        MatchCameraRotation,
        LookAtCamera,
    }

    [SerializeField] private Camera _targetCamera;
    [SerializeField] private FacingMode _facingMode = FacingMode.MatchCameraRotation;
    [SerializeField] private bool _lockYAxis;
    [SerializeField] private bool _flipForward;

    private void OnEnable()
    {
        ApplyBillboard();
    }

    private void LateUpdate()
    {
        ApplyBillboard();
    }

    public void SetTargetCamera(Camera targetCamera)
    {
        _targetCamera = targetCamera;
        ApplyBillboard();
    }

    private void ApplyBillboard()
    {
        Camera targetCamera = GetTargetCamera();
        if (targetCamera == null)
        {
            return;
        }

        Transform cameraTransform = targetCamera.transform;
        Vector3 forward = GetForward(cameraTransform);
        Vector3 up = _lockYAxis ? Vector3.up : cameraTransform.up;

        if (_lockYAxis)
        {
            forward.y = 0f;
        }

        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        if (_flipForward)
        {
            forward = -forward;
        }

        transform.rotation = Quaternion.LookRotation(forward.normalized, up);
    }

    private Camera GetTargetCamera()
    {
        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }

        return _targetCamera;
    }

    private Vector3 GetForward(Transform cameraTransform)
    {
        if (_facingMode == FacingMode.LookAtCamera)
        {
            return cameraTransform.position - transform.position;
        }

        return cameraTransform.forward;
    }
}
