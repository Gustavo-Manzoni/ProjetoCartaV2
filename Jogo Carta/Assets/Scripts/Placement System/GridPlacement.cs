using UnityEngine;

public class GridPlacement : MonoBehaviour
{
    public static GridPlacement Instance { get; private set; }

    [SerializeField] float _gridSize = 0.5f;

    void Awake()
    {
        Instance = this;
    }

    public Vector3 Snap(Vector3 position)
    {
        position.x = Mathf.Round(position.x / _gridSize) * _gridSize;
        position.z = Mathf.Round(position.z / _gridSize) * _gridSize;
        return position;
    }
}
