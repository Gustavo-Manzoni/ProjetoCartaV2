using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Porta da camara (P2). Fica travada enquanto a cadeira eletrica estiver ligada; depois de desligada
    /// pela caixa de controle, abrir a porta dispara o final (uma unica vez).
    /// </summary>
    public sealed class FinalDoor : RaycastInteractable
    {
        [SerializeField] private PiscadaFinale finale;
        [Tooltip("Folha da porta que gira ao abrir.")]
        [SerializeField] private Transform doorLeaf;
        [SerializeField] private Vector3 openLocalEulerOffset = new(0f, -95f, 0f);

        [Header("Prompts e pensamentos")]
        [SerializeField] private string lockedPrompt = "Porta da câmara";
        [SerializeField] private string openPrompt = "Abrir a porta";
        [SerializeField] private InventoryItemDefinition chamberKey;
        [SerializeField, TextArea(1, 2)] private string lockedThought = "Trancada. A trava parece ligada ao sistema da cadeira.";
        [SerializeField, TextArea(1, 2)] private string keyHereThought = "A chave não entra aqui. Ela deve abrir outra coisa nesta sala.";
        [SerializeField, TextArea(1, 2)] private string unlockedThought;

        private bool unlocked;
        private bool opened;

        public bool IsUnlocked => unlocked;
        public Transform DoorLeaf => doorLeaf;
        public Vector3 OpenLocalEulerOffset => openLocalEulerOffset;

        public void Unlock()
        {
            if (unlocked)
                return;

            unlocked = true;
            NoirAudio.Play(NoirSfx.Unlock, 1f, 0.8f);
            TomasThoughts.Say(unlockedThought);
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && !opened;
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context) && unlocked && finale != null;
        }

        public override string GetPrompt(InteractionContext context)
        {
            return unlocked ? openPrompt : lockedPrompt;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            if (unlocked)
                return InteractionFeedback.None;

            if (chamberKey != null && context.IsEquipped(chamberKey))
                return InteractionFeedback.Blocked(InteractionOutcome.WrongItem, keyHereThought);

            return InteractionFeedback.Blocked(InteractionOutcome.PrerequisiteMissing, lockedThought);
        }

        public override void OnInteractionBlocked(InteractionContext context)
        {
            base.OnInteractionBlocked(context);
            if (!unlocked)
                NoirAudio.Play(NoirSfx.Locked, 0.9f, 0.85f);
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            opened = true;
            SetAvailable(false);
            finale.Play(this);
        }
    }
}
