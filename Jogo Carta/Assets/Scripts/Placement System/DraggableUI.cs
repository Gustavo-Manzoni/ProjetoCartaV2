using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class DraggableUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] PlaceableObject _worldObject;     
    [SerializeField] CanvasGroup _canvasGroup;        

    [SerializeField] float _fadeStartX = 0.8f;
    [SerializeField] float _fadeEndX = 0.3f;

    Vector2 _originalAnchoredPosition;
    RectTransform _rectTransform;
    Canvas _rootCanvas;
    bool _isDragging;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>();
        _originalAnchoredPosition = _rectTransform.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {

        if (!DragPlacementManager.Instance.TryBeginDrag(this))
            return;

        _isDragging = true;
        _canvasGroup.blocksRaycasts = false;
        DragPlacementManager.Instance.OnDragStarted(_worldObject);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
        float normalizedX = eventData.position.x / Screen.width;

        float alpha = Mathf.InverseLerp(_fadeEndX, _fadeStartX, normalizedX);
        _canvasGroup.alpha = alpha;

        bool objectShouldBeVisible = normalizedX <= _fadeEndX;
        DragPlacementManager.Instance.OnDragUpdate(eventData.position, !objectShouldBeVisible);
    }

   public void OnEndDrag(PointerEventData eventData)
{
    if (!_isDragging) return;

    _isDragging = false;
    _canvasGroup.blocksRaycasts = true;

    bool placed = DragPlacementManager.Instance.OnDragEnded();

    if (!placed)
    {
        ReturnToBar();
    }
    else
    {
        gameObject.SetActive(false); 
    }

    _canvasGroup.alpha = 1f;
}

    void ReturnToBar()
    {
        _rectTransform.anchoredPosition = _originalAnchoredPosition;
    }
    public PlaceableObject WorldObject => _worldObject;
}
