using UnityEngine;

namespace ProjetoCarta.Inventory
{
    public abstract class InventoryItemAction : ScriptableObject
    {
        public virtual void OnEquipped(InventoryController inventory, InventoryItemDefinition item, GameObject instance) { }
        public virtual void OnUnequipped(InventoryController inventory, InventoryItemDefinition item, GameObject instance) { }
        public virtual void OnUse(InventoryController inventory, InventoryItemDefinition item, GameObject instance) { }
    }
}
