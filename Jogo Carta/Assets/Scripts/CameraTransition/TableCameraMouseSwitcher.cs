using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TableCameraMouseSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera initialCamera;
    [SerializeField] private CinemachineCamera tableCamera;

    [Header("Screen activation zones")]
    [SerializeField, Range(0.01f, 0.5f)] private float bottomZoneHeight = 0.15f;
    [SerializeField, Range(0.01f, 0.5f)] private float topZoneHeight = 0.15f;

    [Header("Priorities")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    private bool isLookingAtTable;
    private bool switchingBlocked;

    public bool IsLookingAtTable => isLookingAtTable;
    public bool IsLookingForward => !isLookingAtTable;

    private void Awake()
    {
        LookForward();
    }

    private void Update()
    {
        if (switchingBlocked || !Application.isFocused || Mouse.current == null || Screen.height <= 0)
            return;

        float normalizedMouseY = Mouse.current.position.ReadValue().y / Screen.height;

        if (!isLookingAtTable && normalizedMouseY <= bottomZoneHeight)
        {
            LookAtTable();
        }
        else if (isLookingAtTable && normalizedMouseY >= 1f - topZoneHeight)
        {
            LookForward();
        }
    }

    public void LookAtTable()
    {
        SetCameraPriorities(tableCamera, initialCamera);
        isLookingAtTable = true;
    }

    public void LookForward()
    {
        SetCameraPriorities(initialCamera, tableCamera);
        isLookingAtTable = false;
    }

    public void SetSwitchingBlocked(bool blocked)
    {
        switchingBlocked = blocked;
    }

    private void SetCameraPriorities(CinemachineCamera activeCamera, CinemachineCamera inactiveCamera)
    {
        if (activeCamera != null)
            activeCamera.Priority = activePriority;

        if (inactiveCamera != null)
            inactiveCamera.Priority = inactivePriority;
    }

    private void OnValidate()
    {
        if (activePriority <= inactivePriority)
            activePriority = inactivePriority + 1;
    }
}
