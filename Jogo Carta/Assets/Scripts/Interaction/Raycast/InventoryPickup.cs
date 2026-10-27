using ProjetoCarta.Core;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace ProjetoCarta.Interaction
{
    public sealed class InventoryPickup : RaycastInteractable
    {
        [SerializeField] private InventoryItemDefinition item;
        [FormerlySerializedAs("objectToRemove")]
        [SerializeField] private GameObject itemObject;
        [SerializeField] private bool disableCollidersOnPickup = true;
        [SerializeField] private bool disablePhysicsOnPickup = true;

        [Header("Pensamento ao pegar (opcional)")]
        [Tooltip("Frase de Tomas mostrada logo depois de pegar o item. Use para dar a proxima pista.")]
        [SerializeField, TextArea(1, 3)] private string pickupThought;

        [SerializeField] private UnityEvent onPickedUp;

        private bool collected;

        public InventoryItemDefinition Item => item;
        public bool IsCollected => collected;
        public event System.Action<InventoryPickup> PickedUp;

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && !collected && item != null && context.Inventory != null &&
                   !context.Inventory.Contains(item);
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context);
        }

        public override string GetPrompt(InteractionContext context)
        {
            string prompt = InteractionPrompt;
            if (string.IsNullOrWhiteSpace(prompt) || prompt == "Interagir")
                prompt = item != null ? Loc.Format("Pegar {0}", Loc.T(item.DisplayName).ToLowerInvariant()) : "Pegar";

            return prompt;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            GameObject collectedObject = itemObject != null ? itemObject : gameObject;
            SetAvailable(false);
            DisableWorldPhysics(collectedObject);

            if (!context.Inventory.TryCollectAndEquip(item, collectedObject))
            {
                SetAvailable(true);
                return;
            }

            collected = true;
            NoirAudio.Play(NoirSfx.Pickup, 0.8f);
            TomasThoughts.Say(pickupThought);
            onPickedUp?.Invoke();
            PickedUp?.Invoke(this);
        }

        private void DisableWorldPhysics(GameObject collectedObject)
        {
            if (disableCollidersOnPickup)
            {
                DisableColliders(gameObject);

                if (collectedObject != gameObject)
                    DisableColliders(collectedObject);
            }

            if (!disablePhysicsOnPickup)
                return;

            DisableRigidbodies(gameObject);

            if (collectedObject != gameObject)
                DisableRigidbodies(collectedObject);
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (Collider itemCollider in root.GetComponentsInChildren<Collider>(true))
                itemCollider.enabled = false;
        }

        private static void DisableRigidbodies(GameObject root)
        {
            foreach (Rigidbody itemRigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
                itemRigidbody.isKinematic = true;
                itemRigidbody.detectCollisions = false;
            }
        }
    }
}
