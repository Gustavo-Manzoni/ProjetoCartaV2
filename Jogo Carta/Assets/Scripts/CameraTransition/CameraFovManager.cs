using UnityEngine;
using Unity.Cinemachine;
public enum CameraZoomType 
{
    Default, Photo
}
public class CameraFovManager : MonoBehaviour
{
    [System.Serializable]
    struct CameraZoomTypes
    {
        public CameraZoomType zoomType;
        public float targetFov;
        public float fovSpeed;

    }
    float targetFov;
    float targetFovSpeed;
    [SerializeField] CameraZoomTypes[] cameraZoomTypes;
     
    [SerializeField]CinemachineCamera boardCamera;

    void Awake()
    {
        ServiceLocator.Register(this);
    
    }
    public void ChangeZoom(CameraZoomType zoomType)
    {
       
        foreach (var cameraZoomType in cameraZoomTypes) 
        {
            if (cameraZoomType.zoomType == zoomType) 
            {
                targetFov = cameraZoomType.targetFov;
                targetFovSpeed = cameraZoomType.fovSpeed;

                return;
            }
        }
        

    }
    void Update()
    {
        boardCamera.Lens.FieldOfView = Mathf.Lerp(boardCamera.Lens.FieldOfView, targetFov, Time.deltaTime * targetFovSpeed);
    }

}
