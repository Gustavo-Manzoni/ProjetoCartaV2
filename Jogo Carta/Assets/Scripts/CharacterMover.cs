using UnityEngine;

// ================================================================
// CharacterMover
// ----------------------------------------------------------------
// Movimentação simples por destino, SEM física (não usa Rigidbody)
// e sem input direto (nada de WASD aqui). De outro lugar do seu
// jogo (IA, clique no chão, waypoint, diálogo, etc) você só chama:
//
//     mover.SetDestination(pontoNoMundo);
//
// e o personagem anda até lá na velocidade definida em moveSpeed.
// ================================================================

public class CharacterMover : MonoBehaviour
{
    public enum FacingMode
    {
        None,          
        RotateY,       // gira o personagem no eixo Y pra encarar a direção (bom pra jogos com câmera livre)
        FlipSpriteX    // não gira; só espelha o sprite (localScale.x) — bom pra câmera fixa tipo 2.5D
    }

    [Header("Movimento")]
    public float moveSpeed = 3f;
    [Tooltip("Distância mínima até o destino pra considerar que chegou")]
    public float stoppingDistance = 0.05f;

    [Header("Orientação")]
    public FacingMode facingMode = FacingMode.RotateY;
    public float rotationSpeed = 720f; // graus/seg, só usado em RotateY
    [Tooltip("Transform que será espelhado no modo FlipSpriteX. Se vazio, usa o próprio objeto.")]
    public Transform visualRoot;

    private Vector3 destination;
    private bool hasDestination;

    public bool IsMoving { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }

    void Awake()
    {
        destination = transform.position;
    }

  
    public void SetDestination(Vector3 point    )
    {
        point.y = transform.position.y;
        destination = point;
        hasDestination = true;
    }
 [SerializeField] Transform destin;
    [ContextMenu("Ir para destino")]
    public void GoToDestin()
    {
        SetDestination(destin.position);
    }
       public void Stop()
    {
        hasDestination = false;
        destination = transform.position;
        IsMoving = false;
        CurrentVelocity = Vector3.zero;
    }

    void Update()
    {
        if (!hasDestination)
        {
            IsMoving = false;
            CurrentVelocity = Vector3.zero;
            return;
        }

        Vector3 toDestination = destination - transform.position;
        toDestination.y = 0f;
        float distance = Vector3.Distance(transform.position, destination);

        if (distance <= stoppingDistance)
        {
            hasDestination = false;
            IsMoving = false;
            CurrentVelocity = Vector3.zero;
            return;
        }

        Vector3 direction = toDestination.normalized;
        float step = moveSpeed * Time.deltaTime;

        Vector3 newPos = (step >= distance)
            ? destination
            : transform.position + direction * step;

        transform.position = newPos;
        CurrentVelocity = direction * moveSpeed;
        IsMoving = true;

        ApplyFacing(direction);
    }

    void ApplyFacing(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        switch (facingMode)
        {
            case FacingMode.RotateY:
                Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                break;

            case FacingMode.FlipSpriteX:
                Transform t = visualRoot != null ? visualRoot : transform;
                Vector3 scale = t.localScale;
                float sign = direction.x >= 0f ? 1f : -1f;
                scale.x = Mathf.Abs(scale.x) * sign;
                t.localScale = scale;
                break;

            case FacingMode.None:
            default:
                break;
        }
    }
}