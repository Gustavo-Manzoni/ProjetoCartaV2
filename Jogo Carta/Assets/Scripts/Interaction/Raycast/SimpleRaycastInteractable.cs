using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Interaction
{
    public sealed class SimpleRaycastInteractable : RaycastInteractable
    {
        [SerializeField] private bool interactOnlyOnce;
        [SerializeField] private UnityEvent onInteract;

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            onInteract?.Invoke();

            if (interactOnlyOnce)
                SetAvailable(false);
        }
    }
}
