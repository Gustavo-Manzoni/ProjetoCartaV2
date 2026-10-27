using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Objeto que nao muda de estado, mas que Tomas comenta ao ser examinado (relogio parado, cadeira...).
    /// Serve para reforcar pistas sem criar puzzle novo. O pensamento pode mudar depois de um evento
    /// (ex.: a cadeira depois de desligada) via <see cref="UseAlternateThought"/>.
    /// </summary>
    public sealed class InspectableObject : RaycastInteractable
    {
        [SerializeField, TextArea(1, 3)] private string thought;
        [SerializeField, TextArea(1, 3)] private string alternateThought;
        [SerializeField] private UnityEvent onInspected;

        private bool useAlternate;

        public override string GetPrompt(InteractionContext context)
        {
            string prompt = InteractionPrompt;
            return string.IsNullOrWhiteSpace(prompt) || prompt == "Interagir" ? "Examinar" : prompt;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            TomasThoughts.Say(useAlternate && !string.IsNullOrWhiteSpace(alternateThought) ? alternateThought : thought,
                InteractionOutcome.NotPossible);
            onInspected?.Invoke();
        }

        public void UseAlternateThought()
        {
            useAlternate = true;
        }
    }
}
