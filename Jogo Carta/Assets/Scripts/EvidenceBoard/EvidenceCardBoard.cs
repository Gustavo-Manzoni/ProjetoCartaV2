using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class EvidenceCardBoard : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private EvidenceCardDatabase database;
    [SerializeField] private EvidenceCardView cardPrefab;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private Transform firstCardPosition;
    [SerializeField, Min(1)] private int cardsPerRow = 6;
    [SerializeField] private Vector3 columnSpacing = new(0.18f, 0f, 0f);
    [SerializeField] private Vector3 rowSpacing = new(0f, 0f, -0.25f);

    [Header("World interaction")]
    [SerializeField] private Camera renderingCamera;
    [SerializeField] private TableCameraMouseSwitcher cameraSwitcher;
    [SerializeField] private LayerMask cardLayers;
    [SerializeField] private LayerMask tableLayers;
    [SerializeField] private LayerMask slotLayers;
    [SerializeField] private float rayDistance = 100f;
    [SerializeField] private float dragHeight = 0.03f;

    [Header("Temporary witness test keys")]
    [SerializeField] private string[] testWitnessIds =
    {
        "testemunha_1",
        "testemunha_2",
        "testemunha_3",
        "testemunha_4",
        "testemunha_5"
    };

    private readonly List<EvidenceCardView> generatedCards = new();
    private readonly HashSet<string> unlockedWitnesses = new();
    private readonly RaycastHit[] slotHits = new RaycastHit[16];
    private EvidenceCardView draggedCard;
    private EvidenceCardView hoveredCard;
    private EvidenceCardSlot hoveredSlot;
    private Quaternion dragRotation;

    private void Awake()
    {
        if (renderingCamera == null)
            renderingCamera = Camera.main;
        if (cardContainer == null)
            cardContainer = transform;
        if (firstCardPosition == null)
            firstCardPosition = transform;
    }

    private void Update()
    {
        if (Keyboard.current != null)
            HandleTestWitnessKeys();

        if (Mouse.current == null || renderingCamera == null)
            return;

        if (!Application.isFocused)
        {
            ClearCardHover();
            ClearSlotHover();
            CancelDrag();
            return;
        }

        if (cameraSwitcher != null && !cameraSwitcher.IsLookingAtTable)
        {
            ClearCardHover();
            ClearSlotHover();
            CancelDrag();
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (draggedCard == null && Mouse.current.leftButton.wasPressedThisFrame)
            TryBeginDrag(mousePosition);

        if (draggedCard != null)
        {
            UpdateDrag(mousePosition);

            if (Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag(mousePosition);
        }
        else
        {
            UpdateCardHover(mousePosition);
        }
    }

    public void UnlockWitness(string witnessId)
    {
        if (database == null || cardPrefab == null || firstCardPosition == null)
        {
            Debug.LogError("EvidenceCardBoard is missing its database, prefab, or first-card transform.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(witnessId) || !unlockedWitnesses.Add(witnessId))
            return;

        int spawnedThisBatch = 0;
        foreach (EvidenceCardDefinition definition in database.Cards)
        {
            if (definition == null || definition.WitnessId != witnessId ||
                generatedCards.Exists(card => card != null && card.Definition.Id == definition.Id))
                continue;

            int index = generatedCards.Count;
            int column = index % cardsPerRow;
            int row = index / cardsPerRow;
            Vector3 position = firstCardPosition.position
                + firstCardPosition.rotation * columnSpacing * column
                + firstCardPosition.rotation * rowSpacing * row;

            EvidenceCardView card = Instantiate(cardPrefab, position, firstCardPosition.rotation, cardContainer);
            card.Initialize(definition, position, firstCardPosition.rotation, spawnedThisBatch * 0.035f);
            generatedCards.Add(card);
            spawnedThisBatch++;
        }
    }

    private void TryBeginDrag(Vector2 mousePosition)
    {
        Ray ray = renderingCamera.ScreenPointToRay(mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, cardLayers, QueryTriggerInteraction.Collide))
            return;

        EvidenceCardView card = hit.collider.GetComponentInParent<EvidenceCardView>();
        if (card == null)
            return;

        draggedCard = card;
        dragRotation = card.transform.rotation;
        ClearCardHover();
        card.BeginDrag();
        cameraSwitcher?.SetSwitchingBlocked(true);
    }

    private void UpdateDrag(Vector2 mousePosition)
    {
        Ray ray = renderingCamera.ScreenPointToRay(mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, tableLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 position = hit.point + hit.normal * dragHeight;
            draggedCard.SetDragPosition(position, dragRotation);
        }

        SetHoveredSlot(FindSlot(mousePosition));
    }

    private void EndDrag(Vector2 mousePosition)
    {
        EvidenceCardSlot slot = FindSlot(mousePosition);
        ClearSlotHover();

        if (slot != null && slot.TryPlace(draggedCard))
        {
            FinishDrag();
            return;
        }

        draggedCard.ReleaseOnTable();
        FinishDrag();
    }

    private EvidenceCardSlot FindSlot(Vector2 mousePosition)
    {
        Ray ray = renderingCamera.ScreenPointToRay(mousePosition);
        int hitCount = Physics.RaycastNonAlloc(ray, slotHits, rayDistance, slotLayers, QueryTriggerInteraction.Collide);
        EvidenceCardSlot closestSlot = null;
        float closestDistance = float.PositiveInfinity;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = slotHits[index];
            EvidenceCardSlot slot = hit.collider.GetComponentInParent<EvidenceCardSlot>();
            if (slot != null && slot.IsEmpty && hit.distance < closestDistance)
            {
                closestSlot = slot;
                closestDistance = hit.distance;
            }
        }

        return closestSlot;
    }

    private void CancelDrag()
    {
        if (draggedCard == null)
            return;

        draggedCard.ReleaseOnTable();
        ClearSlotHover();
        FinishDrag();
    }

    private void FinishDrag()
    {
        draggedCard = null;
        cameraSwitcher?.SetSwitchingBlocked(false);
    }

    private void HandleTestWitnessKeys()
    {
        if (testWitnessIds == null)
            return;

        TryUnlockTestWitness(0, Keyboard.current.digit1Key.wasPressedThisFrame);
        TryUnlockTestWitness(1, Keyboard.current.digit2Key.wasPressedThisFrame);
        TryUnlockTestWitness(2, Keyboard.current.digit3Key.wasPressedThisFrame);
        TryUnlockTestWitness(3, Keyboard.current.digit4Key.wasPressedThisFrame);
        TryUnlockTestWitness(4, Keyboard.current.digit5Key.wasPressedThisFrame);
    }

    private void TryUnlockTestWitness(int index, bool wasPressedThisFrame)
    {
        if (wasPressedThisFrame && index < testWitnessIds.Length)
            UnlockWitness(testWitnessIds[index]);
    }

    private void UpdateCardHover(Vector2 mousePosition)
    {
        Ray ray = renderingCamera.ScreenPointToRay(mousePosition);
        EvidenceCardView newHoveredCard = null;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, cardLayers, QueryTriggerInteraction.Collide))
            newHoveredCard = hit.collider.GetComponentInParent<EvidenceCardView>();

        if (newHoveredCard == hoveredCard)
            return;

        ClearCardHover();
        hoveredCard = newHoveredCard;
        hoveredCard?.SetHovered(true);
    }

    private void ClearCardHover()
    {
        if (hoveredCard == null)
            return;

        hoveredCard.SetHovered(false);
        hoveredCard = null;
    }

    private void SetHoveredSlot(EvidenceCardSlot slot)
    {
        if (hoveredSlot == slot)
            return;

        ClearSlotHover();
        hoveredSlot = slot;
        hoveredSlot?.SetHovered(true);
    }

    private void ClearSlotHover()
    {
        if (hoveredSlot == null)
            return;

        hoveredSlot.SetHovered(false);
        hoveredSlot = null;
    }

    private void OnDisable()
    {
        ClearCardHover();
        ClearSlotHover();
        if (draggedCard != null)
            FinishDrag();
        else
            cameraSwitcher?.SetSwitchingBlocked(false);
    }
}
