using UnityEngine;

namespace ProjetoCarta.Inventory
{
    [CreateAssetMenu(fileName = "New Inventory Item", menuName = "Projeto Carta/Inventory/Item")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;

        [Header("Radial inventory presentation")]
        [Tooltip("Pixel offset from the center of this item's radial segment.")]
        [SerializeField] private Vector2 iconPositionOffset;
        [SerializeField] private Vector3 iconLocalEulerAngles;
        [SerializeField] private Vector3 iconLocalScale = Vector3.one;

        [Header("Equipped object offsets")]
        [SerializeField] private Vector3 equippedLocalPosition;
        [SerializeField] private Vector3 equippedLocalEulerAngles;
        [SerializeField] private Vector3 equippedLocalScale = Vector3.one;

        [Header("Optional item logic")]
        [SerializeField] private InventoryItemAction[] actions;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Vector2 IconPositionOffset => iconPositionOffset;
        public Vector3 IconLocalEulerAngles => iconLocalEulerAngles;
        public Vector3 IconLocalScale => iconLocalScale;
        public Vector3 EquippedLocalPosition => equippedLocalPosition;
        public Vector3 EquippedLocalEulerAngles => equippedLocalEulerAngles;
        public Vector3 EquippedLocalScale => equippedLocalScale;
        public InventoryItemAction[] Actions => actions;

        private void OnValidate()
        {
            if (iconLocalScale == Vector3.zero)
                iconLocalScale = Vector3.one;

            if (equippedLocalScale == Vector3.zero)
                equippedLocalScale = Vector3.one;
        }
    }
}
