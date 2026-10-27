using System.Collections;
using System.Collections.Generic;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Base de tudo que abre e fecha (bau, gaveta, caixa, tampa, porta de caixa de controle).
    /// Cuida de: tranca opcional por item, prompts por estado, pensamentos de Tomas, bloqueio durante
    /// a animacao e liberacao do conteudo (pickups filhos) somente quando aberto.
    /// A forma de mover fica com a subclasse (transform, Animator...).
    /// </summary>
    public abstract class OpenableInteractable : RaycastInteractable
    {
        [Header("Fechadura (opcional)")]
        [Tooltip("Item necessario para destrancar. Vazio = abre livremente.")]
        [SerializeField] private InventoryItemDefinition requiredItem;
        [Tooltip("Trancado ate um puzzle chamar Unlock() (gaveta do arquivo, compartimento de manutencao). " +
                 "Sem item: o clique so mostra o lockedThought.")]
        [SerializeField] private bool lockedByPuzzle;
        [SerializeField] private bool consumeItemOnUnlock = true;
        [SerializeField] private string lockedPrompt = "Trancado";
        [Tooltip("Prompt quando o item certo esta na mao.")]
        [SerializeField] private string unlockPrompt = "Destrancar";
        [SerializeField, TextArea(1, 3)] private string lockedThought = "Está trancado. Preciso encontrar a chave certa.";
        [SerializeField, TextArea(1, 3)] private string wrongItemThought = "Essa chave não parece servir aqui.";
        [SerializeField, TextArea(1, 3)] private string unlockedThought;

        [Header("Abrir e fechar")]
        [SerializeField] private string openPrompt = "Abrir";
        [SerializeField] private string closePrompt = "Fechar";
        [SerializeField] private bool startsOpen;
        [Tooltip("Se falso, depois de aberto o objeto fica aberto e deixa de receber foco.")]
        [SerializeField] private bool canClose = true;
        [SerializeField] private bool openImmediatelyOnUnlock = true;
        [SerializeField] private OpenableSound sound = OpenableSound.Wood;

        [Header("Conteúdo")]
        [Tooltip("Interagiveis liberados so quando aberto. Vazio + auto = pickups filhos.")]
        [SerializeField] private RaycastInteractable[] contents;
        [SerializeField] private bool autoFindContents = true;
        [Tooltip("Fracao da abertura a partir da qual o conteudo pode ser pego.")]
        [SerializeField, Range(0f, 1f)] private float contentsAvailableAt = 0.7f;

        [Header("Events")]
        [SerializeField] private UnityEvent onUnlocked;
        [SerializeField] private UnityEvent onOpened;
        [SerializeField] private UnityEvent onClosed;

        private readonly List<RaycastInteractable> resolvedContents = new();
        private bool unlocked;
        private bool isOpen;
        private Coroutine motionRoutine;

        public bool IsOpen => isOpen;
        public bool IsLocked => (requiredItem != null || lockedByPuzzle) && !unlocked;
        public bool IsLockedByPuzzle => lockedByPuzzle;
        public bool IsBusy => motionRoutine != null;
        public InventoryItemDefinition RequiredItem => requiredItem;

        protected virtual void Awake()
        {
            ResolveContents();
            isOpen = startsOpen;
            if (startsOpen)
                unlocked = true;

            ApplyInitialState(isOpen);
            SetContentsAvailable(isOpen);
        }

        // ------------------------------------------------------------ interacao

        public override bool IsFocusable(InteractionContext context)
        {
            if (!base.IsFocusable(context))
                return false;

            return IsLocked || !isOpen || canClose;
        }

        public override bool CanInteract(InteractionContext context)
        {
            if (!IsFocusable(context) || IsBusy)
                return false;

            return !IsLocked || UnlocksWithEquipped(context);
        }

        public override string GetPrompt(InteractionContext context)
        {
            if (IsLocked)
                return UnlocksWithEquipped(context) ? unlockPrompt : lockedPrompt;

            return isOpen ? closePrompt : openPrompt;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            if (IsBusy || !IsLocked)
                return InteractionFeedback.None;

            // Trava de puzzle sem item: nenhum objeto da mao resolve, so o puzzle.
            if (requiredItem == null)
                return InteractionFeedback.Blocked(InteractionOutcome.PrerequisiteMissing, lockedThought);

            return context.EquippedItem == null
                ? InteractionFeedback.Blocked(InteractionOutcome.MissingItem, lockedThought)
                : InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongItemThought);
        }

        public override void OnInteractionBlocked(InteractionContext context)
        {
            base.OnInteractionBlocked(context);
            if (IsLocked && !IsBusy)
            {
                NoirAudio.Play(NoirSfx.Locked, 0.8f);
                OnLockedRattle();
            }
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            if (IsLocked)
            {
                if (consumeItemOnUnlock && context.Inventory != null)
                    context.Inventory.Consume(requiredItem);

                Unlock();
                if (openImmediatelyOnUnlock)
                    SetOpen(true);
            }
            else
            {
                SetOpen(!isOpen);
            }

            context.Interactor?.RefreshPrompt(this);
        }

        private bool UnlocksWithEquipped(InteractionContext context)
        {
            return requiredItem != null && context.IsEquipped(requiredItem);
        }

        // ------------------------------------------------------------ API publica

        public void Unlock()
        {
            if (unlocked)
                return;

            unlocked = true;
            NoirAudio.Play(NoirSfx.Unlock, 0.9f);
            TomasThoughts.Say(unlockedThought);
            onUnlocked?.Invoke();
        }

        /// <summary>
        /// Recompensa de puzzle: destrava e abre. Com o objeto inativo (o jogador piscou durante a espera),
        /// fica aberto sem animacao, e o jogador encontra o conteudo liberado ao voltar.
        /// </summary>
        public void UnlockAndOpen()
        {
            Unlock();
            if (isActiveAndEnabled)
            {
                SetOpen(true);
                return;
            }

            isOpen = true;
            ApplyInitialState(true);
            SetContentsAvailable(true);
            onOpened?.Invoke();
        }

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!isOpen);

        public void SetOpen(bool open)
        {
            if (IsLocked && open)
                return;

            if (open == isOpen && motionRoutine == null)
                return;

            isOpen = open;
            if (motionRoutine != null)
                StopCoroutine(motionRoutine);

            if (!open)
                SetContentsAvailable(false);

            PlaySound(open);
            motionRoutine = StartCoroutine(MotionRoutine(open));
        }

        // ------------------------------------------------------------ subclasses

        /// <summary>Coloca o objeto no estado inicial, sem animacao.</summary>
        protected abstract void ApplyInitialState(bool open);

        /// <summary>Inicia o movimento e devolve a duracao em segundos.</summary>
        protected abstract float BeginMotion(bool open);

        /// <summary>Atualizado a cada frame com o progresso 0..1 do movimento atual.</summary>
        protected virtual void UpdateMotion(bool open, float normalizedTime) { }

        protected virtual void EndMotion(bool open) { }

        /// <summary>Pequena reacao visual ao tentar abrir trancado.</summary>
        protected virtual void OnLockedRattle() { }

        // ------------------------------------------------------------ interno

        private IEnumerator MotionRoutine(bool open)
        {
            float duration = Mathf.Max(0f, BeginMotion(open));
            float elapsed = 0f;
            bool contentsReleased = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                UpdateMotion(open, t);

                if (open && !contentsReleased && t >= contentsAvailableAt)
                {
                    contentsReleased = true;
                    SetContentsAvailable(true);
                }

                yield return null;
            }

            UpdateMotion(open, 1f);
            EndMotion(open);
            motionRoutine = null;

            if (open)
            {
                SetContentsAvailable(true);
                onOpened?.Invoke();
            }
            else
            {
                onClosed?.Invoke();
            }
        }

        private void PlaySound(bool open)
        {
            NoirSfx sfx = sound switch
            {
                OpenableSound.Drawer => NoirSfx.Drawer,
                OpenableSound.Metal => open ? NoirSfx.MetalOpen : NoirSfx.MetalClose,
                OpenableSound.None => NoirSfx.None,
                _ => open ? NoirSfx.WoodOpen : NoirSfx.WoodClose
            };

            NoirAudio.Play(sfx, 0.85f);
        }

        private void ResolveContents()
        {
            resolvedContents.Clear();
            if (contents != null)
            {
                foreach (RaycastInteractable content in contents)
                {
                    if (content != null && content != this)
                        resolvedContents.Add(content);
                }
            }

            if (resolvedContents.Count > 0 || !autoFindContents)
                return;

            foreach (InventoryPickup pickup in GetComponentsInChildren<InventoryPickup>(true))
                resolvedContents.Add(pickup);
        }

        private void SetContentsAvailable(bool value)
        {
            foreach (RaycastInteractable content in resolvedContents)
            {
                if (content == null)
                    continue;

                // Um pickup ja recolhido pertence ao inventario: nao mexer.
                if (content is InventoryPickup pickup && pickup.IsCollected)
                    continue;

                content.SetAvailable(value);
            }
        }

        protected virtual void OnDisable()
        {
            if (motionRoutine == null)
                return;

            StopCoroutine(motionRoutine);
            motionRoutine = null;
            ApplyInitialState(isOpen);
            SetContentsAvailable(isOpen);
        }
    }

    public enum OpenableSound
    {
        Wood,
        Drawer,
        Metal,
        None
    }
}
