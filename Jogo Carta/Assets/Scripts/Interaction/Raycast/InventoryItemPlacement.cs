using System;
using ProjetoCarta.Core;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Encaixe que recebe um item da mao, guarda-o no mundo como filho deste objeto (ou de um parent opcional) e
    /// devolve o item a mao se o jogador clicar de novo. O encaixe nao decide se esta certo: <see cref="RequiredItem"/>
    /// e so a posicao correta, e quem junta os encaixes (RitualPanel) decide quando o conjunto esta completo.
    /// </summary>
    public sealed class InventoryItemPlacement : RaycastInteractable
    {
        [Header("Required item")]
        [Tooltip("Item CORRETO desta posicao. Outros itens aceitos pelo painel podem ocupar o encaixe, mas so este conta como certo.")]
        [SerializeField] private InventoryItemDefinition requiredItem;

        [Header("Placement")]
        [Tooltip("Defaults to this interactable's transform when left empty.")]
        [SerializeField] private Transform placementParent;
        [SerializeField] private Vector3 targetLocalPosition;
        [SerializeField] private Vector3 targetLocalEulerAngles;
        [SerializeField] private Vector3 targetLocalScale = Vector3.one;
        [Tooltip("Se maior que zero, ignora a escala acima e ajusta o objeto para o maior lado medir isto (metros).")]
        [SerializeField, Min(0f)] private float fitWorldSize;
        [SerializeField, Min(0f)] private float interpolationDuration = 0.45f;
        [SerializeField] private AnimationCurve interpolationCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Prompts e pensamentos")]
        [Tooltip("Prompt quando o item correto esta na mao. Vazio = \"Colocar <item>\".")]
        [SerializeField] private string placePrompt;
        [Tooltip("Prompt quando o encaixe esta vazio e a mao nao tem um item aceito.")]
        [SerializeField] private string emptyPrompt = "Encaixe vazio";
        [SerializeField, TextArea(1, 3)] private string missingItemThought = "Algo deveria ser encaixado aqui.";
        [SerializeField, TextArea(1, 3)] private string wrongItemThought = "Esse objeto não pertence a esta posição.";

        [Header("Consequence")]
        [Tooltip("Chamado quando um item termina de entrar no encaixe.")]
        [SerializeField] private UnityEvent onPlaced;
        [Tooltip("Chamado quando o jogador tira o item do encaixe.")]
        [SerializeField] private UnityEvent onRemoved;

        private InventoryItemDefinition occupant;
        private GameObject occupantObject;
        private bool moving;
        private bool locked;

        public InventoryItemDefinition RequiredItem => requiredItem;
        /// <summary>Item que esta no encaixe agora (null se vazio). Pode estar errado.</summary>
        public InventoryItemDefinition Occupant => occupant;
        public bool IsOccupied => occupant != null;
        /// <summary>Tem o item correto desta posicao.</summary>
        public bool IsCorrect => occupant != null && SameItem(occupant, requiredItem);

        /// <summary>
        /// Quais itens este encaixe aceita. O painel liga aqui a regra do conjunto (qualquer item do ritual).
        /// Sem ele, so o item correto cabe.
        /// </summary>
        public Func<InventoryItemDefinition, bool> AcceptsItem { get; set; }

        /// <summary>
        /// Encaixe cuja pose (rotacao e tamanho) vale para o item colocado aqui. O painel aponta para o encaixe
        /// correto do item, para ele ficar na propria orientacao em qualquer posicao. Sem ele, usa a deste encaixe.
        /// </summary>
        public Func<InventoryItemDefinition, InventoryItemPlacement> PoseSourceFor { get; set; }

        /// <summary>Disparado quando o item termina de entrar no encaixe (ja ocupado).</summary>
        public event Action<InventoryItemPlacement> Placed;
        /// <summary>Disparado quando o jogador tira o item do encaixe.</summary>
        public event Action<InventoryItemPlacement> Removed;

        /// <summary>Trava o encaixe: o que esta nele fica onde esta (usado quando o painel fica completo).</summary>
        public void Lock()
        {
            locked = true;
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && requiredItem != null && !locked;
        }

        public override bool CanInteract(InteractionContext context)
        {
            if (!IsFocusable(context) || moving || context.Inventory == null)
                return false;

            // Ocupado: sempre pode tirar o item de volta (mesmo que esteja certo, ate o painel completar).
            if (IsOccupied)
                return true;

            return context.EquippedItem != null && Accepts(context.EquippedItem) &&
                   context.Inventory.EquippedInstance != null;
        }

        public override string GetPrompt(InteractionContext context)
        {
            if (IsOccupied)
                // Traduz molde e nome antes de montar: a frase montada nunca e chave da tabela.
                return Loc.Format("Retirar {0}", Loc.T(occupant.DisplayName).ToLowerInvariant());

            if (context.EquippedItem != null && Accepts(context.EquippedItem))
            {
                if (context.IsEquipped(requiredItem))
                    return Pick(placePrompt, Loc.Format("Colocar {0}", Loc.T(requiredItem.DisplayName).ToLowerInvariant()));

                return Loc.Format("Colocar {0}", Loc.T(context.EquippedItem.DisplayName).ToLowerInvariant());
            }

            return emptyPrompt;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            if (moving)
                return InteractionFeedback.None;

            if (IsOccupied)
                return InteractionFeedback.Blocked(InteractionOutcome.NotPossible, null);

            return context.EquippedItem == null
                ? InteractionFeedback.Blocked(InteractionOutcome.MissingItem, missingItemThought)
                : InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongItemThought);
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            if (IsOccupied)
                TakeBack(context);
            else
                Place(context);
        }

        private void Place(InteractionContext context)
        {
            InventoryItemDefinition item = context.EquippedItem;
            if (!context.Inventory.TryRemoveAndGetObject(item, out GameObject itemObject) || itemObject == null)
                return;

            occupant = item;
            occupantObject = itemObject;
            moving = true;

            Transform parent = PlacementParent;
            WorldItemPlacement.PrepareForWorld(itemObject, parent);

            // A pose vem do encaixe correto do item: a mesma orientacao no mundo e o mesmo tamanho que ele
            // teria la, so que na posicao deste encaixe.
            InventoryItemPlacement source = PoseSourceFor?.Invoke(item);
            if (source == null)
                source = this;

            Transform sourceParent = source.PlacementParent;
            Quaternion worldRotation = sourceParent.rotation * Quaternion.Euler(source.targetLocalEulerAngles);
            Quaternion localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;

            Vector3 scale;
            if (source.fitWorldSize > 0f)
            {
                scale = WorldItemPlacement.ScaleToFit(itemObject, source.fitWorldSize, localRotation, parent);
            }
            else
            {
                float parentScale = Mathf.Abs(parent.lossyScale.x);
                scale = parentScale > 0.000001f
                    ? source.targetLocalScale * (Mathf.Abs(sourceParent.lossyScale.x) / parentScale)
                    : source.targetLocalScale;
            }

            WorldItemPlacement.MoveTo(itemObject.transform, targetLocalPosition, localRotation, scale,
                interpolationDuration, interpolationCurve, OnPlacementFinished);
        }

        private Transform PlacementParent => placementParent != null ? placementParent : transform;

        private void TakeBack(InteractionContext context)
        {
            InventoryItemDefinition item = occupant;
            GameObject itemObject = occupantObject;

            // Se o inventario recusar (nao deveria acontecer), o item continua no encaixe.
            if (!context.Inventory.TryAdd(item, itemObject))
                return;

            occupant = null;
            occupantObject = null;
            context.Inventory.Equip(item);
            NoirAudio.Play(NoirSfx.Pickup, 0.6f);
            onRemoved?.Invoke();
            Removed?.Invoke(this);
        }

        private void OnPlacementFinished()
        {
            moving = false;
            NoirAudio.Play(NoirSfx.Place, 0.9f);
            onPlaced?.Invoke();
            Placed?.Invoke(this);
        }

        private bool Accepts(InventoryItemDefinition item)
        {
            if (item == null || requiredItem == null)
                return false;

            return AcceptsItem != null ? AcceptsItem(item) : SameItem(item, requiredItem);
        }

        private static bool SameItem(InventoryItemDefinition first, InventoryItemDefinition second)
        {
            if (ReferenceEquals(first, second))
                return true;

            return first != null && second != null && string.Equals(first.Id, second.Id, StringComparison.Ordinal);
        }

        private void OnValidate()
        {
            if (targetLocalScale == Vector3.zero)
                targetLocalScale = Vector3.one;
        }
    }
}
