using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EvidenceCardDatabase", menuName = "Detective/Evidence Card Database")]
public sealed class EvidenceCardDatabase : ScriptableObject
{
    [SerializeField] private List<EvidenceCardDefinition> cards = new();

    public IReadOnlyList<EvidenceCardDefinition> Cards => cards;

    private void OnValidate()
    {
        HashSet<string> ids = new();

        foreach (EvidenceCardDefinition card in cards)
        {
            if (card == null)
                continue;

            if (string.IsNullOrWhiteSpace(card.Id))
                Debug.LogWarning("Evidence card found without an Id.", this);
            else if (!ids.Add(card.Id))
                Debug.LogWarning($"Duplicate evidence card Id: {card.Id}", this);

            if (string.IsNullOrWhiteSpace(card.WitnessId))
                Debug.LogWarning($"Evidence card '{card.Id}' has no Witness Id.", this);
        }
    }
}

[Serializable]
public sealed class EvidenceCardDefinition
{
    [Tooltip("Unique and stable identifier, for example: witness-1-strangled.")]
    [SerializeField] private string id;

    [TextArea(1, 4)]
    [SerializeField] private string text;

    [Tooltip("Identifies which testimony unlocks this fragment.")]
    [SerializeField] private string witnessId;

    [Tooltip("Fragments with the same clue key describe the same fact, even when their displayed text differs.")]
    [SerializeField] private string clueKey;

    public string Id => id;
    public string Text => text;
    public string WitnessId => witnessId;
    public string ClueKey => clueKey;
}
