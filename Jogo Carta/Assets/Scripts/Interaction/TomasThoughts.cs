using System;
using UnityEngine;

namespace ProjetoCarta.Interaction
{
    /// <summary>Por que uma interacao nao aconteceu (ou o que acabou de acontecer).</summary>
    public enum InteractionOutcome
    {
        Success,
        /// <summary>O objeto existe, mas nao ha nada a fazer com ele agora.</summary>
        NotPossible,
        /// <summary>Precisa de um item e a mao esta vazia.</summary>
        MissingItem,
        /// <summary>Ha um item na mao, mas nao e o certo.</summary>
        WrongItem,
        /// <summary>Falta resolver outra coisa antes.</summary>
        PrerequisiteMissing,
        /// <summary>O objeto esta no meio de uma animacao; nao mostra mensagem.</summary>
        Busy
    }

    public readonly struct InteractionFeedback
    {
        public InteractionFeedback(InteractionOutcome outcome, string thought)
        {
            Outcome = outcome;
            Thought = thought;
        }

        public InteractionOutcome Outcome { get; }
        public string Thought { get; }

        public static InteractionFeedback None => new(InteractionOutcome.Busy, null);

        public static InteractionFeedback Blocked(InteractionOutcome outcome, string thought)
        {
            return new InteractionFeedback(outcome, thought);
        }
    }

    /// <summary>
    /// Canal dos pensamentos curtos de Tomas mostrados na HUD (GameplayHud escuta).
    /// Qualquer sistema pode chamar <see cref="Say"/>; a HUD cuida de fade e repeticao.
    /// </summary>
    public static class TomasThoughts
    {
        public static event Action<string, InteractionOutcome> Raised;

        public static void Say(string thought, InteractionOutcome outcome = InteractionOutcome.Success)
        {
            if (string.IsNullOrWhiteSpace(thought))
                return;

            Raised?.Invoke(thought.Trim(), outcome);
        }

        public static void Say(InteractionFeedback feedback)
        {
            Say(feedback.Thought, feedback.Outcome);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Raised = null;
        }
    }
}
