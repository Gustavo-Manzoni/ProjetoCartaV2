using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraTilt : MonoBehaviour
{

    [SerializeField] Vector3 _maxRotationAmount;
    [SerializeField] float _rotationSpeed;
    CinemachineCamera myCamera;
    public CinemachineCamera MyCamera => myCamera;
    Vector3 _starterRotation;

    /// <summary>Rotacao da camera sem a inclinacao do mouse (a pose "olhando reto" do quadro).</summary>
    public Quaternion BaseRotation => Quaternion.Euler(_starterRotation);

    private void Awake()
    {
        _starterRotation = transform.eulerAngles;
        myCamera = GetComponent<CinemachineCamera>();

    }
    void Update()
    {
        // Mesmo espaco do mousePosition do Input legado (pixels, origem embaixo a esquerda).
        // Sem mouse, mira no centro da tela (sem inclinacao). O Input System so conhece a posicao depois do
        // primeiro movimento (antes le (0,0)): zero conta como "sem leitura", senao a camera abre inclinada.
        Mouse mouse = Mouse.current;
        Vector2 mousePosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if (position != Vector2.zero)
                mousePosition = position;
        }
        Vector2 viewportPos = Camera.main.ScreenToViewportPoint(mousePosition);
        Vector2 normalized = (viewportPos - Vector2.one * 0.5f) * 2f;

        Vector3 tilt = new Vector3(
            _maxRotationAmount.x * -normalized.y,
            _maxRotationAmount.y *  normalized.x,
            0f
        );

        Quaternion target = Quaternion.Euler(_starterRotation + tilt);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * _rotationSpeed);
  
    }
}
