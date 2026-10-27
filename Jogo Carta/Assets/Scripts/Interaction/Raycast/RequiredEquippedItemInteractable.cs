using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// "Use o item X aqui": exige um item na mao e dispara eventos (Animator, ativar objetos...).
    /// Pode consumir o item ou coloca-lo no mundo. Sem o item certo, mostra um pensamento de Tomas.
    /// </summary>
    public sealed class RequiredEquippedItemInteractable : RaycastInteractable
    {
        [Header("Requirement")]
        [SerializeField] private InventoryItemDefinition requiredItem;
        [Tooltip("Remove the required item from the inventory after a successful interaction.")]
        [SerializeField] private bool consumeItemOnSuccess;

        [Header("Place equipped item")]
        [Tooltip("Instead of consuming it, removes the equipped object from the inventory and places it under the configured parent.")]
        [SerializeField] private bool placeEquippedItemOnSuccess;
        [Tooltip("Defaults to this interactable's transform when left empty.")]
        [SerializeField] private Transform itemPlacementParent;
        [SerializeField] private Vector3 itemTargetLocalPosition;
        [SerializeField] private Vector3 itemTargetLocalEulerAngles;
        [SerializeField] private Vector3 itemTargetLocalScale = Vector3.one;
        [SerializeField, Min(0f)] private float itemPlacementDuration = 0.45f;
        [SerializeField] private AnimationCurve itemPlacementCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("After success")]
        [Tooltip("Prevents this object from being interacted with again after success.")]
        [SerializeField] private bool disableInteractionOnSuccess = true;
        [Tooltip("Disables every Collider on this object and its children after success.")]
        [SerializeField] private bool disableCollidersOnSuccess;

        [Header("Prompts e pensamentos")]
        [Tooltip("Prompt quando o item certo esta na mao. Vazio = usa o prompt padrao.")]
        [SerializeField] private string promptWithItem;
        [SerializeField, TextArea(1, 3)] private string missingItemThought = "Preciso de alguma coisa para isso.";
        [SerializeField, TextArea(1, 3)] private string wrongItemThought = "Isso não serve aqui.";
        [SerializeField, TextArea(1, 3)] private string successThought;
        [SerializeField] private NoirSfx successSound = NoirSfx.Place;

        [Header("Events")]
        [Tooltip("Called when the required item is equipped.")]
        [SerializeField] private UnityEvent onRequirementMet;
        [Tooltip("Called when no item or the wrong item is equipped.")]
        [SerializeField] private UnityEvent onRequirementNotMet;

        private bool completed;

        public InventoryItemDefinition RequiredItem => requiredItem;
        public bool IsCompleted => completed;

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && !completed && requiredItem != null;
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context) && context.IsEquipped(requiredItem);
        }

        public override string GetPrompt(InteractionContext context)
        {
            if (requiredItem != null && context.IsEquipped(requiredItem) && !string.IsNullOrWhiteSpace(promptWithItem))
                return promptWithItem;

            return InteractionPrompt;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            return context.EquippedItem == null
                ? InteractionFeedback.Blocked(InteractionOutcome.MissingItem, missingItemThought)
                : InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongItemThought);
        }

        public override void OnInteractionBlocked(InteractionContext context)
        {
            base.OnInteractionBlocked(context);
            onRequirementNotMet?.Invoke();
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            if (placeEquippedItemOnSuccess)
            {
                if (!TryPlaceEquippedItem(context.Inventory))
                    return;
            }
            else if (consumeItemOnSuccess && !context.Inventory.Consume(requiredItem))
            {
                return;
            }

            completed = disableInteractionOnSuccess;

            if (disableInteractionOnSuccess)
                SetAvailable(false);

            if (disableCollidersOnSuccess)
                DisableColliders();

            NoirAudio.Play(successSound, 0.9f);
            TomasThoughts.Say(successThought);
            onRequirementMet?.Invoke();
        }

        private bool TryPlaceEquippedItem(InventoryController inventory)
        {
            if (!inventory.TryRemoveAndGetObject(requiredItem, out GameObject itemObject) || itemObject == null)
                return false;

            Transform parent = itemPlacementParent != null ? itemPlacementParent : transform;
            WorldItemPlacement.PrepareForWorld(itemObject, parent);
            WorldItemPlacement.MoveTo(itemObject.transform, itemTargetLocalPosition,
                Quaternion.Euler(itemTargetLocalEulerAngles), itemTargetLocalScale, itemPlacementDuration,
                itemPlacementCurve);
            return true;
        }

        private void DisableColliders()
        {
            foreach (Collider itemCollider in GetComponentsInChildren<Collider>(true))
                itemCollider.enabled = false;
        }

        private void OnValidate()
        {
            if (itemTargetLocalScale == Vector3.zero)
                itemTargetLocalScale = Vector3.one;

            if (placeEquippedItemOnSuccess)
                consumeItemOnSuccess = false;
        }
    }
}
