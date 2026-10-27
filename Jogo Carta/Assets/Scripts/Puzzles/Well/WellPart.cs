using ProjetoCarta.Interaction;
using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Ponto especifico do poco com interacao propria: o gancho da corda (onde o balde e preso) e a ponta do eixo
    /// do guincho (onde a manivela encaixa). Repassa tudo ao <see cref="WellController"/>; quando o ponto ja foi
    /// usado, deixa de receber foco e o clique cai no corpo do poco.
    /// </summary>
    public sealed class WellPart : RaycastInteractable
    {
        [SerializeField] private WellController well;
        [SerializeField] private WellController.PartRole role;

        public WellController.PartRole Role => role;

        private void Awake()
        {
            if (well == null)
                well = GetComponentInParent<WellController>();
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && well != null && well.IsPartFocusable(role, context);
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context) && well.CanUsePart(role, context);
        }

        public override string GetPrompt(InteractionContext context)
        {
            return well != null ? well.GetPartPrompt(role, context) : InteractionPrompt;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            return well != null ? well.GetPartFeedback(role, context) : InteractionFeedback.None;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            well.UsePart(role, context);
            context.Interactor?.RefreshPrompt(this);
        }
    }
}
