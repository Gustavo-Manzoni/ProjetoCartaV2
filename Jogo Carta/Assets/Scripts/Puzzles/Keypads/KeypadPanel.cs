using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using ProjetoCarta.Core;
using ProjetoCarta.NoirUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Base dos keypads em World Space: guarda a tentativa, mostra uma bolinha branca por entrada na
    /// telinha, valida no Enter e da feedback de erro/acerto. As subclasses definem botoes e validacao.
    /// </summary>
    public abstract class KeypadPanel : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image displayBackground;
        [Tooltip("Uma imagem por posicao da senha. Cheia = entrada digitada.")]
        [SerializeField] private Image[] displayDots = Array.Empty<Image>();
        [SerializeField] private TMP_Text statusText;

        [Header("Cores da telinha")]
        [SerializeField] private Color displayIdleColor = new(0.05f, 0.06f, 0.06f, 1f);
        [SerializeField] private Color displayErrorColor = new(0.45f, 0.06f, 0.05f, 1f);
        [SerializeField] private Color displaySuccessColor = new(0.08f, 0.32f, 0.14f, 1f);
        [SerializeField] private Color dotFilledColor = Color.white;
        [SerializeField] private Color dotEmptyColor = new(1f, 1f, 1f, 0.12f);

        private readonly List<int> entry = new();
        private bool interactive;
        private bool locked;
        private Coroutine feedbackRoutine;

        /// <summary>Disparado no Enter com o resultado da validacao.</summary>
        public event Action<bool> Submitted;

        public IReadOnlyList<int> Entry => entry;
        public bool IsInteractive => interactive && !locked;
        protected abstract int MaxLength { get; }

        protected virtual void Awake()
        {
            BindButtons();
            SetInteractive(false);
            RefreshDisplay();
            if (displayBackground != null)
                displayBackground.color = displayIdleColor;
            SetStatus(string.Empty);
        }

        protected virtual void OnDisable()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            locked = false;
            if (panelRoot != null)
                panelRoot.DOKill(true);
        }

        protected virtual void Update()
        {
            if (IsInteractive)
                HandleKeyboard();
        }

        public void SetInteractive(bool value)
        {
            interactive = value;
            if (canvasGroup == null)
                return;

            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        public void ResetEntry()
        {
            entry.Clear();
            locked = false;
            RefreshDisplay();
            OnEntryChanged();
            SetStatus(string.Empty);
            if (displayBackground != null)
                displayBackground.color = displayIdleColor;
        }

        /// <summary>Mostra o estado de acerto e trava a entrada.</summary>
        public void ShowSolved()
        {
            locked = true;
            if (displayBackground != null)
                displayBackground.color = displaySuccessColor;
            SetStatus("ABERTO");
        }

        // ------------------------------------------------------------ entrada

        protected abstract void BindButtons();
        protected abstract bool Validate(IReadOnlyList<int> attempt);
        protected virtual void HandleKeyboard() { }
        protected virtual void OnEntryChanged() { }

        protected void Append(int value)
        {
            if (!IsInteractive)
                return;

            if (entry.Count >= MaxLength)
            {
                // Entrada cheia: nada muda em silencio; o painel "recusa" com um tranco.
                Nudge();
                NoirAudio.Play(NoirSfx.KeypadFull, 0.6f);
                return;
            }

            entry.Add(value);
            NoirAudio.Play(NoirSfx.KeypadPress, 0.7f, 0.95f + entry.Count * 0.03f);
            RefreshDisplay();
            PunchDot(entry.Count - 1);
            OnEntryChanged();
        }

        protected void RemoveLast()
        {
            if (!IsInteractive || entry.Count == 0)
                return;

            entry.RemoveAt(entry.Count - 1);
            NoirAudio.Play(NoirSfx.KeypadPress, 0.55f, 0.8f);
            RefreshDisplay();
            OnEntryChanged();
        }

        protected void ClearAll()
        {
            if (!IsInteractive || entry.Count == 0)
                return;

            entry.Clear();
            NoirAudio.Play(NoirSfx.KeypadPress, 0.55f, 0.7f);
            RefreshDisplay();
            OnEntryChanged();
        }

        protected void Submit()
        {
            if (!IsInteractive)
                return;

            bool correct = Validate(entry);
            if (correct)
            {
                locked = true;
                NoirAudio.Play(NoirSfx.KeypadSuccess, 0.9f);
                if (displayBackground != null)
                {
                    displayBackground.DOKill();
                    displayBackground.DOColor(displaySuccessColor, 0.15f).SetUpdate(true);
                }

                SetStatus("ABERTO");
            }
            else
            {
                NoirAudio.Play(NoirSfx.KeypadError, 0.9f);
                if (feedbackRoutine != null)
                    StopCoroutine(feedbackRoutine);
                feedbackRoutine = StartCoroutine(ErrorRoutine());
            }

            Submitted?.Invoke(correct);
        }

        private IEnumerator ErrorRoutine()
        {
            locked = true;
            SetStatus(entry.Count == 0 ? "VAZIO" : "ERRO");
            if (displayBackground != null)
            {
                displayBackground.DOKill();
                displayBackground.color = displayErrorColor;
            }

            if (panelRoot != null)
            {
                panelRoot.DOKill(true);
                panelRoot.DOShakeAnchorPos(0.35f, new Vector2(10f, 0f), 18, 0f).SetUpdate(true);
            }

            yield return new WaitForSecondsRealtime(0.7f);

            // Regra previsivel: tentativa errada sempre limpa a telinha inteira.
            entry.Clear();
            RefreshDisplay();
            OnEntryChanged();
            SetStatus(string.Empty);
            if (displayBackground != null)
                displayBackground.DOColor(displayIdleColor, 0.25f).SetUpdate(true);

            locked = false;
            feedbackRoutine = null;
        }

        // ------------------------------------------------------------ visual

        private void RefreshDisplay()
        {
            for (int i = 0; i < displayDots.Length; i++)
            {
                if (displayDots[i] == null)
                    continue;

                bool filled = i < entry.Count;
                displayDots[i].color = filled ? dotFilledColor : dotEmptyColor;
                displayDots[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void PunchDot(int index)
        {
            if (index < 0 || index >= displayDots.Length || displayDots[index] == null)
                return;

            RectTransform dot = displayDots[index].rectTransform;
            dot.DOKill(true);
            dot.localScale = Vector3.one * 0.4f;
            dot.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void Nudge()
        {
            if (panelRoot == null)
                return;

            panelRoot.DOKill(true);
            panelRoot.DOPunchAnchorPos(new Vector2(6f, 0f), 0.2f, 12, 0.5f).SetUpdate(true);
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = Loc.T(text);
        }

        protected static void Bind(Button button, Action action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }
    }
}
