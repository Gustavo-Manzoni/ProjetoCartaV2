using UnityEngine;

namespace ProjetoCarta.Inventory
{
    public abstract class EquippedItemBehaviour : MonoBehaviour, IEquippedItem
    {
        public virtual void OnEquipped(InventoryController inventory, InventoryItemDefinition item) { }
        public virtual void OnUnequipped(InventoryController inventory, InventoryItemDefinition item) { }
        public virtual void OnUse(InventoryController inventory, InventoryItemDefinition item) { }
    }
}
