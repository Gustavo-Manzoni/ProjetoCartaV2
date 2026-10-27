using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Base de todo objeto interagivel em primeira pessoa.
    /// <para><see cref="IsFocusable"/>: o objeto recebe a mira e mostra prompt.</para>
    /// <para><see cref="CanInteract"/>: o clique executa a acao agora.</para>
    /// Quando o objeto tem foco mas a acao nao e possivel, o clique mostra um pensamento de Tomas
    /// (<see cref="GetBlockedFeedback"/>) em vez de nao fazer nada.
    /// </summary>
    public abstract class RaycastInteractable : MonoBehaviour, IPlayerInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Interagir";
        [SerializeField] private bool available = true;

        [Header("Pensamento de Tomás")]
        [Tooltip("Frase curta mostrada quando o jogador tenta usar o objeto e nada pode ser feito. Vazio = sem mensagem.")]
        [SerializeField, TextArea(1, 3)] private string blockedThought;

        [Header("Optional focus callbacks")]
        [SerializeField] private UnityEvent onFocusEntered;
        [SerializeField] private UnityEvent onFocusExited;

        public virtual string InteractionPrompt => interactionPrompt;
        public bool IsAvailable => available;
        protected string BlockedThought => blockedThought;

        public virtual bool IsFocusable(InteractionContext context)
        {
            return available && isActiveAndEnabled;
        }

        public virtual bool CanInteract(InteractionContext context)
        {
            return available && isActiveAndEnabled;
        }

        public virtual string GetPrompt(InteractionContext context)
        {
            return InteractionPrompt;
        }

        public abstract void Interact(InteractionContext context);

        public virtual InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            return InteractionFeedback.Blocked(InteractionOutcome.NotPossible, blockedThought);
        }

        /// <summary>Chamado pelo interactor quando o jogador clica e a acao nao e possivel.</summary>
        public virtual void OnInteractionBlocked(InteractionContext context)
        {
            TomasThoughts.Say(GetBlockedFeedback(context));
        }

        public virtual void OnFocusEntered(InteractionContext context)
        {
            onFocusEntered?.Invoke();
        }

        public virtual void OnFocusExited(InteractionContext context)
        {
            onFocusExited?.Invoke();
        }

        public void SetAvailable(bool value)
        {
            available = value;
        }

        public void SetPrompt(string prompt)
        {
            interactionPrompt = prompt;
        }

        protected static string Pick(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        }
    }
}
