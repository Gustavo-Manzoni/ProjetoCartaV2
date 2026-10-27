using UnityEngine;
using Unity.Cinemachine;
public class CameraTiltManager : MonoBehaviour
{

    CameraTilt currentCamera;
    private void Awake()
    {
        ServiceLocator.Register(this);
        
    }
    public void ChangeCamera(CameraTilt to) 
    {
        if(currentCamera != null) 
            currentCamera.enabled = false;

        to.enabled = true;
        currentCamera = to;

        to.MyCamera.Prioritize();
    }


}
