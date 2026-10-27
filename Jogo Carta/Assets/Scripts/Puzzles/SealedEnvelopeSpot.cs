using System.Collections;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Ponto da mesa do escritorio (P1) onde o envelope lacrado do poco e aberto com o abridor de cartas.
    /// 1) com o envelope na mao: coloca-lo na mesa; 2) com o abridor: abrir; 3) o conteudo (ficha) sai do envelope.
    /// O abridor nao e consumido.
    /// </summary>
    public sealed class SealedEnvelopeSpot : RaycastInteractable
    {
        [Header("Itens")]
        [SerializeField] private InventoryItemDefinition envelopeItem;
        [SerializeField] private InventoryItemDefinition openerItem;

        [Header("Pose do envelope na mesa")]
        [SerializeField] private Transform envelopeAnchor;
        [SerializeField] private Vector3 envelopeLocalEuler;
        [SerializeField, Min(0.05f)] private float envelopeWorldSize = 0.26f;

        [Header("Conteúdo")]
        [SerializeField] private InventoryPickup content;
        [Tooltip("Onde o conteudo para depois de sair do envelope.")]
        [SerializeField] private Transform contentRevealPoint;
        [Tooltip("Objetos que aparecem na mesa junto com o conteudo (ex.: o bilhete que veio no envelope). Comecam inativos.")]
        [SerializeField] private GameObject[] extraContents;

        [Header("Prompts e pensamentos")]
        [SerializeField] private string placePrompt = "Colocar o envelope lacrado na mesa";
        [SerializeField] private string sealedPrompt = "Envelope lacrado";
        [SerializeField] private string openPrompt = "Abrir o envelope com o abridor";
        [SerializeField, TextArea(1, 2)] private string missingOpenerThought = "Está bem colado. Preciso de algo com lâmina fina.";
        [SerializeField, TextArea(1, 2)] private string wrongItemThought = "Assim eu rasgo o que tem dentro.";
        [SerializeField, TextArea(1, 2)] private string openedThought;

        [SerializeField] private UnityEvent onOpened;

        private enum SpotState { Empty, Placed, Opening, Opened }

        private SpotState state = SpotState.Empty;
        private GameObject envelopeObject;

        private void Awake()
        {
            if (content != null)
            {
                content.SetAvailable(false);
                content.gameObject.SetActive(false);
            }

            SetExtraContentsActive(false);
        }

        private void SetExtraContentsActive(bool value)
        {
            if (extraContents == null)
                return;

            foreach (GameObject extra in extraContents)
            {
                if (extra != null)
                    extra.SetActive(value);
            }
        }

        public override bool IsFocusable(InteractionContext context)
        {
            if (!base.IsFocusable(context))
                return false;

            return state switch
            {
                SpotState.Empty => context.IsEquipped(envelopeItem),
                SpotState.Placed => true,
                _ => false
            };
        }

        public override bool CanInteract(InteractionContext context)
        {
            if (!IsFocusable(context))
                return false;

            return state == SpotState.Empty ? context.IsEquipped(envelopeItem) : context.IsEquipped(openerItem);
        }

        public override string GetPrompt(InteractionContext context)
        {
            if (state == SpotState.Empty)
                return placePrompt;

            return context.IsEquipped(openerItem) ? openPrompt : sealedPrompt;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            if (state != SpotState.Placed)
                return InteractionFeedback.None;

            return context.EquippedItem == null
                ? InteractionFeedback.Blocked(InteractionOutcome.MissingItem, missingOpenerThought)
                : InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongItemThought);
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            if (state == SpotState.Empty)
                PlaceEnvelope(context.Inventory);
            else
                StartCoroutine(OpenRoutine());

            context.Interactor?.RefreshPrompt(this);
        }

        private void PlaceEnvelope(InventoryController inventory)
        {
            if (!inventory.TryRemoveAndGetObject(envelopeItem, out GameObject envelope) || envelope == null)
                return;

            envelopeObject = envelope;
            Transform anchor = envelopeAnchor != null ? envelopeAnchor : transform;
            WorldItemPlacement.PrepareForWorld(envelope, anchor);
            Vector3 scale = WorldItemPlacement.ScaleToFit(envelope, envelopeWorldSize, Quaternion.Euler(envelopeLocalEuler), anchor);
            WorldItemPlacement.MoveTo(envelope.transform, Vector3.zero, Quaternion.Euler(envelopeLocalEuler),
                scale, 0.45f, null, () => NoirAudio.Play(NoirSfx.Paper, 0.7f));
            state = SpotState.Placed;
        }

        private IEnumerator OpenRoutine()
        {
            state = SpotState.Opening;
            NoirAudio.Play(NoirSfx.PaperTear, 0.9f);

            // O envelope levanta um pouco e inclina enquanto e cortado.
            if (envelopeObject != null)
            {
                Transform envelope = envelopeObject.transform;
                Vector3 basePosition = envelope.localPosition;
                Quaternion baseRotation = envelope.localRotation;
                Vector3 lift = envelope.parent != null ? envelope.parent.InverseTransformVector(Vector3.up * 0.03f) : Vector3.up * 0.03f;
                float elapsed = 0f;
                while (elapsed < 0.6f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / 0.6f);
                    float bump = Mathf.Sin(t * Mathf.PI);
                    envelope.localPosition = basePosition + lift * bump;
                    envelope.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 18f) * 3f * bump);
                    yield return null;
                }

                envelope.localPosition = basePosition;
                envelope.localRotation = baseRotation;
            }

            if (content != null)
            {
                Transform contentTransform = content.transform;
                Vector3 finalWorldPosition = contentRevealPoint != null ? contentRevealPoint.position : contentTransform.position;
                Quaternion finalWorldRotation = contentRevealPoint != null ? contentRevealPoint.rotation : contentTransform.rotation;
                Vector3 startWorld = envelopeObject != null ? envelopeObject.transform.position : finalWorldPosition;

                content.gameObject.SetActive(true);
                contentTransform.position = startWorld;
                contentTransform.rotation = finalWorldRotation;

                // Desliza para fora do envelope.
                float elapsed = 0f;
                while (elapsed < 0.55f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.55f));
                    contentTransform.position = Vector3.Lerp(startWorld, finalWorldPosition, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.04f);
                    yield return null;
                }

                contentTransform.position = finalWorldPosition;
                NoirAudio.Play(NoirSfx.Coin, 0.85f);
                content.SetAvailable(true);
            }

            SetExtraContentsActive(true);
            NoirAudio.Play(NoirSfx.Paper, 0.6f);
            state = SpotState.Opened;
            TomasThoughts.Say(openedThought);
            onOpened?.Invoke();
        }

        private void OnDisable()
        {
            // Piscar no meio da abertura: conclui sem animacao para nao perder o conteudo.
            if (state != SpotState.Opening)
                return;

            if (content != null)
            {
                content.gameObject.SetActive(true);
                if (contentRevealPoint != null)
                    content.transform.SetPositionAndRotation(contentRevealPoint.position, contentRevealPoint.rotation);
                content.SetAvailable(true);
            }

            SetExtraContentsActive(true);
            state = SpotState.Opened;
        }
    }
}
