using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using ProjetoCarta.Blink;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.Inventory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Juice do HUD de gameplay: placa do prompt de interacao, mira reativa, avisos de item,
    /// cartao de titulo a cada momento (P1/P2/P3), reacao a piscada e dicas de controle.
    /// Tudo adicionado por cima da UI existente.
    /// </summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        private static readonly string[] MomentNumerals = { "I", "II", "III" };
        private static readonly string[] MomentNames = { "A SENTENÇA", "A ÚLTIMA NOITE", "O PERDÃO" };

        private Canvas canvas;
        private PlayerRaycastInteractor interactor;
        private InventoryController inventory;
        private BlinkSceneController blink;
        private InteractionPromptView promptView;

        // prompt
        private TMP_Text promptText;
        private CanvasGroup promptGroup;
        private Image promptPlate;
        private string lastPrompt = string.Empty;
        private bool ownPrompt;
        private RectTransform promptRoot;
        private Image promptShade;
        private Image promptDiamond;
        private Image promptLine;
        private bool promptVisible;
        private bool promptBlocked;

        // cantos de visor em volta da mira (fecham sobre o objeto em foco)
        private RectTransform focusFrame;
        private readonly List<RectTransform> focusCorners = new();
        private readonly List<Image> focusCornerImages = new();
        private float focusAmount;
        private float focusTarget;

        // mira
        private Image reticle;
        private Vector3 reticleBaseScale = Vector3.one;
        private Color reticleBaseColor = Color.white;

        // avisos
        private RectTransform toastRoot;
        private readonly List<RectTransform> toasts = new();
        private readonly Dictionary<string, InventoryItemDefinition> known = new();

        // cartao de momento
        private CanvasGroup cardGroup;
        private TMP_Text cardNumeral;
        private TMP_Text cardTitle;
        private Image cardDivider;
        private Coroutine cardRoutine;
        private int lastMoment = -1;
        private bool wasBlinking;

        // dicas (painel fixo no canto: a acao atual do jogador fica sempre ali)
        private CanvasGroup hintsGroup;
        private TMP_Text hintsText;
        private string lastHintsText;
        private string inventoryKeyLabel = "TAB";
        private bool hintsRequested;

        // pensamentos de Tomas
        private CanvasGroup thoughtGroup;
        private TMP_Text thoughtText;
        private TMP_Text thoughtQuote;
        private Image thoughtLine;
        private RectTransform thoughtBody;
        private string currentThought;
        private float thoughtHideAt;
        private bool thoughtVisible;

        // Opcoes da pausa
        private bool showHints = true;
        private TMP_Text fpsText;
        private float fpsTimer;
        private int fpsFrames;

        private void Start()
        {
            interactor = FindFirstObjectByType<PlayerRaycastInteractor>(FindObjectsInactive.Include);
            inventory = FindFirstObjectByType<InventoryController>(FindObjectsInactive.Include);
            blink = FindFirstObjectByType<BlinkSceneController>(FindObjectsInactive.Include);
            promptView = FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);

            BuildCanvas();
            SetupPrompt();
            SetupReticle();
            BuildToasts();
            BuildMomentCard();
            BuildHints();
            BuildThoughts();
            BuildFpsCounter();
            new GameObject("NoirBlinkTutorial").AddComponent<BlinkTutorial>().transform.SetParent(transform, false);

            ApplySettings(null);
            GameSettings.Changed += ApplySettings;

            if (interactor != null)
                interactor.PromptChanged += OnPromptChanged;

            TomasThoughts.Raised += OnThought;

            if (inventory != null)
            {
                SnapshotInventory();
                inventory.InventoryChanged += OnInventoryChanged;
            }

            if (blink != null)
            {
                lastMoment = blink.CurrentMoment;
                StartCoroutine(ShowCardAfter(1.6f, lastMoment));
            }
        }

        private void OnDestroy()
        {
            TomasThoughts.Raised -= OnThought;
            GameSettings.Changed -= ApplySettings;
            if (interactor != null)
                interactor.PromptChanged -= OnPromptChanged;
            if (inventory != null)
                inventory.InventoryChanged -= OnInventoryChanged;
        }

        // ------------------------------------------------------------ canvas

        private void BuildCanvas()
        {
            GameObject go = new("NoirHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(NoirOwned));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ------------------------------------------------------------ prompt

        private void SetupPrompt()
        {
            if (promptView != null)
            {
                promptText = promptView.GetComponentInChildren<TMP_Text>(true);
                promptGroup = promptView.GetComponentInChildren<CanvasGroup>(true);
                if (promptGroup == null)
                    promptGroup = promptView.GetComponent<CanvasGroup>();
            }

            if (promptText == null)
            {
                BuildOwnPrompt();
                return;
            }

            promptText.color = NoirTheme.Paper;
            promptText.characterSpacing = 5f;
            promptText.fontStyle |= FontStyles.UpperCase;

            GameObject go = new("NoirPromptPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(promptText.transform.parent, false);
            promptPlate = go.GetComponent<Image>();
            promptPlate.sprite = NoirTheme.GetSprite("noir_btn_normal", 14f);
            promptPlate.type = Image.Type.Sliced;
            promptPlate.pixelsPerUnitMultiplier = 1.4f;
            promptPlate.raycastTarget = false;
            go.transform.SetSiblingIndex(promptText.transform.GetSiblingIndex());

            RectTransform src = promptText.rectTransform;
            RectTransform rt = promptPlate.rectTransform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition;
            rt.sizeDelta = new Vector2(260f, 64f);
        }

        // A cena nao tem InteractionPromptView: cria um prompt proprio, alimentado pelo PlayerRaycastInteractor.
        private void BuildOwnPrompt()
        {
            ownPrompt = true;
            GameObject go = new("NoirPrompt", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            promptRoot = (RectTransform)go.transform;
            promptRoot.anchorMin = promptRoot.anchorMax = new Vector2(0.5f, 0.5f);
            promptRoot.anchoredPosition = new Vector2(0f, -112f);
            promptRoot.sizeDelta = new Vector2(1000f, 70f);
            promptGroup = go.GetComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            promptGroup.blocksRaycasts = false;
            promptGroup.interactable = false;

            // Sombra suave em vez de caixa: o texto le sobre qualquer fundo sem parecer um botao.
            promptShade = MakeImage("Sombra", promptRoot, NoirTheme.GetSprite("noir_glow"), new Color(0f, 0f, 0f, 0.6f));
            promptShade.rectTransform.sizeDelta = new Vector2(520f, 120f);

            promptText = MakeText("Text", promptRoot, string.Empty, 28f, NoirTheme.Paper, 6f);
            promptText.rectTransform.sizeDelta = new Vector2(1000f, 50f);
            promptText.fontStyle |= FontStyles.UpperCase;
            promptText.outlineWidth = 0.1f;
            promptText.outlineColor = new Color32(0, 0, 0, 160);

            promptDiamond = MakeImage("Losango", promptRoot, null, NoirTheme.Amber);
            promptDiamond.rectTransform.sizeDelta = new Vector2(10f, 10f);
            promptDiamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            promptLine = MakeImage("Traco", promptRoot, NoirTheme.GetSprite("noir_divider"), NoirTheme.WithAlpha(NoirTheme.Paper, 0.55f));
            promptLine.rectTransform.anchoredPosition = new Vector2(0f, -27f);
            promptLine.rectTransform.sizeDelta = new Vector2(400f, 16f);

            BuildFocusFrame();
        }

        /// <summary>Quatro cantos de visor em volta da mira: fecham sobre o objeto quando ele recebe o foco.</summary>
        private void BuildFocusFrame()
        {
            GameObject go = new("Visor", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            focusFrame = (RectTransform)go.transform;
            focusFrame.anchorMin = focusFrame.anchorMax = new Vector2(0.5f, 0.5f);
            focusFrame.sizeDelta = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                float sy = i < 2 ? 1f : -1f;
                GameObject c = new("Canto" + i, typeof(RectTransform));
                c.transform.SetParent(focusFrame, false);
                RectTransform rt = (RectTransform)c.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;

                // L: um traco horizontal e um vertical saindo do canto para dentro.
                Image h = MakeImage("H", rt, null, NoirTheme.Amber);
                h.rectTransform.pivot = new Vector2(sx < 0 ? 0f : 1f, 0.5f);
                h.rectTransform.sizeDelta = new Vector2(12f, 2f);
                Image v = MakeImage("V", rt, null, NoirTheme.Amber);
                v.rectTransform.pivot = new Vector2(0.5f, sy > 0 ? 1f : 0f);
                v.rectTransform.sizeDelta = new Vector2(2f, 12f);
                v.rectTransform.anchoredPosition = new Vector2(sx < 0 ? 1f : -1f, 0f);
                h.rectTransform.anchoredPosition = new Vector2(0f, sy > 0 ? -1f : 1f);

                focusCorners.Add(rt);
                focusCornerImages.Add(h);
                focusCornerImages.Add(v);
            }

            UpdateFocusFrame(0f);
        }

        /// <summary>Anima os cantos todo frame (sem tween): fecham com o foco e respiram de leve.</summary>
        private void UpdateFocusFrame(float dt)
        {
            if (focusFrame == null)
                return;

            focusAmount = Mathf.MoveTowards(focusAmount, focusTarget, dt * 5f);
            float eased = 1f - (1f - focusAmount) * (1f - focusAmount);
            float breathe = Mathf.Sin(Time.unscaledTime * 3f) * 1.5f * focusAmount;
            float spread = Mathf.Lerp(64f, promptBlocked ? 30f : 22f, eased) + breathe;

            for (int i = 0; i < focusCorners.Count; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                float sy = i < 2 ? 1f : -1f;
                focusCorners[i].anchoredPosition = new Vector2(sx * spread, sy * spread);
            }

            bool hidden = PauseState.IsPaused || GameplayLocks.IsLocked(GameplayLock.Modal) || !GameSettings.ShowReticle;
            Color tint = promptBlocked ? NoirTheme.WithAlpha(NoirTheme.Ash, 0.8f) : NoirTheme.Amber;
            tint.a *= hidden ? 0f : eased * 0.9f;
            foreach (Image img in focusCornerImages)
                img.color = tint;
        }

        /// <summary>Encaixa sombra, losango e traco no tamanho do texto atual.</summary>
        private void LayoutOwnPrompt()
        {
            float w = Mathf.Max(80f, promptText.GetPreferredValues(promptText.text).x);
            promptShade.rectTransform.sizeDelta = new Vector2(w + 300f, 120f);
            promptDiamond.rectTransform.anchoredPosition = new Vector2(-w * 0.5f - 26f, 1f);
            promptLine.rectTransform.sizeDelta = new Vector2(w + 90f, 16f);
        }

        /// <summary>Clique possivel: papel e ambar. Bloqueado (trancado, falta item): cinza, italico, sem brilho.</summary>
        private void ApplyPromptState(bool blocked, bool animate)
        {
            promptBlocked = blocked;
            Color text = blocked ? NoirTheme.WithAlpha(NoirTheme.Ash, 0.85f) : NoirTheme.Paper;
            Color diamond = blocked ? NoirTheme.WithAlpha(NoirTheme.Blood, 0.9f) : NoirTheme.Amber;
            promptText.fontStyle = blocked ? (FontStyles.UpperCase | FontStyles.Italic) : FontStyles.UpperCase;

            promptText.DOKill();
            promptDiamond.DOKill();
            promptLine.DOKill();
            if (animate)
            {
                promptText.DOColor(text, 0.2f).SetUpdate(true);
                promptDiamond.DOColor(diamond, 0.2f).SetUpdate(true);
                promptLine.DOFade(blocked ? 0.2f : 0.55f, 0.2f).SetUpdate(true);
            }
            else
            {
                promptText.color = text;
                promptDiamond.color = diamond;
                promptLine.color = NoirTheme.WithAlpha(NoirTheme.Paper, blocked ? 0.2f : 0.55f);
            }
        }

        /// <summary>Texto novo no prompt: o traco se redesenha do centro e as letras se assentam.</summary>
        private void PlayPromptIntro(bool fresh)
        {
            RectTransform line = promptLine.rectTransform;
            line.DOKill();
            line.localScale = new Vector3(0f, 1f, 1f);
            line.DOScaleX(1f, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true);

            DOTween.Kill(promptText.rectTransform);
            promptText.characterSpacing = fresh ? 16f : 10f;
            DOTween.To(() => promptText.characterSpacing, v => promptText.characterSpacing = v, 6f, 0.35f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(promptText.rectTransform);

            RectTransform d = promptDiamond.rectTransform;
            d.DOKill();
            d.localScale = Vector3.zero;
            d.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
            d.localRotation = Quaternion.Euler(0f, 0f, -45f);
            d.DOLocalRotate(new Vector3(0f, 0f, 45f), 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);

            if (fresh)
            {
                promptRoot.DOKill();
                promptRoot.anchoredPosition = new Vector2(0f, -100f);
                promptRoot.DOAnchorPosY(-112f, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        private void LateUpdate()
        {
            if (ownPrompt)
            {
                // O interactor decide a cada frame se o clique executa; o prompt acompanha sem esperar outro evento.
                bool blocked = promptVisible && interactor != null && !interactor.FocusedCanInteract;
                if (blocked != promptBlocked)
                    ApplyPromptState(blocked, true);
                LayoutOwnPrompt();
                UpdateFocusFrame(Time.unscaledDeltaTime);
            }
            else if (promptText != null && promptPlate != null)
            {
                string current = promptText.text ?? string.Empty;
                Vector2 pref = promptText.GetPreferredValues(current);
                Vector2 size = new(Mathf.Max(240f, pref.x + 90f), Mathf.Max(60f, pref.y + 34f));
                promptPlate.rectTransform.sizeDelta = size;
                promptPlate.rectTransform.anchoredPosition = promptText.rectTransform.anchoredPosition;
                if (promptGroup != null && !promptPlate.transform.IsChildOf(promptGroup.transform))
                    promptPlate.canvasRenderer.SetAlpha(promptGroup.alpha);

                if (current != lastPrompt)
                {
                    lastPrompt = current;
                    if (!string.IsNullOrEmpty(current))
                    {
                        promptPlate.rectTransform.DOKill(true);
                        promptPlate.rectTransform.DOPunchScale(new Vector3(0.08f, 0.14f, 0f), 0.28f, 8, 0.6f).SetUpdate(true);
                        promptText.rectTransform.DOKill(true);
                        promptText.rectTransform.DOPunchAnchorPos(new Vector2(0f, 6f), 0.25f, 8, 0.6f).SetUpdate(true);
                    }
                }
            }

            if (blink != null)
            {
                bool b = blink.IsBlinking;
                if (b && !wasBlinking)
                {
                    NoirAudio.Play(NoirSfx.Blink, 0.9f);
                    NoirAtmosphere.Get().Pulse(0.35f);
                }

                wasBlinking = b;

                int m = blink.CurrentMoment;
                if (m != lastMoment)
                {
                    lastMoment = m;
                    if (cardRoutine != null)
                        StopCoroutine(cardRoutine);
                    cardRoutine = StartCoroutine(ShowCardAfter(0.35f, m));
                }
            }
        }

        // ------------------------------------------------------------ mira

        private void SetupReticle()
        {
            foreach (Graphic g in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g is Image img && img.gameObject.name == "Mira")
                {
                    reticle = img;
                    break;
                }
            }

            if (reticle == null)
                return;

            reticleBaseScale = reticle.rectTransform.localScale;
            reticleBaseColor = reticle.color;
        }

        private void OnPromptChanged(string message, bool visible)
        {
            if (ownPrompt && promptGroup != null)
            {
                bool fresh = visible && !promptVisible;
                bool changed = visible && message != promptText.text;
                if (visible)
                    promptText.text = message;

                promptVisible = visible;
                focusTarget = visible ? 1f : 0f;
                if (visible)
                {
                    ApplyPromptState(interactor != null && !interactor.FocusedCanInteract, false);
                    LayoutOwnPrompt();
                    if (fresh || changed)
                        PlayPromptIntro(fresh);
                }

                promptGroup.DOKill();
                promptGroup.DOFade(visible ? 1f : 0f, visible ? 0.18f : 0.14f).SetUpdate(true);
            }

            if (reticle != null)
            {
                reticle.rectTransform.DOKill();
                reticle.DOKill();
                reticle.rectTransform.DOScale(reticleBaseScale * (visible ? 1.35f : 1f), 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
                reticle.DOColor(visible ? NoirTheme.Amber : reticleBaseColor, 0.15f).SetUpdate(true);
            }

            if (visible)
                NoirAudio.Play(NoirSfx.Tick, 0.5f, 1.2f);
        }

        // ------------------------------------------------------------ avisos de item

        private void BuildToasts()
        {
            GameObject go = new("Toasts", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            toastRoot = (RectTransform)go.transform;
            toastRoot.anchorMin = toastRoot.anchorMax = new Vector2(1f, 1f);
            toastRoot.pivot = new Vector2(1f, 1f);
            toastRoot.anchoredPosition = new Vector2(-56f, -56f);
            toastRoot.sizeDelta = new Vector2(460f, 100f);
        }

        private void SnapshotInventory()
        {
            known.Clear();
            foreach (InventoryItemDefinition item in inventory.Items)
            {
                if (item != null)
                    known[item.Id] = item;
            }
        }

        private void OnInventoryChanged()
        {
            Dictionary<string, InventoryItemDefinition> now = new();
            foreach (InventoryItemDefinition item in inventory.Items)
            {
                if (item != null)
                    now[item.Id] = item;
            }

            foreach (KeyValuePair<string, InventoryItemDefinition> kv in now)
            {
                if (!known.ContainsKey(kv.Key))
                    ShowToast(Loc.T("NOVO ITEM"), kv.Value, NoirTheme.Amber);
            }

            foreach (KeyValuePair<string, InventoryItemDefinition> kv in known)
            {
                if (!now.ContainsKey(kv.Key))
                    ShowToast(Loc.T("USADO"), kv.Value, NoirTheme.Moon);
            }

            known.Clear();
            foreach (KeyValuePair<string, InventoryItemDefinition> kv in now)
                known[kv.Key] = kv.Value;
        }

        private void ShowToast(string header, InventoryItemDefinition item, Color accent)
        {
            if (toasts.Count >= 4)
                RemoveToast(toasts[0], true);

            bool used = accent == NoirTheme.Moon;

            // Ficha de evidencia: papel escuro, polaroide torta com o item, carimbo e nome datilografado.
            GameObject go = new("Toast", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(toastRoot, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(470f, 112f);
            CanvasGroup cg = go.GetComponent<CanvasGroup>();

            GameObject cardGo = new("Ficha", typeof(RectTransform));
            cardGo.transform.SetParent(rt, false);
            RectTransform card = (RectTransform)cardGo.transform;
            Stretch(card);
            card.pivot = new Vector2(1f, 1f);

            Image shadow = MakeImage("Sombra", card, NoirTheme.GetSprite("noir_glow"), new Color(0f, 0f, 0f, 0.55f));
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-50f, -40f);
            shadow.rectTransform.offsetMax = new Vector2(50f, 40f);

            Image paper = MakeImage("Papel", card, null, new Color(0.055f, 0.05f, 0.047f, 0.93f));
            Stretch(paper.rectTransform);

            Image frame = MakeImage("Moldura", card, NoirTheme.GetSprite("noir_btn_normal", 14f), NoirTheme.WithAlpha(NoirTheme.Paper, used ? 0.3f : 0.55f));
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1.4f;
            Stretch(frame.rectTransform);

            // Barra do tempo restante, encolhendo ate o aviso sair.
            Image timer = MakeImage("Tempo", card, null, NoirTheme.WithAlpha(accent, 0.7f));
            timer.rectTransform.anchorMin = new Vector2(0f, 0f);
            timer.rectTransform.anchorMax = new Vector2(1f, 0f);
            timer.rectTransform.pivot = new Vector2(0f, 0f);
            timer.rectTransform.offsetMin = new Vector2(10f, 7f);
            timer.rectTransform.offsetMax = new Vector2(-10f, 9f);

            float textLeft = 30f;
            RectTransform polaroid = null;
            if (item.Icon != null)
            {
                Image photo = MakeImage("Polaroide", card, null, used ? new Color(0.6f, 0.58f, 0.54f) : NoirTheme.Paper);
                polaroid = photo.rectTransform;
                polaroid.anchorMin = polaroid.anchorMax = new Vector2(0f, 0.5f);
                polaroid.anchoredPosition = new Vector2(62f, 3f);
                polaroid.sizeDelta = new Vector2(76f, 86f);
                polaroid.localRotation = Quaternion.Euler(0f, 0f, used ? 3f : -5f);

                Image well = MakeImage("Foto", polaroid, null, new Color(0.08f, 0.075f, 0.07f));
                well.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                well.rectTransform.sizeDelta = new Vector2(64f, 62f);

                Image icon = MakeImage("Icon", well.rectTransform, item.Icon, used ? new Color(0.55f, 0.55f, 0.55f) : Color.white);
                icon.preserveAspect = true;
                icon.rectTransform.sizeDelta = new Vector2(56f, 56f);
                textLeft = 120f;
            }

            // Carimbo: o cabecalho cai sobre o papel com um baque.
            GameObject stampGo = new("Carimbo", typeof(RectTransform));
            stampGo.transform.SetParent(card, false);
            RectTransform stamp = (RectTransform)stampGo.transform;
            stamp.anchorMin = stamp.anchorMax = new Vector2(0f, 1f);
            stamp.pivot = new Vector2(0f, 1f);
            stamp.anchoredPosition = new Vector2(textLeft, -14f);
            // Tarja de tinta (fundo tingido + filetes em cima e embaixo): le bem em qualquer tamanho.
            Image stampFrame = MakeImage("Tinta", stamp, null, NoirTheme.WithAlpha(accent, 0.2f));
            Stretch(stampFrame.rectTransform);
            TMP_Text h = MakeText("Header", stamp, header, 19f, Color.Lerp(accent, NoirTheme.Paper, 0.25f), 6f);
            h.fontStyle = FontStyles.Bold;
            Vector2 hSize = h.GetPreferredValues(header);
            stamp.sizeDelta = new Vector2(hSize.x + 24f, 28f);
            Stretch(h.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                Image rule = MakeImage("Filete", stampFrame.rectTransform, null, NoirTheme.WithAlpha(accent, 0.9f));
                rule.rectTransform.anchorMin = new Vector2(0f, i);
                rule.rectTransform.anchorMax = new Vector2(1f, i);
                rule.rectTransform.sizeDelta = new Vector2(0f, 2f);
                rule.rectTransform.anchoredPosition = Vector2.zero;
            }
            stamp.localRotation = Quaternion.Euler(0f, 0f, used ? 2f : -3f);

            // Traduz pelo nome exato (a chave) e so depois muda a caixa.
            string name = Loc.T(item.DisplayName).ToUpperInvariant();
            TMP_Text n = MakeText("Name", card, name, 30f, used ? NoirTheme.Ash : NoirTheme.Paper, 4f);
            n.alignment = TextAlignmentOptions.BottomLeft;
            Stretch(n.rectTransform);
            n.rectTransform.offsetMin = new Vector2(textLeft, 20f);
            n.rectTransform.offsetMax = new Vector2(-16f, -12f);
            n.maxVisibleCharacters = 0;

            toasts.Add(rt);
            LayoutToasts(rt);

            // Entrada: a ficha desliza e assenta girando de leve, como papel jogado na mesa.
            cg.alpha = 0f;
            Vector2 pos = rt.anchoredPosition;
            rt.anchoredPosition = pos + new Vector2(160f, 0f);
            card.localRotation = Quaternion.Euler(0f, 0f, 7f);
            cg.DOFade(1f, 0.22f).SetUpdate(true);
            rt.DOAnchorPos(pos, 0.55f).SetEase(Ease.OutCubic).SetUpdate(true);
            card.DOLocalRotate(Vector3.zero, 0.7f).SetEase(Ease.OutBack).SetUpdate(true);

            if (polaroid != null)
            {
                polaroid.localScale = Vector3.one * 0.6f;
                polaroid.DOScale(1f, 0.45f).SetDelay(0.12f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            stamp.localScale = Vector3.one * 1.9f;
            CanvasGroup stampGroup = stampGo.AddComponent<CanvasGroup>();
            stampGroup.alpha = 0f;
            DOTween.Sequence().SetUpdate(true).SetTarget(stamp)
                .AppendInterval(0.3f)
                .AppendCallback(() => NoirAudio.Play(NoirSfx.Place, 0.55f, 0.75f))
                .Append(stamp.DOScale(1f, 0.16f).SetEase(Ease.InCubic))
                .Join(stampGroup.DOFade(1f, 0.12f))
                .Append(card.DOPunchAnchorPos(new Vector2(0f, -4f), 0.25f, 8, 0.5f));

            // Nome batido a maquina depois do carimbo.
            DOTween.To(() => n.maxVisibleCharacters, v => n.maxVisibleCharacters = v, name.Length, Mathf.Min(0.7f, name.Length * 0.04f))
                .SetDelay(0.5f).SetEase(Ease.Linear).SetUpdate(true).SetTarget(n);

            if (used)
            {
                // Item usado: um risco atravessa o nome.
                float w = n.GetPreferredValues(name).x;
                Image strike = MakeImage("Risco", card, null, NoirTheme.WithAlpha(NoirTheme.Moon, 0.9f));
                strike.rectTransform.anchorMin = strike.rectTransform.anchorMax = new Vector2(0f, 0f);
                strike.rectTransform.pivot = new Vector2(0f, 0.5f);
                strike.rectTransform.anchoredPosition = new Vector2(textLeft - 4f, 37f);
                strike.rectTransform.sizeDelta = new Vector2(w + 8f, 2.5f);
                strike.rectTransform.localScale = new Vector3(0f, 1f, 1f);
                strike.rectTransform.DOScaleX(1f, 0.35f).SetDelay(1.1f).SetEase(Ease.InOutCubic).SetUpdate(true);
            }

            const float life = 3.6f;
            timer.rectTransform.DOScaleX(0f, life).SetDelay(0.4f).SetEase(Ease.Linear).SetUpdate(true);

            NoirAudio.Play(NoirSfx.Paper, 0.55f, used ? 0.9f : 1.1f);
            NoirAudio.Play(NoirSfx.Toast, used ? 0.5f : 0.8f);
            StartCoroutine(ExpireToast(rt, life + 0.4f));
        }

        private IEnumerator ExpireToast(RectTransform rt, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (rt != null)
                RemoveToast(rt, false);
        }

        private void RemoveToast(RectTransform rt, bool instant)
        {
            if (!toasts.Remove(rt))
                return;

            CanvasGroup cg = rt.GetComponent<CanvasGroup>();
            rt.DOKill();
            cg.DOKill();
            if (instant)
            {
                DestroyToast(rt);
            }
            else
            {
                rt.DOAnchorPosX(rt.anchoredPosition.x + 90f, 0.35f).SetEase(Ease.InBack).SetUpdate(true);
                cg.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => { if (rt != null) DestroyToast(rt); });
            }

            LayoutToasts(null);
        }

        /// <summary>Mata os tweens dos filhos (carimbo, nome, risco, barra) antes de destruir a ficha.</summary>
        private static void DestroyToast(RectTransform rt)
        {
            foreach (Component c in rt.GetComponentsInChildren<Component>(true))
                DOTween.Kill(c);
            Destroy(rt.gameObject);
        }

        private void LayoutToasts(RectTransform fresh)
        {
            for (int i = 0; i < toasts.Count; i++)
            {
                RectTransform t = toasts[i];
                float y = -i * 126f;
                if (t == fresh)
                    t.anchoredPosition = new Vector2(0f, y);
                else
                    t.DOAnchorPosY(y, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------ cartao do momento

        private void BuildMomentCard()
        {
            GameObject go = new("MomentCard", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -300f);
            rt.sizeDelta = new Vector2(1400f, 240f);
            cardGroup = go.GetComponent<CanvasGroup>();
            cardGroup.alpha = 0f;

            cardNumeral = MakeText("Numeral", rt, "I", 34f, NoirTheme.Amber, 22f);
            cardNumeral.rectTransform.anchoredPosition = new Vector2(0f, 78f);
            cardNumeral.rectTransform.sizeDelta = new Vector2(600f, 50f);

            cardTitle = MakeText("Title", rt, "", 78f, NoirTheme.Paper, 12f);
            cardTitle.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            cardTitle.rectTransform.sizeDelta = new Vector2(1400f, 110f);

            cardDivider = MakeImage("Divider", rt, NoirTheme.GetSprite("noir_divider"), Color.white);
            cardDivider.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            cardDivider.rectTransform.sizeDelta = new Vector2(640f, 24f);
        }

        private IEnumerator ShowCardAfter(float delay, int moment)
        {
            yield return new WaitForSecondsRealtime(delay);
            int i = Mathf.Clamp(moment, 0, MomentNames.Length - 1);
            cardNumeral.text = MomentNumerals[i];
            cardTitle.text = Loc.T(MomentNames[i]);

            cardGroup.DOKill();
            DOTween.Kill(cardTitle);
            cardGroup.alpha = 0f;
            cardTitle.characterSpacing = 40f;
            cardDivider.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            NoirAudio.Play(NoirSfx.Title, 0.7f);
            cardGroup.DOFade(1f, 0.7f).SetUpdate(true);
            DOTween.To(() => cardTitle.characterSpacing, v => cardTitle.characterSpacing = v, 12f, 2.4f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(cardTitle);
            cardDivider.rectTransform.DOScaleX(1f, 1f).SetDelay(0.3f).SetEase(Ease.OutCubic).SetUpdate(true);

            yield return new WaitForSecondsRealtime(2.7f);
            cardGroup.DOFade(0f, 0.9f).SetUpdate(true);
            cardRoutine = null;
        }

        // ------------------------------------------------------------ dicas

        private void BuildHints()
        {
            GameObject go = new("Hints", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(56f, 52f);
            rt.sizeDelta = new Vector2(560f, 170f);
            hintsGroup = go.GetComponent<CanvasGroup>();
            hintsGroup.alpha = 0f;

            // Linha de cima: o clique esquerdo, so quando interagir e possivel (vazia caso contrario).
            // As linhas de baixo (piscar, inventario, pausa) ficam sempre.
            hintsText = MakeText("Text", rt, string.Empty, 22f, NoirTheme.Ash, 4f);
            hintsText.alignment = TextAlignmentOptions.BottomLeft;
            hintsText.lineSpacing = 22f;
            Stretch(hintsText.rectTransform);

            inventoryKeyLabel = ReadKey(FindFirstObjectByType<RadialInventoryView>(FindObjectsInactive.Include), "inventoryKey", "TAB");
            lastHintsText = null;
            RefreshHints();

            StartCoroutine(HintsRoutine());
        }

        private static string Cap(string key)
        {
            return "<color=#DEA846>[" + key + "]</color>";
        }

        private static string ReadKey(object owner, string field, string fallback)
        {
            if (owner == null)
                return fallback;

            FieldInfo f = owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            object v = f?.GetValue(owner);
            return v != null ? v.ToString().ToUpperInvariant() : fallback;
        }

        /// <summary>Recalcula o painel de controles so quando o texto muda (chamado todo frame, custa quase nada).</summary>
        private void RefreshHints()
        {
            if (hintsText == null)
                return;

            string action = string.Empty;
            if (interactor != null && interactor.FocusedCanInteract)
                action = Cap(Loc.T("CLIQUE ESQUERDO")) + "  " + Loc.T(interactor.CurrentPrompt).ToUpperInvariant();

            // Ate o primeiro piscar, a linha de piscar fica maior e ambar: e a mecanica central do jogo.
            string blinkLine = BlinkTutorial.Learned
                ? Cap(Loc.T("CLIQUE DIREITO")) + "  " + Loc.T("PISCAR")
                : "<size=130%>" + Cap(Loc.T("CLIQUE DIREITO")) + "  <color=#F2D9A0>" + Loc.T("PISCAR") + "</color></size>";
            string text = action + "\n" +
                blinkLine + "\n" +
                Cap(inventoryKeyLabel) + "  " + Loc.T("INVENTÁRIO (SEGURAR)") + "\n" +
                Cap("ESC") + "  " + Loc.T("PAUSA");

            if (text == lastHintsText)
                return;

            lastHintsText = text;
            hintsText.text = text;
        }

        /// <summary>Os controles aparecem no comeco do jogo e depois ficam: nao somem mais.</summary>
        private IEnumerator HintsRoutine()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            hintsRequested = true;
        }

        /// <summary>Some so atras de telas cheias (documento, keypad, pausa); piscar e interagir continuam mostrados.</summary>
        private void UpdateHintsVisibility()
        {
            if (hintsGroup == null)
                return;

            bool covered = PauseState.IsPaused || GameplayLocks.IsLocked(GameplayLock.Modal);
            float target = hintsRequested && showHints && !covered ? 1f : 0f;
            hintsGroup.alpha = Mathf.MoveTowards(hintsGroup.alpha, target, Time.unscaledDeltaTime * 1.5f);
        }

        // ------------------------------------------------------------ pensamentos de Tomas

        private void BuildThoughts()
        {
            GameObject go = new("Pensamento", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 96f);
            rt.sizeDelta = new Vector2(960f, 110f);
            thoughtGroup = go.GetComponent<CanvasGroup>();
            thoughtGroup.alpha = 0f;
            thoughtGroup.blocksRaycasts = false;
            thoughtGroup.interactable = false;

            // Corpo animado separado da raiz: a raiz leva a escala da opcao "Tamanho dos pensamentos".
            GameObject body = new("Corpo", typeof(RectTransform));
            body.transform.SetParent(rt, false);
            thoughtBody = (RectTransform)body.transform;
            Stretch(thoughtBody);

            // Faixa escura suave atras do texto para ler sobre qualquer fundo.
            Image shade = MakeImage("Sombra", thoughtBody, NoirTheme.GetSprite("noir_glow"), new Color(0f, 0f, 0f, 0.7f));
            Stretch(shade.rectTransform);
            shade.rectTransform.offsetMin = new Vector2(-120f, -50f);
            shade.rectTransform.offsetMax = new Vector2(120f, 40f);

            // Aspas grandes e apagadas, como numa legenda de filme antigo.
            thoughtQuote = MakeText("Aspas", thoughtBody, "“", 120f, NoirTheme.WithAlpha(NoirTheme.Amber, 0.3f), 0f);
            thoughtQuote.rectTransform.sizeDelta = new Vector2(80f, 120f);
            thoughtQuote.fontStyle = FontStyles.Italic;

            thoughtText = MakeText("Texto", thoughtBody, string.Empty, 30f, NoirTheme.Paper, 1.5f);
            Stretch(thoughtText.rectTransform);
            thoughtText.fontStyle = FontStyles.Italic;
            thoughtText.alignment = TextAlignmentOptions.Bottom;
            thoughtText.textWrappingMode = TextWrappingModes.Normal;
            thoughtText.rectTransform.offsetMin = new Vector2(60f, 16f);
            thoughtText.rectTransform.offsetMax = new Vector2(-60f, 0f);
            thoughtText.outlineWidth = 0.12f;
            thoughtText.outlineColor = new Color32(0, 0, 0, 200);

            thoughtLine = MakeImage("Traco", thoughtBody, NoirTheme.GetSprite("noir_divider"), NoirTheme.WithAlpha(NoirTheme.Paper, 0.3f));
            thoughtLine.rectTransform.anchorMin = thoughtLine.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            thoughtLine.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            thoughtLine.rectTransform.sizeDelta = new Vector2(500f, 16f);
        }

        private void OnThought(string thought, InteractionOutcome outcome)
        {
            if (thoughtText == null)
                return;

            // O payload chega em PT (a logica e o teste automatico leem PT); traduz so na exibicao.
            string shown = Loc.T(thought);
            float typing = Mathf.Min(1.3f, shown.Length * 0.024f);
            float hold = typing + Mathf.Clamp(1.6f + shown.Length * 0.04f, 2.2f, 5f);
            bool blocked = outcome == InteractionOutcome.WrongItem || outcome == InteractionOutcome.MissingItem ||
                           outcome == InteractionOutcome.PrerequisiteMissing || outcome == InteractionOutcome.NotPossible;

            // A mesma frase repetida so prolonga o tempo na tela (sem repetir o efeito inteiro).
            // A comparacao e feita no PT.
            if (thoughtVisible && thought == currentThought)
            {
                thoughtHideAt = Time.unscaledTime + hold;
                thoughtBody.DOKill(true);
                if (blocked)
                    thoughtBody.DOShakeAnchorPos(0.3f, new Vector2(7f, 0f), 18, 0f).SetUpdate(true);
                else
                    thoughtBody.DOPunchAnchorPos(new Vector2(0f, 5f), 0.25f, 6, 0.5f).SetUpdate(true);
                return;
            }

            currentThought = thought;
            thoughtText.text = shown;
            thoughtText.color = blocked ? Color.Lerp(NoirTheme.Paper, NoirTheme.Amber, 0.3f) : NoirTheme.Paper;
            thoughtHideAt = Time.unscaledTime + hold;
            thoughtVisible = true;

            // Aspas na frente da primeira linha; traco sob o texto, na largura dele.
            thoughtText.ForceMeshUpdate();
            float width = Mathf.Min(thoughtText.GetPreferredValues(shown, 840f, 0f).x, 840f);
            float lines = Mathf.Max(1, thoughtText.textInfo.lineCount);
            thoughtQuote.rectTransform.anchoredPosition = new Vector2(-width * 0.5f - 34f, -24f + (lines - 1) * 18f);
            thoughtQuote.color = NoirTheme.WithAlpha(blocked ? NoirTheme.Blood : NoirTheme.Amber, 0.35f);
            thoughtLine.rectTransform.sizeDelta = new Vector2(width + 140f, 16f);

            thoughtGroup.DOKill();
            thoughtGroup.alpha = 0f;
            thoughtGroup.DOFade(1f, 0.25f).SetUpdate(true);

            thoughtBody.DOKill(true);
            thoughtBody.anchoredPosition = new Vector2(0f, -12f);
            thoughtBody.DOAnchorPosY(0f, 0.45f).SetEase(Ease.OutCubic).SetUpdate(true);
            thoughtText.characterSpacing = 1.5f;

            // Datilografado: a frase chega como se Tomas a pensasse ali.
            DOTween.Kill(thoughtText);
            thoughtText.maxVisibleCharacters = 0;
            DOTween.To(() => thoughtText.maxVisibleCharacters, v => thoughtText.maxVisibleCharacters = v, shown.Length, typing)
                .SetDelay(0.1f).SetEase(Ease.Linear).SetUpdate(true).SetTarget(thoughtText);

            RectTransform quote = thoughtQuote.rectTransform;
            quote.DOKill();
            quote.localScale = Vector3.one * 1.6f;
            quote.DOScale(1f, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);

            RectTransform line = thoughtLine.rectTransform;
            line.DOKill();
            line.localScale = new Vector3(0f, 1f, 1f);
            line.DOScaleX(1f, 0.6f).SetDelay(0.15f).SetEase(Ease.OutCubic).SetUpdate(true);

            if (blocked)
                thoughtBody.DOShakeAnchorPos(0.35f, new Vector2(8f, 0f), 20, 0f).SetDelay(0.1f).SetUpdate(true);

            NoirAudio.Play(NoirSfx.Thought, 0.6f);
        }

        // ------------------------------------------------------------ opcoes da pausa e FPS

        private void ApplySettings(string key)
        {
            showHints = GameSettings.ShowHints;

            if (reticle != null)
                reticle.enabled = GameSettings.ShowReticle;

            if (thoughtGroup != null)
                thoughtGroup.transform.localScale = Vector3.one * GameSettings.ThoughtScale;

            if (fpsText != null)
                fpsText.gameObject.SetActive(GameSettings.ShowFps);
        }

        private void BuildFpsCounter()
        {
            fpsText = MakeText("FPS", canvas.transform, string.Empty, 20f, NoirTheme.Ash, 3f);
            RectTransform rt = fpsText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-36f, -28f);
            rt.sizeDelta = new Vector2(220f, 36f);
            fpsText.alignment = TextAlignmentOptions.TopRight;
        }

        private void UpdateFps()
        {
            if (fpsText == null || !fpsText.gameObject.activeSelf)
                return;

            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer < 0.5f)
                return;

            int fps = Mathf.RoundToInt(fpsFrames / fpsTimer);
            fpsFrames = 0;
            fpsTimer = 0f;
            fpsText.text = fps + " FPS";
        }

        private void Update()
        {
            RefreshHints();
            UpdateHintsVisibility();
            UpdateFps();

            if (!thoughtVisible || Time.unscaledTime < thoughtHideAt)
                return;

            thoughtVisible = false;
            currentThought = null;
            // Some subindo e se desfazendo, como fumaca de cigarro.
            thoughtGroup.DOKill();
            thoughtGroup.DOFade(0f, 0.7f).SetUpdate(true);
            thoughtBody.DOKill();
            thoughtBody.DOAnchorPosY(10f, 0.7f).SetEase(Ease.InCubic).SetUpdate(true);
            DOTween.To(() => thoughtText.characterSpacing, v => thoughtText.characterSpacing = v, 5f, 0.7f)
                .SetEase(Ease.InCubic).SetUpdate(true).SetTarget(thoughtText);
        }

        // ------------------------------------------------------------ helpers

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return img;
        }

        private static TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, float spacing)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.font = NoirTheme.Font();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.richText = true;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
