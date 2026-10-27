using System;
using System.Collections;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Painel do ritual (P1): cinco encaixes. Qualquer objeto do ritual pode ser colocado em qualquer encaixe vazio
    /// e retirado a qualquer momento; o painel so se completa quando cada encaixe tem o seu item correto. Entao revela
    /// o papel com a sequencia de simbolos do cofre. O painel nao abre nem entrega a chave.
    /// Clicar no painel incompleto mostra um pensamento de Tomas.
    /// </summary>
    public sealed class RitualPanel : RaycastInteractable
    {
        [Header("Encaixes")]
        [SerializeField] private InventoryItemPlacement[] slots = new InventoryItemPlacement[5];

        [Header("Pensamento de conjunto errado")]
        [Tooltip("Mostrado quando os cinco encaixes estao preenchidos, mas algum item esta no lugar errado.")]
        [SerializeField, TextArea(1, 2)] private string wrongArrangementThought = "Algo não está no lugar certo.";

        [Header("Papel revelado")]
        [Tooltip("Papel que ja existe na cena; comeca inativo.")]
        [SerializeField] private GameObject paper;
        [Tooltip("Pose final do papel (filho do painel).")]
        [SerializeField] private Transform paperFinalPose;
        [SerializeField, Min(0f)] private float revealDelay = 0.7f;
        [SerializeField, Min(0.1f)] private float revealDuration = 1.4f;
        [SerializeField] private Light revealLight;

        [Header("Pensamentos")]
        [SerializeField, TextArea(1, 2)] private string incompleteThought = "Ainda faltam objetos para completar esse esquema.";
        [SerializeField, TextArea(1, 2)] private string revealedThought = "Um papel... estava escondido atrás do painel.";

        [SerializeField] private UnityEvent onCompleted;

        private bool completed;
        private bool revealFinished;
        private Vector3 paperScale = Vector3.one;

        public bool IsCompleted => completed;

        /// <summary>Encaixes com o item correto da propria posicao.</summary>
        public int CorrectCount
        {
            get
            {
                int count = 0;
                foreach (InventoryItemPlacement slot in slots)
                {
                    if (slot != null && slot.IsCorrect)
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            foreach (InventoryItemPlacement slot in slots)
            {
                if (slot == null)
                    continue;

                slot.AcceptsItem = IsRitualItem;
                slot.PoseSourceFor = SlotOf;
                slot.Placed += OnSlotPlaced;
            }

            if (paper != null)
            {
                paperScale = paper.transform.localScale;
                paper.SetActive(false);
            }
            if (revealLight != null)
                revealLight.enabled = false;
        }

        private void OnDestroy()
        {
            foreach (InventoryItemPlacement slot in slots)
            {
                if (slot != null)
                    slot.Placed -= OnSlotPlaced;
            }
        }

        private void OnEnable()
        {
            // Painel completado com P1 inativo (o ultimo encaixe terminou depois de uma piscada) ou revelacao
            // interrompida por uma piscada: o jogador ainda nao viu o papel, entao a revelacao toca ao voltar.
            if (completed && !revealFinished)
                StartCoroutine(RevealRoutine());
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && !completed;
        }

        public override bool CanInteract(InteractionContext context)
        {
            return false;
        }

        public override string GetPrompt(InteractionContext context)
        {
            return Loc.Format("Painel: {0} de {1}", CorrectCount, slots.Length);
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            return InteractionFeedback.Blocked(InteractionOutcome.PrerequisiteMissing, incompleteThought);
        }

        public override void Interact(InteractionContext context)
        {
        }

        /// <summary>Aceita qualquer item do ritual (o de qualquer encaixe), em qualquer posicao.</summary>
        private bool IsRitualItem(InventoryItemDefinition item)
        {
            foreach (InventoryItemPlacement slot in slots)
            {
                if (slot != null && SameItem(slot.RequiredItem, item))
                    return true;
            }

            return false;
        }

        /// <summary>Encaixe correto do item: e dele que vem a orientacao do item em qualquer posicao.</summary>
        private InventoryItemPlacement SlotOf(InventoryItemDefinition item)
        {
            foreach (InventoryItemPlacement slot in slots)
            {
                if (slot != null && SameItem(slot.RequiredItem, item))
                    return slot;
            }

            return null;
        }

        private static bool SameItem(InventoryItemDefinition first, InventoryItemDefinition second)
        {
            if (ReferenceEquals(first, second))
                return true;

            return first != null && second != null && string.Equals(first.Id, second.Id, StringComparison.Ordinal);
        }

        private void OnSlotPlaced(InventoryItemPlacement slot)
        {
            if (completed)
                return;

            // Todos preenchidos mas com algum item fora do lugar: nao completa, e Tomas comenta.
            if (CorrectCount < slots.Length)
            {
                if (AllSlotsOccupied())
                    TomasThoughts.Say(wrongArrangementThought);
                return;
            }

            completed = true;
            foreach (InventoryItemPlacement s in slots)
            {
                if (s != null)
                    s.Lock();
            }

            SetAvailable(false);
            // Inativo (o jogador piscou durante o encaixe): OnEnable revela quando P1 voltar.
            if (isActiveAndEnabled)
                StartCoroutine(RevealRoutine());
        }

        private bool AllSlotsOccupied()
        {
            foreach (InventoryItemPlacement slot in slots)
            {
                if (slot == null || !slot.IsOccupied)
                    return false;
            }

            return true;
        }

        private IEnumerator RevealRoutine()
        {
            yield return new WaitForSeconds(revealDelay);
            NoirAudio.Play(NoirSfx.Reveal, 1f);

            if (paper == null)
            {
                FinishReveal();
                yield break;
            }

            Transform paperTransform = paper.transform;
            Vector3 endPosition = paperFinalPose != null ? paperFinalPose.position : paperTransform.position;
            Quaternion endRotation = paperFinalPose != null ? paperFinalPose.rotation : paperTransform.rotation;
            Vector3 endScale = paperScale;

            // O papel desliza de tras do painel, girando de leve, e fica preso no centro.
            Vector3 behind = endPosition + transform.forward * 0.06f + Vector3.down * 0.35f;
            paperTransform.SetPositionAndRotation(behind, endRotation * Quaternion.Euler(0f, 0f, -14f));
            paperTransform.localScale = endScale * 0.85f;
            paper.SetActive(true);

            if (revealLight != null)
            {
                revealLight.enabled = true;
                revealLight.intensity = 0f;
            }

            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / revealDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                paperTransform.position = Vector3.Lerp(behind, endPosition, eased);
                paperTransform.rotation = Quaternion.Slerp(endRotation * Quaternion.Euler(0f, 0f, -14f), endRotation, eased);
                paperTransform.localScale = Vector3.Lerp(endScale * 0.85f, endScale, eased);
                if (revealLight != null)
                    revealLight.intensity = Mathf.Sin(t * Mathf.PI * 0.5f) * 0.9f;
                yield return null;
            }

            paperTransform.SetPositionAndRotation(endPosition, endRotation);
            paperTransform.localScale = endScale;
            NoirAudio.Play(NoirSfx.Paper, 0.8f);
            FinishReveal();
        }

        private void FinishReveal()
        {
            if (revealFinished)
                return;

            revealFinished = true;
            if (revealLight != null)
            {
                revealLight.enabled = true;
                revealLight.intensity = 0.45f;
            }

            TomasThoughts.Say(revealedThought);
            onCompleted?.Invoke();
        }
    }
}
