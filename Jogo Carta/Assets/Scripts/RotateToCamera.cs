using UnityEngine;

public class RotateToCamera : MonoBehaviour
{
   Transform targetCamera;
   [SerializeField] Vector3 rotationOffset;
    void Start()
    {
        if (Camera.main != null)
            targetCamera = Camera.main.transform;
    }


    void Update()
    {
       if (targetCamera == null)
           return;

       transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.position, Vector3.up);
    }
}
