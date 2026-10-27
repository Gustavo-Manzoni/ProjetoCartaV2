using UnityEngine;
public class PlaceableObjectInput : MonoBehaviour
{
     [SerializeField] float _dragThreshold = 10f;
    [SerializeField] RawImageViewport _viewport;

    PlaceableObject _placeableObject;
    bool _mouseDown;
    Vector2 _mouseDownPos;

    void Awake()
    {
        _placeableObject = GetComponent<PlaceableObject>();
    }

    void Update()
    {
        if (_placeableObject.State != PlaceableObject.PlacementState.Placed) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverSelf())
            {
                _mouseDown = true;
                _mouseDownPos = Input.mousePosition;
            }
        }

        if (_mouseDown)
        {
            if (Input.GetMouseButtonUp(0))
            {
                _mouseDown = false;
                return;
            }

            float dist = Vector2.Distance(Input.mousePosition, _mouseDownPos);
            if (dist > _dragThreshold)
            {
                _mouseDown = false;
                DragPlacementManager.Instance.TryBeginWorldRedrag(_placeableObject);
            }
        }
    }

    bool IsMouseOverSelf()
    {
        if (!_viewport.ScreenPointToRay(Input.mousePosition, out Ray ray))
            return false;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            return hit.collider.gameObject == gameObject;

        return false;
    }
}
