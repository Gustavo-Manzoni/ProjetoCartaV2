using TMPro;
using UnityEngine;
public enum PlacementZones 
{
    A, B, C, D, E, F, G, H, I, J


}
public class PlacementZone : MonoBehaviour
{
    [SerializeField] float _snapBreakScreenDistance = 60f;

    [SerializeField] Renderer _zoneRenderer;
    [SerializeField] Color _normalColor = new Color(1, 1, 1, 0.3f);
    [SerializeField] Color _hoverColor = new Color(0, 1, 0.5f, 0.5f);
    [SerializeField] Color _occupiedColor = new Color(1, 0.5f, 0, 0.4f);
    [SerializeField] PlacementZones zone;

    TMP_Text _zoneText;

    bool _isOccupied;
    bool _isHovered;

    void Awake()
    {
        gameObject.SetActive(false); 
        if(_zoneText)
            _zoneText.gameObject.SetActive(false);

        UpdateVisual();
        
    }

    public bool IsOccupied => _isOccupied;
    public PlacementZones ZoneId => zone;

    public void SetOccupied(bool occupied)
    {
        _isOccupied = occupied;
        if (!occupied) _isHovered = false;
        UpdateVisual();
    }

    public void SetHovered(bool hovered)
    {
        _isHovered = hovered;
        UpdateVisual();
    }
    public bool IsMouseInSnapRange(Vector2 screenMousePos, Camera renderCamera, RawImageViewport viewport)
    {
        Vector2 zoneScreenPos = viewport.WorldToViewportScreenPoint(transform.position, renderCamera);
        float dist = Vector2.Distance(screenMousePos, zoneScreenPos);
        return dist < GetSnapRadius(screenMousePos, renderCamera, viewport);
    }
    public bool IsMouseOutsideSnapBreak(Vector2 screenMousePos, Camera renderCamera, RawImageViewport viewport)
    {
        Vector2 zoneScreenPos = viewport.WorldToViewportScreenPoint(transform.position, renderCamera);
        float dist = Vector2.Distance(screenMousePos, zoneScreenPos);
        return dist > _snapBreakScreenDistance;
    }

    float GetSnapRadius(Vector2 screenMousePos, Camera renderCamera, RawImageViewport viewport)
    {
        return _snapBreakScreenDistance * 0.5f;
    }
    private void OnValidate()
    {
        _zoneText = GetComponentInChildren<TMP_Text>();
        if(_zoneText)
        _zoneText.text = zone.ToString();

    }
    void UpdateVisual()
    {
        if (_zoneRenderer == null) return;

        Color c = _isOccupied ? _occupiedColor :
                  _isHovered  ? _hoverColor :
                                _normalColor;

        _zoneRenderer.material.color = c;
    }
}
