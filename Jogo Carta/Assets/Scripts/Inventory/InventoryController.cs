using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.Inventory
{
    public sealed class InventoryController : MonoBehaviour
    {
        [Header("Equipment")]
        [SerializeField] private Transform equipmentMount;
        [SerializeField] private Transform storedItemsRoot;

        [Header("Pickup transition")]
        [SerializeField, Min(0f)] private float pickupTransitionDuration = 0.25f;
        [SerializeField] private AnimationCurve pickupTransitionCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Optional starting items without scene objects")]
        [SerializeField] private InventoryItemDefinition[] startingItems;

        private readonly List<InventoryItemDefinition> items = new();
        private readonly List<GameObject> itemObjects = new();
        private GameObject equippedInstance;
        private Coroutine equipTransitionRoutine;

        public event Action InventoryChanged;
        public event Action<InventoryItemDefinition> EquippedItemChanged;

        public IReadOnlyList<InventoryItemDefinition> Items => items;
        public InventoryItemDefinition EquippedItem { get; private set; }
        public GameObject EquippedInstance => equippedInstance;

        private void Awake()
        {
            if (storedItemsRoot == null)
                storedItemsRoot = transform;

            if (startingItems == null)
                return;

            foreach (InventoryItemDefinition item in startingItems)
                TryAdd(item);
        }

        public bool Contains(InventoryItemDefinition item)
        {
            return FindIndex(item) >= 0;
        }

        public bool TryAdd(InventoryItemDefinition item)
        {
            return TryAdd(item, null);
        }

        public bool TryAdd(InventoryItemDefinition item, GameObject sceneObject)
        {
            return TryAddInternal(item, sceneObject, true);
        }

        public bool TryCollectAndEquip(InventoryItemDefinition item, GameObject sceneObject)
        {
            if (sceneObject == null || !TryAddInternal(item, sceneObject, false))
                return false;

            return Equip(item, true);
        }

        private bool TryAddInternal(
            InventoryItemDefinition item,
            GameObject sceneObject,
            bool storeSceneObject)
        {
            if (item == null || Contains(item))
                return false;

            items.Add(item);
            itemObjects.Add(sceneObject);

            if (sceneObject != null && storeSceneObject)
                StoreItemObject(sceneObject);

            InventoryChanged?.Invoke();
            return true;
        }

        public bool Remove(InventoryItemDefinition item)
        {
            int index = FindIndex(item);
            if (index < 0)
                return false;

            InventoryItemDefinition storedItem = items[index];
            if (IsSameItem(EquippedItem, storedItem))
                Equip(null);

            GameObject storedObject = itemObjects[index];
            if (storedObject != null)
                StoreItemObject(storedObject);

            items.RemoveAt(index);
            itemObjects.RemoveAt(index);
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryRemoveAndGetObject(
            InventoryItemDefinition item,
            out GameObject sceneObject)
        {
            sceneObject = null;
            int index = FindIndex(item);
            if (index < 0)
                return false;

            InventoryItemDefinition storedItem = items[index];
            sceneObject = itemObjects[index];
            if (sceneObject == null)
                return false;

            if (IsSameItem(EquippedItem, storedItem))
            {
                UnequipCurrent(false);
                EquippedItemChanged?.Invoke(null);
            }

            items.RemoveAt(index);
            itemObjects.RemoveAt(index);
            InventoryChanged?.Invoke();
            return true;
        }

        public bool Consume(InventoryItemDefinition item)
        {
            int index = FindIndex(item);
            if (index < 0)
                return false;

            InventoryItemDefinition storedItem = items[index];
            if (IsSameItem(EquippedItem, storedItem))
                Equip(null);

            GameObject storedObject = itemObjects[index];
            items.RemoveAt(index);
            itemObjects.RemoveAt(index);

            if (storedObject != null)
            {
                storedObject.SetActive(false);
                Destroy(storedObject);
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public bool Equip(InventoryItemDefinition item)
        {
            return Equip(item, false);
        }

        private bool Equip(InventoryItemDefinition item, bool animateFromWorld)
        {
            int itemIndex = -1;
            if (item != null)
            {
                itemIndex = FindIndex(item);
                if (itemIndex < 0)
                    return false;

                item = items[itemIndex];
            }

            if (IsSameItem(EquippedItem, item))
                return true;

            UnequipCurrent();
            EquippedItem = item;

            if (item != null)
            {
                equippedInstance = itemObjects[itemIndex];
                ShowEquippedObject(item, equippedInstance, animateFromWorld);
                InvokeActions(item, action => action.OnEquipped(this, item, equippedInstance));
                NotifyEquippedBehaviours(item, true);
            }

            EquippedItemChanged?.Invoke(EquippedItem);
            return true;
        }

        public void UseEquippedItem()
        {
            if (EquippedItem == null)
                return;

            InventoryItemDefinition item = EquippedItem;
            InvokeActions(item, action => action.OnUse(this, item, equippedInstance));

            if (equippedInstance == null)
                return;

            foreach (MonoBehaviour behaviour in equippedInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IEquippedItem equippedBehaviour)
                    equippedBehaviour.OnUse(this, item);
            }
        }

        private void UnequipCurrent(bool storeObject = true)
        {
            if (EquippedItem == null)
                return;

            StopEquipTransition();

            InventoryItemDefinition previousItem = EquippedItem;
            GameObject previousInstance = equippedInstance;
            NotifyEquippedBehaviours(previousItem, false);
            InvokeActions(previousItem, action => action.OnUnequipped(this, previousItem, previousInstance));

            if (previousInstance != null && storeObject)
                StoreItemObject(previousInstance);

            equippedInstance = null;
            EquippedItem = null;
        }

        private void ShowEquippedObject(
            InventoryItemDefinition item,
            GameObject sceneObject,
            bool animateFromWorld)
        {
            if (sceneObject == null || equipmentMount == null)
                return;

            StopEquipTransition();
            Transform itemTransform = sceneObject.transform;

            if (animateFromWorld && pickupTransitionDuration > 0f && Application.isPlaying)
            {
                itemTransform.SetParent(equipmentMount, true);
                sceneObject.SetActive(true);
                equipTransitionRoutine = StartCoroutine(AnimateEquippedObject(item, itemTransform));
                return;
            }

            itemTransform.SetParent(equipmentMount, false);
            ApplyEquippedOffsets(item, itemTransform);
            sceneObject.SetActive(true);
        }

        private IEnumerator AnimateEquippedObject(
            InventoryItemDefinition item,
            Transform itemTransform)
        {
            Vector3 startPosition = itemTransform.localPosition;
            Quaternion startRotation = itemTransform.localRotation;
            Vector3 startScale = itemTransform.localScale;
            Quaternion targetRotation = Quaternion.Euler(item.EquippedLocalEulerAngles);
            float elapsed = 0f;

            while (elapsed < pickupTransitionDuration)
            {
                if (itemTransform == null)
                    yield break;

                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / pickupTransitionDuration);
                float amount = pickupTransitionCurve != null
                    ? pickupTransitionCurve.Evaluate(normalizedTime)
                    : normalizedTime;

                itemTransform.localPosition = Vector3.LerpUnclamped(
                    startPosition,
                    item.EquippedLocalPosition,
                    amount);
                itemTransform.localRotation = Quaternion.SlerpUnclamped(
                    startRotation,
                    targetRotation,
                    amount);
                itemTransform.localScale = Vector3.LerpUnclamped(
                    startScale,
                    item.EquippedLocalScale,
                    amount);
                yield return null;
            }

            ApplyEquippedOffsets(item, itemTransform);
            equipTransitionRoutine = null;
        }

        private static void ApplyEquippedOffsets(
            InventoryItemDefinition item,
            Transform itemTransform)
        {
            itemTransform.localPosition = item.EquippedLocalPosition;
            itemTransform.localRotation = Quaternion.Euler(item.EquippedLocalEulerAngles);
            itemTransform.localScale = item.EquippedLocalScale;
        }

        private void StopEquipTransition()
        {
            if (equipTransitionRoutine == null)
                return;

            StopCoroutine(equipTransitionRoutine);
            equipTransitionRoutine = null;
        }

        private void StoreItemObject(GameObject sceneObject)
        {
            sceneObject.SetActive(false);
            sceneObject.transform.SetParent(storedItemsRoot, false);
        }

        private void NotifyEquippedBehaviours(InventoryItemDefinition item, bool equipped)
        {
            if (equippedInstance == null)
                return;

            foreach (MonoBehaviour behaviour in equippedInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is not IEquippedItem equippedBehaviour)
                    continue;

                if (equipped)
                    equippedBehaviour.OnEquipped(this, item);
                else
                    equippedBehaviour.OnUnequipped(this, item);
            }
        }

        private int FindIndex(InventoryItemDefinition item)
        {
            if (item == null)
                return -1;

            for (int i = 0; i < items.Count; i++)
            {
                if (IsSameItem(items[i], item))
                    return i;
            }

            return -1;
        }

        private static bool IsSameItem(InventoryItemDefinition first, InventoryItemDefinition second)
        {
            if (ReferenceEquals(first, second))
                return true;

            return first != null && second != null &&
                   string.Equals(first.Id, second.Id, StringComparison.Ordinal);
        }

        private void InvokeActions(InventoryItemDefinition item, Action<InventoryItemAction> callback)
        {
            if (item.Actions == null)
                return;

            foreach (InventoryItemAction action in item.Actions)
            {
                if (action != null)
                    callback(action);
            }
        }
    }
}
