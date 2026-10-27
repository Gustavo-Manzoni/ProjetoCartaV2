using UnityEngine;
using UnityEngine.Events; 
public class PlaceableObject : MonoBehaviour
{
    [SerializeField] float _followSpeed = 15f;
    [SerializeField] float _groundClearance = 0.5f;
    [SerializeField] bool _useGrid = true;
    [SerializeField] PlacementZones _correctZone;
    public enum PlacementState { InUI, Dragging, Placed }
    public PlacementState State { get; private set; } = PlacementState.InUI;

    PlacementZone _lastPlacedZone;

    [SerializeField] UnityEvent onSuccessfulPut;
    [SerializeField] UnityEvent onPlacementReset;
 
    PlacementZone _currentZone;
    Vector3 _targetPosition;
    Vector3 _positionBeforeRedrag; 
    void Awake()
    {
        gameObject.SetActive(false); 
    }

    void Update()
    {
        if (State == PlacementState.InUI) return;

        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            Time.deltaTime * _followSpeed);
    }

    

    public void StartDraggingFromUI()
    {
        State = PlacementState.Dragging;
        gameObject.SetActive(true);
    }

    public void StartRedraggingFromWorld()
    {
        _positionBeforeRedrag = transform.position;
        _lastPlacedZone = _currentZone;
        onPlacementReset?.Invoke();
        _currentZone?.SetOccupied(false);
        _currentZone = null;
        State = PlacementState.Dragging;
    }

    public void SetTargetWorldPosition(Vector3 worldPos)
    {
        float x = _useGrid ? GridPlacement.Instance.Snap(worldPos).x : worldPos.x;
        float z = _useGrid ? GridPlacement.Instance.Snap(worldPos).z : worldPos.z;

       
        float y = Mathf.Max(worldPos.y + _groundClearance, _groundClearance);

        _targetPosition = new Vector3(x, y, z);
    }

 
    public void SnapToZone(PlacementZone zone)
    {
        _targetPosition = zone.transform.position + Vector3.up * _groundClearance;
    }


    public void PlaceInZone(PlacementZone zone)
    {
        _currentZone = zone;
        _currentZone.SetOccupied(true);
        _targetPosition = zone.transform.position + Vector3.up * _groundClearance;
        State = PlacementState.Placed;

        if (_currentZone.ZoneId == _correctZone)
        {
            onSuccessfulPut?.Invoke();
        }
        else
        {
            onPlacementReset?.Invoke();
        }
    }


    public void CancelFromUI()
    {
        State = PlacementState.InUI;
        gameObject.SetActive(false);
    }

  
   public void ReturnToPreviousPosition()
{
    _targetPosition = _positionBeforeRedrag;
    State = PlacementState.Placed;
    _lastPlacedZone?.SetOccupied(true);
    _currentZone = _lastPlacedZone;
    _lastPlacedZone = null;

    if (_currentZone != null && _currentZone.ZoneId == _correctZone)
    {
        onSuccessfulPut?.Invoke();
    }
    else
    {
        onPlacementReset?.Invoke();
    }
}
    public PlacementZone CurrentZone => _currentZone;
}
