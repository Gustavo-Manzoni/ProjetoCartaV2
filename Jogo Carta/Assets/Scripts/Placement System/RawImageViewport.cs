using UnityEngine;
using UnityEngine.UI;

public class RawImageViewport : MonoBehaviour
{
    [SerializeField] RawImage _rawImage;
     Camera _sceneCamera;

    public Camera SceneCamera { get => _sceneCamera; set => _sceneCamera = value; }

    public bool ScreenPointToRay(Vector2 screenPos, out Ray ray)
    {
        ray = default;

        RectTransform rect = _rawImage.rectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
    rect, screenPos, Camera.main, out Vector2 localPoint))
    return false;

        Rect r = rect.rect;
        float x = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);
        float y = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);

        
        if (x < 0 || x > 1 || y < 0 || y > 1)
            return false;

        ray = SceneCamera.ViewportPointToRay(new Vector3(x, y, 0));
        return true;
    }

   
    public Vector2 WorldToViewportScreenPoint(Vector3 worldPos, Camera cam)
    {
        Vector3 viewport = cam.WorldToViewportPoint(worldPos);

        RectTransform rect = _rawImage.rectTransform;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

       
        Vector3 bottomLeft = corners[0];
        Vector3 bottomRight = corners[3];
        Vector3 topLeft = corners[1];

        Vector3 screenPos =
            bottomLeft +
            (bottomRight - bottomLeft) * viewport.x +
            (topLeft - bottomLeft) * viewport.y;
                
        return RectTransformUtility.WorldToScreenPoint(null, screenPos);
    }
}
