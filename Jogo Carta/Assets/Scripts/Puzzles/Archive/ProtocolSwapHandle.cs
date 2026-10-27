using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Alavanca de latao entre dois espacos da <see cref="ProtocolTray"/>: o clique troca os dois documentos de lugar.
    /// Some do foco quando a bandeja fica em ordem.
    /// </summary>
    public sealed class ProtocolSwapHandle : RaycastInteractable
    {
        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

        [SerializeField] private ProtocolTray tray;
        [Tooltip("Espaco da esquerda (0 = I). Troca com o espaco seguinte.")]
        [SerializeField, Min(0)] private int leftSlot;

        [Header("Prompt")]
        [SerializeField] private string swapPrompt = "Trocar os documentos {0} e {1} de lugar";

        public int LeftSlot => leftSlot;

        private void Awake()
        {
            if (tray == null)
                tray = GetComponentInParent<ProtocolTray>();
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && tray != null && !tray.IsSolved;
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context) && tray.CanSwap(leftSlot, leftSlot + 1);
        }

        public override string GetPrompt(InteractionContext context)
        {
            // Os numerais romanos sao iguais nos dois idiomas: so o molde e traduzido.
            return Loc.Format(swapPrompt, Numeral(leftSlot), Numeral(leftSlot + 1));
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            // So bloqueia no meio de uma troca: sem mensagem.
            return InteractionFeedback.None;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            tray.Swap(leftSlot, leftSlot + 1);
            context.Interactor?.RefreshPrompt(this);
        }

        private static string Numeral(int index)
        {
            return index >= 0 && index < Numerals.Length ? Numerals[index] : (index + 1).ToString();
        }
    }
}
