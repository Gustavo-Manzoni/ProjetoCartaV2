using System.Collections;
using System.Collections.Generic;
using ProjetoCarta.Core;
using ProjetoCarta.NoirUI;
using ProjetoCarta.Puzzles;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Cofre com keypad fisico (Canvas World Space na porta). Ao interagir, a camera aproxima do teclado,
    /// o jogador fica travado e o cursor liberado; Esc sai sem resolver. O tipo de keypad
    /// (numerico ou de simbolos) e o codigo ficam no <see cref="KeypadPanel"/>.
    /// Ao acertar: volta a camera, abre a porta com a animacao existente e libera o conteudo.
    /// </summary>
    public sealed class SafeInteractable : RaycastInteractable
    {
        [Header("Safe view")]
        [SerializeField] private CinemachineCamera safeCamera;
        [Tooltip("CanvasGroup do keypad (Canvas World Space).")]
        [SerializeField] private CanvasGroup safeInterface;
        [SerializeField] private int activeCameraPriority = 100;
        [Tooltip("Espera a camera chegar antes de aceitar cliques.")]
        [SerializeField, Min(0f)] private float inputDelay = 0.45f;

        [Header("Keypad")]
        [SerializeField] private KeypadPanel keypad;

        [Header("Porta")]
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private string openTrigger = "OpenSafe";
        [SerializeField, Min(0f)] private float openDelayAfterSolve = 0.55f;

        [Header("Conteúdo")]
        [Tooltip("Liberado so depois que a porta abre. Vazio = pickups filhos.")]
        [SerializeField] private RaycastInteractable[] contents;

        [Header("Pensamentos")]
        [SerializeField, TextArea(1, 3)] private string wrongCodeThought = "A sequência não está certa.";
        [SerializeField, TextArea(1, 3)] private string solvedThought;

        [Header("Events")]
        [SerializeField] private UnityEvent onOpened;
        [SerializeField] private UnityEvent onExited;
        [SerializeField] private UnityEvent onSolved;

        private readonly List<RaycastInteractable> resolvedContents = new();
        private int previousSafeCameraPriority;
        private bool isInSafeView;
        private bool solved;
        private bool doorOpened;
        private bool contentsReleased;
        private Coroutine enterRoutine;

        public bool IsInSafeView => isInSafeView;
        public bool IsSolved => solved;

        private void Awake()
        {
            if (doorAnimator == null)
                doorAnimator = GetComponent<Animator>();

            if (doorAnimator != null)
                doorAnimator.keepAnimatorStateOnDisable = true;

            ResolveContents();
            SetContentsAvailable(false);
        }

        private void OnEnable()
        {
            if (keypad != null)
                keypad.Submitted += OnKeypadSubmitted;

            // Se algo interrompeu a abertura (objeto desativado), conclui ao voltar.
            if (Application.isPlaying && solved && !contentsReleased)
            {
                OpenDoor();
                FinishOpening();
            }
        }

        private void OnDisable()
        {
            if (keypad != null)
                keypad.Submitted -= OnKeypadSubmitted;

            if (Application.isPlaying && isInSafeView)
                ExitSafeView();

            GameplayLocks.Release(this);
        }

        private void Update()
        {
            if (!isInSafeView || Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                GameplayLocks.ConsumeEscape();
                ExitSafeView();
            }
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && !solved && !isInSafeView;
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context) && keypad != null;
        }

        public override string GetPrompt(InteractionContext context)
        {
            string prompt = InteractionPrompt;
            return string.IsNullOrWhiteSpace(prompt) || prompt == "Interagir" ? "Usar o teclado do cofre" : prompt;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            EnterSafeView();
        }

        public void EnterSafeView()
        {
            if (isInSafeView || solved)
                return;

            isInSafeView = true;
            GameplayLocks.Acquire(this, GameplayLock.Modal);

            if (safeCamera != null)
            {
                previousSafeCameraPriority = safeCamera.Priority;
                safeCamera.Priority = activeCameraPriority;
            }

            keypad?.ResetEntry();
            NoirAudio.Play(NoirSfx.Open, 0.5f, 1.3f);

            if (enterRoutine != null)
                StopCoroutine(enterRoutine);
            enterRoutine = StartCoroutine(EnableInputAfterDelay());
            onOpened?.Invoke();
        }

        public void ExitSafeView()
        {
            if (!isInSafeView)
                return;

            isInSafeView = false;
            if (enterRoutine != null)
            {
                StopCoroutine(enterRoutine);
                enterRoutine = null;
            }

            keypad?.SetInteractive(false);
            SetInterfaceInteractive(false);

            if (safeCamera != null)
                safeCamera.Priority = previousSafeCameraPriority;

            GameplayLocks.Release(this);
            onExited?.Invoke();
        }

        private IEnumerator EnableInputAfterDelay()
        {
            yield return new WaitForSecondsRealtime(inputDelay);
            SetInterfaceInteractive(true);
            keypad?.SetInteractive(true);
            enterRoutine = null;
        }

        private void OnKeypadSubmitted(bool correct)
        {
            if (!isInSafeView || solved)
                return;

            if (!correct)
            {
                TomasThoughts.Say(wrongCodeThought, InteractionOutcome.WrongItem);
                return;
            }

            solved = true;
            SetAvailable(false);
            StartCoroutine(SolveRoutine());
        }

        private IEnumerator SolveRoutine()
        {
            yield return new WaitForSecondsRealtime(openDelayAfterSolve);
            ExitSafeView();

            // Ate a porta abrir, a piscada fica travada: o cofre nunca fica resolvido e fechado.
            GameplayLocks.Acquire(this, GameplayLock.Blink);

            // Deixa a camera voltar antes da porta abrir, para o jogador ver a abertura.
            yield return new WaitForSeconds(0.35f);
            OpenDoor();
            yield return new WaitForSeconds(0.45f);
            FinishOpening();
        }

        private void OpenDoor()
        {
            if (doorOpened)
                return;

            doorOpened = true;
            if (doorAnimator != null && !string.IsNullOrEmpty(openTrigger))
                doorAnimator.SetTrigger(openTrigger);
            NoirAudio.Play(NoirSfx.SafeOpen, 1f);
        }

        private void FinishOpening()
        {
            GameplayLocks.Release(this);
            if (contentsReleased)
                return;

            contentsReleased = true;
            SetContentsAvailable(true);
            TomasThoughts.Say(solvedThought);
            onSolved?.Invoke();
        }

        private void SetInterfaceInteractive(bool value)
        {
            if (safeInterface == null)
                return;

            safeInterface.alpha = 1f;
            safeInterface.interactable = value;
            safeInterface.blocksRaycasts = value;
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

            if (resolvedContents.Count > 0)
                return;

            foreach (InventoryPickup pickup in GetComponentsInChildren<InventoryPickup>(true))
                resolvedContents.Add(pickup);
        }

        private void SetContentsAvailable(bool value)
        {
            foreach (RaycastInteractable content in resolvedContents)
            {
                if (content is InventoryPickup pickup && pickup.IsCollected)
                    continue;

                if (content != null)
                    content.SetAvailable(value);
            }
        }

        private void OnValidate()
        {
            activeCameraPriority = Mathf.Max(1, activeCameraPriority);
        }
    }
}
