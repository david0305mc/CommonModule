using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform rootCanvasRect;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private Canvas canvas;

    [Header("Settings")]
    [SerializeField] private float radius = 100f;
    [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.1f;
    [SerializeField] private bool hideWhenIdle = true;

    public Vector2 InputVector { get; private set; }
    public bool IsPressed { get; private set; }

    private static readonly List<RaycastResult> s_RaycastResults = new();
    private Camera UICamera
    {
        get
        {
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return canvas.worldCamera;
            }

            return null;
        }
    }

    private void OnValidate()
    {
        radius = Mathf.Max(1f, radius);
        deadZone = Mathf.Clamp01(deadZone);
    }

    private void Awake()
    {
        if (!TryResolveReferences())
        {
            Debug.LogWarning($"{nameof(FloatingJoystick)} on {name} is missing required references.", this);
            enabled = false;
            return;
        }

        ResetJoystickVisual();
    }

    private void OnEnable()
    {
        RegisterWithInputManager();
    }

    private void OnDisable()
    {
        ClearInput();
        UnregisterFromInputManager();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!TryResolveReferences())
        {
            return;
        }

        if (IsPointerOverBlockingUI(eventData))
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvasRect,
                eventData.position,
                UICamera,
                out Vector2 localPoint))
        {
            IsPressed = true;
            background.anchoredPosition = localPoint;
            handle.anchoredPosition = localPoint;

            SetVisualActive(true);
            OnDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!TryResolveReferences())
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvasRect,
                eventData.position,
                UICamera,
                out Vector2 pointerLocalPoint))
        {
            return;
        }

        Vector2 center = background.anchoredPosition;
        Vector2 delta = pointerLocalPoint - center;
        float effectiveRadius = Mathf.Max(1f, radius);
        Vector2 normalizedInput = Vector2.ClampMagnitude(delta / effectiveRadius, 1f);

        if (normalizedInput.sqrMagnitude < deadZone * deadZone)
        {
            normalizedInput = Vector2.zero;
        }

        InputVector = normalizedInput;
        handle.anchoredPosition = center + (normalizedInput * effectiveRadius);
        PushInputToManager();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ClearInput();
    }

    private void ResetJoystickVisual()
    {
        if (background == null || handle == null)
        {
            return;
        }

        handle.anchoredPosition = background.anchoredPosition;

        if (hideWhenIdle)
        {
            SetVisualActive(false);
        }
        else
        {
            SetVisualActive(true);
        }
    }

    private void SetVisualActive(bool active)
    {
        if (background == null || handle == null)
        {
            return;
        }

        background.gameObject.SetActive(active);
        handle.gameObject.SetActive(active);
    }

    private bool TryResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvasRect == null && canvas != null)
        {
            rootCanvasRect = canvas.GetComponent<RectTransform>();
        }

        return rootCanvasRect != null && background != null && handle != null;
    }

    private void ClearInput()
    {
        InputVector = Vector2.zero;
        IsPressed = false;
        ResetJoystickVisual();
        PushInputToManager();
    }

    private void RegisterWithInputManager()
    {
        if (!GameInputManager.HasInstance)
        {
            return;
        }

        GameInputManager.Instance.RegisterVirtualJoystick(this);
    }

    private void UnregisterFromInputManager()
    {
        if (!GameInputManager.HasInstance)
        {
            return;
        }

        GameInputManager.Instance.UnregisterVirtualJoystick(this);
    }

    private void PushInputToManager()
    {
        if (!GameInputManager.HasInstance)
        {
            return;
        }

        GameInputManager.Instance.SetVirtualMoveInput(this, InputVector);
    }

    private bool IsPointerOverBlockingUI(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        s_RaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, s_RaycastResults);

        for (int i = 0; i < s_RaycastResults.Count; i++)
        {
            GameObject hitObject = s_RaycastResults[i].gameObject;

            // 자기 자신(조이스틱 루트/배경/핸들 포함)이면 허용
            if (hitObject.transform == transform || hitObject.transform.IsChildOf(transform))
            {
                continue;
            }

            // 조이스틱이 아닌 다른 UI를 건드렸으면 차단
            return true;
        }

        return false;
    }

    public float Horizontal => InputVector.x;
    public float Vertical => InputVector.y;
}
