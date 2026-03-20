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
    [SerializeField] private bool hideWhenIdle = true;

    public Vector2 InputVector { get; private set; }

    private Camera UICamera
    {
        get
        {
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return canvas.worldCamera;
            return null;
        }
    }

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (rootCanvasRect == null && canvas != null)
            rootCanvasRect = canvas.GetComponent<RectTransform>();

        ResetJoystickVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (rootCanvasRect == null)
            return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvasRect,
                eventData.position,
                UICamera,
                out localPoint))
        {
            background.anchoredPosition = localPoint;
            handle.anchoredPosition = localPoint;

            SetVisualActive(true);
            OnDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rootCanvasRect == null)
            return;

        Vector2 pointerLocalPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvasRect,
                eventData.position,
                UICamera,
                out pointerLocalPoint))
            return;

        Vector2 center = background.anchoredPosition;
        Vector2 delta = pointerLocalPoint - center;

        Vector2 clamped = Vector2.ClampMagnitude(delta, radius);
        handle.anchoredPosition = center + clamped;

        InputVector = clamped / radius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        ResetJoystickVisual();
    }

    private void ResetJoystickVisual()
    {
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
        background.gameObject.SetActive(active);
        handle.gameObject.SetActive(active);
    }

    public float Horizontal => InputVector.x;
    public float Vertical => InputVector.y;
}