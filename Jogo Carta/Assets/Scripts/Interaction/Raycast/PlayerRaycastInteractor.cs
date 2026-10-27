using System;
using ProjetoCarta.Core;
using ProjetoCarta.Inventory;
using ProjetoCarta.Inventory.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjetoCarta.Interaction
{
    public sealed class PlayerRaycastInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private InventoryController inventory;
        [SerializeField] private RadialInventoryView radialInventory;

        [Header("Raycast")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactableLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Feedback")]
        [Tooltip("Tempo minimo entre dois pensamentos disparados por cliques repetidos no mesmo objeto.")]
        [SerializeField, Min(0f)] private float blockedFeedbackCooldown = 0.35f;

        private const float ContentLookThrough = 0.7f;
        private const float CoplanarTolerance = 0.03f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[12];
        private RaycastInteractable focusedInteractable;
        private string currentPrompt = string.Empty;
        private RaycastInteractable lastBlockedTarget;
        private float lastBlockedTime = -10f;
        private bool focusCanInteract;

        public event Action<string, bool> PromptChanged;
        public RaycastInteractable FocusedInteractable => focusedInteractable;
        /// <summary>O clique esquerdo agora executa a acao do objeto em foco (false sem foco ou se estiver bloqueado).</summary>
        public bool FocusedCanInteract => focusedInteractable != null && focusCanInteract;
        public InventoryController Inventory => inventory;

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = Camera.main;
        }

        private void Update()
        {
            if (interactionCamera == null || Mouse.current == null ||
                ProjetoCarta.NoirUI.PauseState.IsPaused ||
                GameplayLocks.InteractionLocked ||
                (radialInventory != null && radialInventory.IsOpen))
            {
                ChangeFocus(null);
                return;
            }

            InteractionContext context = CreateContext();
            ChangeFocus(FindCandidate(context));

            if (focusedInteractable == null)
                return;

            RefreshPromptIfChanged(context);
            focusCanInteract = focusedInteractable.CanInteract(context);

            if (Mouse.current.leftButton.wasPressedThisFrame)
                InteractWithFocused();
        }

        /// <summary>
        /// O que o clique esquerdo faz: executa a acao do objeto em foco ou mostra o pensamento de Tomas
        /// explicando por que nao deu. Publico para testes automatizados usarem o mesmo caminho do mouse.
        /// </summary>
        public bool InteractWithFocused()
        {
            RaycastInteractable target = focusedInteractable;
            if (target == null)
                return false;

            InteractionContext context = CreateContext();
            bool performed = target.CanInteract(context);
            if (performed)
            {
                target.Interact(context);
            }
            else if (target != lastBlockedTarget || Time.unscaledTime - lastBlockedTime >= blockedFeedbackCooldown)
            {
                lastBlockedTarget = target;
                lastBlockedTime = Time.unscaledTime;
                target.OnInteractionBlocked(context);
            }

            if (focusedInteractable != null && !focusedInteractable.IsFocusable(context))
                ChangeFocus(null);
            else if (focusedInteractable != null)
                RefreshPromptIfChanged(context);

            return performed;
        }

        public string CurrentPrompt => currentPrompt;

        private RaycastInteractable FindCandidate(InteractionContext context)
        {
            Ray ray = new(interactionCamera.transform.position, interactionCamera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, interactionDistance, interactableLayers, triggerInteraction);
            if (count == 0)
                return null;

            System.Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);

            // Gatilhos (volumes de clique) que nao levam a nada interagivel agora nao bloqueiam o raio.
            int firstIndex = 0;
            RaycastInteractable first = null;
            for (; firstIndex < count; firstIndex++)
            {
                first = Resolve(hitBuffer[firstIndex].collider.transform, context);
                if (first != null || !hitBuffer[firstIndex].collider.isTrigger)
                    break;
            }

            if (firstIndex >= count)
                return null;

            // Superficie "morta" coplanar a algo interagivel (papel colado numa tampa): o interagivel vence.
            if (first == null)
            {
                for (int i = firstIndex + 1; i < count; i++)
                {
                    if (hitBuffer[i].distance - hitBuffer[firstIndex].distance > CoplanarTolerance)
                        break;

                    RaycastInteractable coplanar = Resolve(hitBuffer[i].collider.transform, context);
                    if (coplanar != null)
                        return coplanar;
                }

                return null;
            }

            // Volume de mira amplo (poco): qualquer interagivel mais especifico dentro dele vence o volume.
            if (hitBuffer[firstIndex].collider.TryGetComponent(out InteractionVolume volume))
            {
                for (int i = firstIndex + 1; i < count; i++)
                {
                    if (!volume.Contains(hitBuffer[i].point))
                        break;

                    RaycastInteractable inner = Resolve(hitBuffer[i].collider.transform, context);
                    if (inner != null && inner != first)
                        return inner;
                }

                return first;
            }

            // Itens dentro de um recipiente aberto (bau, gaveta, caixa) tem prioridade sobre as paredes dele:
            // olhando de um angulo raso, a borda do bau nao "rouba" o clique do objeto que esta dentro.
            if (first is OpenableInteractable container && container.IsOpen)
            {
                for (int i = firstIndex + 1; i < count; i++)
                {
                    if (hitBuffer[i].distance - hitBuffer[firstIndex].distance > ContentLookThrough)
                        break;

                    RaycastInteractable behind = Resolve(hitBuffer[i].collider.transform, context);
                    if (behind is InventoryPickup && behind != container && behind.transform.IsChildOf(container.transform))
                        return behind;
                }
            }

            return first;
        }

        /// <summary>Sobe a hierarquia a partir do collider: o objeto mais especifico que aceita foco vence.</summary>
        private static RaycastInteractable Resolve(Transform hitTransform, InteractionContext context)
        {
            for (Transform current = hitTransform; current != null; current = current.parent)
            {
                foreach (RaycastInteractable interactable in current.GetComponents<RaycastInteractable>())
                {
                    if (interactable.IsFocusable(context))
                        return interactable;
                }
            }

            return null;
        }

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();

            public int Compare(RaycastHit a, RaycastHit b)
            {
                return a.distance.CompareTo(b.distance);
            }
        }

        private InteractionContext CreateContext()
        {
            return new InteractionContext(this, inventory);
        }

        public void RefreshPrompt(RaycastInteractable interactable)
        {
            if (focusedInteractable == interactable && focusedInteractable != null)
                RefreshPromptIfChanged(CreateContext(), true);
        }

        private void RefreshPromptIfChanged(InteractionContext context, bool force = false)
        {
            string prompt = focusedInteractable.GetPrompt(context) ?? string.Empty;
            if (!force && prompt == currentPrompt)
                return;

            // CurrentPrompt fica em PT (logica e teste automatico); so o payload exibido e traduzido.
            currentPrompt = prompt;
            PromptChanged?.Invoke(Loc.T(prompt), !string.IsNullOrEmpty(prompt));
        }

        private void ChangeFocus(RaycastInteractable next)
        {
            if (focusedInteractable == next)
                return;

            InteractionContext context = CreateContext();

            if (focusedInteractable != null)
                focusedInteractable.OnFocusExited(context);

            focusedInteractable = next;

            if (focusedInteractable != null)
            {
                focusedInteractable.OnFocusEntered(context);
                currentPrompt = focusedInteractable.GetPrompt(context) ?? string.Empty;
                PromptChanged?.Invoke(Loc.T(currentPrompt), !string.IsNullOrEmpty(currentPrompt));
            }
            else
            {
                focusCanInteract = false;
                currentPrompt = string.Empty;
                PromptChanged?.Invoke(string.Empty, false);
            }
        }

        private void OnDisable()
        {
            ChangeFocus(null);
        }
    }
}
