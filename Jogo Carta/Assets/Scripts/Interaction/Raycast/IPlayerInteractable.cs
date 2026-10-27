using ProjetoCarta.Inventory;

namespace ProjetoCarta.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(PlayerRaycastInteractor interactor, InventoryController inventory)
        {
            Interactor = interactor;
            Inventory = inventory;
        }

        public PlayerRaycastInteractor Interactor { get; }
        public InventoryController Inventory { get; }

        public InventoryItemDefinition EquippedItem => Inventory != null ? Inventory.EquippedItem : null;

        public bool IsEquipped(InventoryItemDefinition item)
        {
            InventoryItemDefinition equipped = EquippedItem;
            return item != null && equipped != null &&
                   (ReferenceEquals(item, equipped) ||
                    string.Equals(item.Id, equipped.Id, System.StringComparison.Ordinal));
        }
    }

    public interface IPlayerInteractable
    {
        string InteractionPrompt { get; }
        /// <summary>O objeto merece foco (mostra prompt) mesmo que a acao ainda nao seja possivel.</summary>
        bool IsFocusable(InteractionContext context);
        /// <summary>O clique agora realmente executa a acao.</summary>
        bool CanInteract(InteractionContext context);
        string GetPrompt(InteractionContext context);
        void Interact(InteractionContext context);
        InteractionFeedback GetBlockedFeedback(InteractionContext context);
        void OnFocusEntered(InteractionContext context);
        void OnFocusExited(InteractionContext context);
    }
}
