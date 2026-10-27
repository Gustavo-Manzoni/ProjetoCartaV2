namespace ProjetoCarta.Inventory
{
    public interface IEquippedItem
    {
        void OnEquipped(InventoryController inventory, InventoryItemDefinition item);
        void OnUnequipped(InventoryController inventory, InventoryItemDefinition item);
        void OnUse(InventoryController inventory, InventoryItemDefinition item);
    }
}
