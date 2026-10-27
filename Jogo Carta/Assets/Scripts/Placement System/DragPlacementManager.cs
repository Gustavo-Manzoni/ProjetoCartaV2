using UnityEngine;
public class DragPlacementManager : MonoBehaviour
{
    public static DragPlacementManager Instance { get; private set; }

    [SerializeField] RawImageViewport _viewport;
    [SerializeField] PlacementZone[] _allZones;


    [SerializeField] LayerMask _groundLayer;
[SerializeField] float rayLenght = 100f;
    
    DraggableUI _currentUI;
    PlaceableObject _currentObject;
    PlacementZone _hoveredZone;
    bool _isDragging;
    bool _objectVisible; 
    bool _redragging;

    void Awake()
    {
        Instance = this;
    }


    public bool TryBeginDrag(DraggableUI ui)
    {
        if (_isDragging) return false;
        _currentUI = ui;
        _isDragging = true;
        _redragging = false;
        return true;
    }

    public void OnDragStarted(PlaceableObject obj)
    {
        _currentObject = obj;
        _objectVisible = false;
        foreach (var zone in _allZones)
        {
            if (!zone.IsOccupied)
                zone.gameObject.SetActive(true);
        }
    }
    void Update()
{
    if (!_isDragging || !_redragging) return;

    Vector2 mousePos = Input.mousePosition;

       if (Input.GetMouseButtonUp(0))
    {
        OnDragEnded();
        return;
    }

    OnDragUpdate(mousePos, true);
}
    public void OnDragUpdate(Vector2 screenPos, bool objectShouldBeVisible)
    {
       
          
        if (objectShouldBeVisible && !_objectVisible)
        {
            _objectVisible = true;
            if (_redragging){
                 
                _currentObject.StartRedraggingFromWorld();
            }
            else{
                    
                _currentObject.StartDraggingFromUI();
            }
        }
        else if (!objectShouldBeVisible && _objectVisible)
        {
               
            _objectVisible = false;
           
            if (!_redragging)
            {
                    
                _currentObject.CancelFromUI();
            }
        }

        if (!_objectVisible) return;
          
        PlacementZone newHover = GetHoveredZone(screenPos);

        if (newHover != null)
        {
           
            if (_hoveredZone != newHover)
            {
                   
                _hoveredZone?.SetHovered(false);
                _hoveredZone = newHover;
                _hoveredZone.SetHovered(true);
            }
            _currentObject.SnapToZone(_hoveredZone);
        }
        else
        {
                
          
            if (_hoveredZone != null)
            {
                 
                if (_hoveredZone.IsMouseOutsideSnapBreak(screenPos, _viewport.SceneCamera, _viewport))
                {
                    _hoveredZone.SetHovered(false);
                    _hoveredZone = null;
                       
                }
                else
                {
                  
                    return;
                }
            }

          
            if (_viewport.ScreenPointToRay(screenPos, out Ray ray))
            {
                    
                if (Physics.Raycast(ray, out RaycastHit hit, rayLenght, _groundLayer))
                {
                       
                    _currentObject.SetTargetWorldPosition(hit.point);

                }
            }
        }
    }


   public bool OnDragEnded()
{
    _isDragging = false;
    HideAllZones();

    Vector2 mousePos = Input.mousePosition;
    if (_hoveredZone == null)
    {
        PlacementZone zone = GetHoveredZone(mousePos);
        if (zone != null)
        {
            _hoveredZone = zone;
            _hoveredZone.SetHovered(true);
        }
    }

    if (_hoveredZone != null)
    {
         
            _currentObject.PlaceInZone(_hoveredZone);
            _hoveredZone.SetHovered(false);
            _hoveredZone = null;

            _currentObject = null;
            _currentUI = null;
            return true;
        }
        else
        {
          
            if (_redragging)
            {
                _currentObject.ReturnToPreviousPosition();
            }
            else
            {
                if (_objectVisible)
                    _currentObject.CancelFromUI();
             
            }

            _currentObject = null;
            _currentUI = null;
            _objectVisible = false;
            return false;
        }
    }
    public bool TryBeginWorldRedrag(PlaceableObject obj)
    {
        if (_isDragging) return false;

        _isDragging = true;
        _redragging = true;
        _objectVisible = true;
        _currentObject = obj;
        _currentUI = null;

        obj.StartRedraggingFromWorld();

        foreach (var zone in _allZones)
        {
            if (!zone.IsOccupied)
                zone.gameObject.SetActive(true);
        }

        return true;
    }

  
    PlacementZone GetHoveredZone(Vector2 screenPos)
{
    if (!_viewport.ScreenPointToRay(screenPos, out Ray ray))
        return null;

    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
    {
        if (hit.collider.TryGetComponent(out PlacementZone zone))
        {
            if (!zone.IsOccupied)
                return zone;
        }
    }

    return null;
}

    void HideAllZones()
    {
        foreach (var zone in _allZones)
            zone.gameObject.SetActive(false);
    }

    public bool IsDragging => _isDragging;
}
