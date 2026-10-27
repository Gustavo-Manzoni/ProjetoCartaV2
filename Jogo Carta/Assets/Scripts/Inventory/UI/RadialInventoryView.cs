using System;
using System.Collections.Generic;
using DG.Tweening;
using ProjetoCarta.Core;
using ProjetoCarta.NoirUI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ProjetoCarta.Inventory.UI
{
    public sealed class RadialInventoryView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryController inventory;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RadialInventoryGraphic ringGraphic;
        [SerializeField] private RectTransform iconsRoot;

        [Header("Input")]
        [SerializeField] private Key inventoryKey = Key.Tab;
        [SerializeField, Min(0f)] private float centerDeadZone = 25f;
        [SerializeField, Min(0f)] private float movementRequiredToSelect = 8f;

        [Header("Icons")]
        [SerializeField, Min(1f)] private float iconSize = 64f;
        [SerializeField, Range(0.05f, 0.95f)] private float iconRadius = 0.75f;
        [SerializeField, Min(1f)] private float selectedIconScale = 1.16f;
        [SerializeField, Min(0f)] private float selectedIconOutward = 9f;
        [SerializeField, Min(0.01f)] private float iconAnimationSharpness = 16f;

        [Header("Item display text")]
        [SerializeField] private Vector2 displayTextOffset = new(0f, -52f);
        [SerializeField, Min(1f)] private float displayTextFontSize = 18f;
        [SerializeField] private Color displayTextColor = Color.white;
        [SerializeField] private Vector2 displayTextSize = new(150f, 44f);

        [Header("Other behaviours paused while open")]
        [SerializeField] private Behaviour[] behavioursDisabledWhileOpen;

        [Header("Events")]
        [SerializeField] private UnityEvent<bool> onVisibilityChanged;

        private readonly List<UnityEngine.UI.Image> iconImages = new();
        private readonly List<TMP_Text> displayTexts = new();
        private readonly List<BehaviourState> pausedBehaviours = new();
        private int selectedSegment = -1;
        private int selectionEnableFrame;
        private float mouseTravelSinceOpen;
        private bool hasIntentionalSelection;

        // Camada noir (montada em runtime sobre o que ja esta na cena): disco central, brilho e escurecimento.
        private static readonly Color IdleIconTint = new(0.82f, 0.78f, 0.7f, 0.95f);
        private RectTransform noirRoot;
        private UnityEngine.UI.Image dim;
        private UnityEngine.UI.Image veil;
        private UnityEngine.UI.Image selectionGlow;
        private RectTransform emptyHandsIcon;
        private RectTransform centerRoot;
        private TMP_Text centerTag;
        private TMP_Text centerName;
        private TMP_Text centerDescription;
        private int shownSegment = int.MinValue;

        public bool IsOpen { get; private set; }
        private bool inputBlocked;

        /// <summary>Bloqueado manualmente ou por uma trava de gameplay (keypad, pausa, piscada...).</summary>
        public bool InputBlocked => inputBlocked || ProjetoCarta.Core.GameplayLocks.InventoryLocked;
        public event Action<bool> VisibilityChanged;

        private readonly struct BehaviourState
        {
            public BehaviourState(Behaviour behaviour, bool wasEnabled)
            {
                Behaviour = behaviour;
                WasEnabled = wasEnabled;
            }

            public Behaviour Behaviour { get; }
            public bool WasEnabled { get; }
        }

        private void Awake()
        {
            BuildNoirLayer();
            SetCanvasVisible(false, true);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        private void OnEnable()
        {
            if (inventory != null)
                inventory.InventoryChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.InventoryChanged -= Refresh;

            if (Application.isPlaying && IsOpen)
                Close(false);
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (!IsOpen && !InputBlocked && Keyboard.current[inventoryKey].wasPressedThisFrame)
            {
                Open();
                return;
            }

            if (!IsOpen)
                return;

            if (!Application.isFocused)
            {
                Close(false);
                return;
            }

            DetectSelectionIntent();

            if (hasIntentionalSelection)
                UpdateSelection();

            AnimateIcons();
            UpdateCenter(hasIntentionalSelection ? selectedSegment : -1);

            if (Keyboard.current[inventoryKey].wasReleasedThisFrame)
                Close(true);
        }

        public void Open()
        {
            if (IsOpen || InputBlocked)
                return;

            IsOpen = true;
            selectedSegment = -1;
            hasIntentionalSelection = false;
            mouseTravelSinceOpen = 0f;
            selectionEnableFrame = Time.frameCount + 2;
            Refresh();

            // A roda trava movimento, interacao, piscada e pausa e libera o cursor enquanto aberta.
            ProjetoCarta.Core.GameplayLocks.Acquire(this,
                ProjetoCarta.Core.GameplayLock.Movement | ProjetoCarta.Core.GameplayLock.Interaction |
                ProjetoCarta.Core.GameplayLock.Blink | ProjetoCarta.Core.GameplayLock.Pause |
                ProjetoCarta.Core.GameplayLock.Cursor);
            if (!ProjetoCarta.Core.GameplayLocks.ManageCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            PauseConfiguredBehaviours();
            SetCanvasVisible(true);
            PlayOpenAnimation();

            if (Mouse.current != null)
                Mouse.current.WarpCursorPosition(GetRingScreenCenter());

            VisibilityChanged?.Invoke(true);
            onVisibilityChanged?.Invoke(true);
        }

        public void Close(bool equipSelection)
        {
            if (!IsOpen)
                return;

            if (equipSelection && hasIntentionalSelection && selectedSegment >= 0)
            {
                PlayEquipFeedback();
                EquipSelectedSegment();
            }
            else if (Application.isPlaying)
            {
                NoirAudio.Play(NoirSfx.Close, 0.45f, 1.1f);
            }

            IsOpen = false;
            selectedSegment = -1;
            hasIntentionalSelection = false;
            SetCanvasVisible(false);
            RestorePausedBehaviours();

            ProjetoCarta.Core.GameplayLocks.Release(this);

            if (ringGraphic != null && inventory != null)
                ringGraphic.SetSegments(inventory.Items.Count + 1, -1);

            VisibilityChanged?.Invoke(false);
            onVisibilityChanged?.Invoke(false);
        }

        public void SetInputBlocked(bool blocked)
        {
            inputBlocked = blocked;

            if (blocked && IsOpen)
                Close(false);
        }

        public void Refresh()
        {
            if (inventory == null || ringGraphic == null || iconsRoot == null)
                return;

            int segmentCount = inventory.Items.Count + 1;
            selectedSegment = Mathf.Clamp(selectedSegment, -1, segmentCount - 1);
            ringGraphic.SetSegments(segmentCount, selectedSegment);
            RebuildIcons(segmentCount);
        }

        private void DetectSelectionIntent()
        {
            if (Mouse.current == null || Time.frameCount < selectionEnableFrame || hasIntentionalSelection)
                return;

            mouseTravelSinceOpen += Mouse.current.delta.ReadValue().magnitude;
            if (mouseTravelSinceOpen >= movementRequiredToSelect)
                hasIntentionalSelection = true;
        }

        private void UpdateSelection()
        {
            if (Mouse.current == null || ringGraphic == null || inventory == null)
                return;

            Vector2 direction = Mouse.current.position.ReadValue() - GetRingScreenCenter();
            int newSelection = 0;

            if (direction.sqrMagnitude >= centerDeadZone * centerDeadZone)
            {
                int segmentCount = inventory.Items.Count + 1;
                float anglePerSegment = 360f / segmentCount;
                float mouseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float clockwiseFromFirst = Mathf.Repeat(
                    ringGraphic.FirstSegmentAngle - mouseAngle + anglePerSegment * 0.5f,
                    360f);
                newSelection = Mathf.Clamp(Mathf.FloorToInt(clockwiseFromFirst / anglePerSegment), 0, segmentCount - 1);
            }

            if (newSelection == selectedSegment)
                return;

            selectedSegment = newSelection;
            ringGraphic.SetSegments(inventory.Items.Count + 1, selectedSegment);

            // Estalo de disco de telefone, um tom por casa.
            NoirAudio.Play(NoirSfx.Tick, 0.75f, 0.85f + 0.07f * newSelection);
        }

        private Vector2 GetRingScreenCenter()
        {
            if (ringGraphic == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Canvas canvas = ringGraphic.canvas;
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                ringGraphic.rectTransform.TransformPoint(ringGraphic.rectTransform.rect.center));
        }

        private void EquipSelectedSegment()
        {
            if (inventory == null)
                return;

            if (selectedSegment == 0)
            {
                inventory.Equip(null);
                return;
            }

            int itemIndex = selectedSegment - 1;
            if (itemIndex >= 0 && itemIndex < inventory.Items.Count)
                inventory.Equip(inventory.Items[itemIndex]);
        }

        private void RebuildIcons(int segmentCount)
        {
            EnsureIconCount(inventory.Items.Count);
            float radius = GetBaseIconRadius();
            float anglePerSegment = 360f / segmentCount;

            for (int i = 0; i < iconImages.Count; i++)
            {
                bool shouldBeVisible = i < inventory.Items.Count;
                UnityEngine.UI.Image image = iconImages[i];
                TMP_Text displayText = displayTexts[i];
                image.gameObject.SetActive(shouldBeVisible);
                // O nome agora aparece no disco central; o texto sob o icone fica so como reserva.
                displayText.gameObject.SetActive(shouldBeVisible && centerName == null);

                if (!shouldBeVisible)
                    continue;

                int segmentIndex = i + 1;
                float angle = (ringGraphic.FirstSegmentAngle - segmentIndex * anglePerSegment) * Mathf.Deg2Rad;
                InventoryItemDefinition item = inventory.Items[i];
                Vector2 segmentCenter = DirectionFromAngle(angle) * radius;
                image.sprite = item.Icon;
                image.enabled = image.sprite != null;
                image.rectTransform.anchoredPosition = segmentCenter + item.IconPositionOffset;
                image.rectTransform.localEulerAngles = item.IconLocalEulerAngles;
                image.rectTransform.localScale = item.IconLocalScale;
                image.rectTransform.sizeDelta = Vector2.one * iconSize;

                displayText.text = Loc.T(item.DisplayName);
                displayText.fontSize = displayTextFontSize;
                displayText.color = displayTextColor;
                displayText.rectTransform.anchoredPosition = segmentCenter + displayTextOffset;
                displayText.rectTransform.sizeDelta = displayTextSize;
            }
        }

        private void AnimateIcons()
        {
            if (inventory == null || ringGraphic == null)
                return;

            int segmentCount = inventory.Items.Count + 1;
            float anglePerSegment = 360f / segmentCount;
            float baseRadius = GetBaseIconRadius();
            float interpolation = 1f - Mathf.Exp(-iconAnimationSharpness * Time.unscaledDeltaTime);

            for (int i = 0; i < inventory.Items.Count && i < iconImages.Count; i++)
            {
                UnityEngine.UI.Image image = iconImages[i];
                TMP_Text displayText = displayTexts[i];
                InventoryItemDefinition item = inventory.Items[i];
                int segmentIndex = i + 1;
                bool selected = segmentIndex == selectedSegment;
                float angle = (ringGraphic.FirstSegmentAngle - segmentIndex * anglePerSegment) * Mathf.Deg2Rad;
                Vector2 segmentCenter = DirectionFromAngle(angle) *
                                        (baseRadius + (selected ? selectedIconOutward : 0f));
                Vector2 targetPosition = segmentCenter + item.IconPositionOffset;
                float selectionScale = selected ? selectedIconScale : 1f;
                Vector3 targetScale = item.IconLocalScale * selectionScale;

                image.rectTransform.anchoredPosition = Vector2.Lerp(
                    image.rectTransform.anchoredPosition,
                    targetPosition,
                    interpolation);
                image.rectTransform.localScale = Vector3.Lerp(
                    image.rectTransform.localScale,
                    targetScale,
                    interpolation);
                image.color = Color.Lerp(image.color, selected ? Color.white : IdleIconTint, interpolation);
                displayText.rectTransform.anchoredPosition = Vector2.Lerp(
                    displayText.rectTransform.anchoredPosition,
                    segmentCenter + displayTextOffset,
                    interpolation);

                if (selected && selectionGlow != null)
                    selectionGlow.rectTransform.anchoredPosition = Vector2.Lerp(
                        selectionGlow.rectTransform.anchoredPosition, segmentCenter, interpolation);
            }

            // Casa 0 (maos vazias): losango vazado no lugar de um icone.
            if (emptyHandsIcon != null)
            {
                bool selected = selectedSegment == 0;
                float angle = FirstSegmentAngleRad();
                Vector2 pos = DirectionFromAngle(angle) * (baseRadius + (selected ? selectedIconOutward : 0f));
                emptyHandsIcon.anchoredPosition = Vector2.Lerp(emptyHandsIcon.anchoredPosition, pos, interpolation);
                emptyHandsIcon.localScale = Vector3.Lerp(emptyHandsIcon.localScale,
                    Vector3.one * (selected ? selectedIconScale : 1f), interpolation);
                foreach (UnityEngine.UI.Image bar in emptyHandsIcon.GetComponentsInChildren<UnityEngine.UI.Image>())
                    bar.color = Color.Lerp(bar.color, selected ? NoirTheme.Paper : IdleIconTint, interpolation);

                if (selected && selectionGlow != null)
                    selectionGlow.rectTransform.anchoredPosition = Vector2.Lerp(
                        selectionGlow.rectTransform.anchoredPosition, pos, interpolation);
            }

            if (selectionGlow != null)
            {
                bool any = hasIntentionalSelection && selectedSegment >= 0;
                float flicker = 0.32f + Mathf.PerlinNoise(Time.unscaledTime * 2.2f, 3f) * 0.12f;
                Color c = selectionGlow.color;
                c.a = Mathf.Lerp(c.a, any ? flicker : 0f, interpolation);
                selectionGlow.color = c;
            }
        }

        private float FirstSegmentAngleRad()
        {
            return ringGraphic.FirstSegmentAngle * Mathf.Deg2Rad;
        }

        private float GetBaseIconRadius()
        {
            return Mathf.Min(ringGraphic.rectTransform.rect.width, ringGraphic.rectTransform.rect.height) *
                   0.5f * iconRadius;
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void EnsureIconCount(int requiredCount)
        {
            while (iconImages.Count < requiredCount)
            {
                GameObject iconObject = new(
                    $"ItemIcon_{iconImages.Count + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image));
                iconObject.transform.SetParent(iconsRoot, false);

                UnityEngine.UI.Image image = iconObject.GetComponent<UnityEngine.UI.Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                iconImages.Add(image);

                GameObject textObject = new(
                    $"ItemDisplayText_{displayTexts.Count + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textObject.transform.SetParent(iconsRoot, false);

                TextMeshProUGUI displayText = textObject.GetComponent<TextMeshProUGUI>();
                displayText.alignment = TextAlignmentOptions.Center;
                displayText.textWrappingMode = TextWrappingModes.NoWrap;
                displayText.overflowMode = TextOverflowModes.Ellipsis;
                displayText.raycastTarget = false;
                displayText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                displayText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                displayText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                displayTexts.Add(displayText);
            }
        }

        private void PauseConfiguredBehaviours()
        {
            pausedBehaviours.Clear();
            if (behavioursDisabledWhileOpen == null)
                return;

            foreach (Behaviour behaviour in behavioursDisabledWhileOpen)
            {
                if (behaviour == null || behaviour == this || behaviour == firstPersonController)
                    continue;

                pausedBehaviours.Add(new BehaviourState(behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        private void RestorePausedBehaviours()
        {
            foreach (BehaviourState state in pausedBehaviours)
            {
                if (state.Behaviour != null)
                    state.Behaviour.enabled = state.WasEnabled;
            }

            pausedBehaviours.Clear();
        }

        private void SetCanvasVisible(bool visible, bool instant = false)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.DOKill();
            if (instant || !Application.isPlaying)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                return;
            }

            if (visible)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.16f).SetUpdate(true);
                return;
            }

            // Fecha rapido: o disco se afasta e some.
            canvasGroup.DOFade(0f, 0.14f).SetUpdate(true);
            if (noirRoot != null)
            {
                noirRoot.DOKill();
                noirRoot.DOScale(1.05f, 0.14f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------ camada noir

        /// <summary>
        /// Veste a roda da cena com a estetica do jogo: disco de telefone em baquelite, centro com nome e descricao
        /// do item, brilho de lampada no setor escolhido e a tela escurecida em volta.
        /// </summary>
        private void BuildNoirLayer()
        {
            if (canvasGroup == null || ringGraphic == null || iconsRoot == null)
                return;

            noirRoot = (RectTransform)canvasGroup.transform;
            ringGraphic.SetPalette(
                new Color(0.075f, 0.068f, 0.062f, 0.94f),
                new Color(0.62f, 0.38f, 0.12f, 1f),
                NoirTheme.WithAlpha(NoirTheme.Paper, 0.22f),
                NoirTheme.Amber,
                NoirTheme.WithAlpha(NoirTheme.Paper, 0.35f));

            // O "Fundo" da cena vira o escurecimento da tela inteira, com vinheta.
            Transform fundo = noirRoot.Find("Fundo");
            dim = fundo != null ? fundo.GetComponent<UnityEngine.UI.Image>() : null;
            if (dim != null)
            {
                RectTransform rt = dim.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(4200f, 2600f);
                rt.anchoredPosition = Vector2.zero;
                dim.sprite = NoirTheme.GetSprite("noir_vignette");
                dim.color = new Color(0f, 0f, 0f, 0.82f);
                dim.raycastTarget = false;

                // Vinheta escurece as bordas; este veu escurece o meio para a roda ler sobre qualquer parede.
                veil = NewImage("Veu", noirRoot, null, new Color(0f, 0f, 0f, 0f));
                veil.rectTransform.sizeDelta = new Vector2(4200f, 2600f);
                veil.transform.SetSiblingIndex(dim.transform.GetSiblingIndex() + 1);
            }

            float ringRadius = Mathf.Min(ringGraphic.rectTransform.rect.width, ringGraphic.rectTransform.rect.height) * 0.5f;

            // Brilho de lampada atras do icone escolhido.
            selectionGlow = NewImage("Brilho", iconsRoot, NoirTheme.GetSprite("noir_glow"), NoirTheme.WithAlpha(NoirTheme.Amber, 0f));
            selectionGlow.rectTransform.sizeDelta = Vector2.one * (iconSize * 2.6f);
            selectionGlow.transform.SetAsFirstSibling();

            // Maos vazias: losango vazado de quatro tracos.
            GameObject empty = new("MaosVazias", typeof(RectTransform));
            empty.transform.SetParent(iconsRoot, false);
            emptyHandsIcon = (RectTransform)empty.transform;
            emptyHandsIcon.sizeDelta = Vector2.one * 30f;
            for (int i = 0; i < 4; i++)
            {
                UnityEngine.UI.Image bar = NewImage("Traco" + i, emptyHandsIcon, null, IdleIconTint);
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                bar.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9f;
                bar.rectTransform.sizeDelta = new Vector2(26f, 2.5f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * i + 135f);
            }

            // Disco central: o nome e a descricao do que esta sob o cursor.
            GameObject centerGo = new("Centro", typeof(RectTransform));
            centerGo.transform.SetParent(noirRoot, false);
            centerRoot = (RectTransform)centerGo.transform;
            centerRoot.sizeDelta = Vector2.one * (ringRadius * 0.95f);

            float discSize = ringRadius * 0.96f;
            UnityEngine.UI.Image disc = NewImage("Disco", centerRoot, NoirSkin.Circle(), new Color(0.03f, 0.028f, 0.026f, 0.92f));
            disc.rectTransform.sizeDelta = Vector2.one * discSize;
            UnityEngine.UI.Image rim = NewImage("Aro", centerRoot, NoirTheme.GetSprite("noir_glow"), NoirTheme.WithAlpha(NoirTheme.Amber, 0.08f));
            rim.rectTransform.sizeDelta = Vector2.one * (discSize * 1.25f);
            rim.transform.SetAsFirstSibling();

            centerTag = NewText("Etiqueta", centerRoot, 16f, NoirTheme.Amber, 7f);
            centerTag.rectTransform.anchoredPosition = new Vector2(0f, discSize * 0.27f);
            centerTag.rectTransform.sizeDelta = new Vector2(discSize * 0.8f, 22f);

            centerName = NewText("Nome", centerRoot, 30f, NoirTheme.Paper, 3f);
            centerName.rectTransform.anchoredPosition = new Vector2(0f, discSize * 0.12f);
            centerName.rectTransform.sizeDelta = new Vector2(discSize * 0.86f, 34f);
            centerName.enableAutoSizing = true;
            centerName.fontSizeMin = 18f;
            centerName.fontSizeMax = 30f;

            UnityEngine.UI.Image line = NewImage("Traco", centerRoot, NoirTheme.GetSprite("noir_divider"), NoirTheme.WithAlpha(NoirTheme.Paper, 0.4f));
            line.rectTransform.anchoredPosition = new Vector2(0f, discSize * 0.03f);
            line.rectTransform.sizeDelta = new Vector2(discSize * 0.6f, 14f);

            centerDescription = NewText("Descricao", centerRoot, 19f, NoirTheme.Ash, 0.5f);
            centerDescription.fontStyle = FontStyles.Italic;
            centerDescription.alignment = TextAlignmentOptions.Top;
            centerDescription.textWrappingMode = TextWrappingModes.Normal;
            centerDescription.rectTransform.anchoredPosition = new Vector2(0f, -discSize * 0.17f);
            centerDescription.rectTransform.sizeDelta = new Vector2(discSize * 0.74f, discSize * 0.34f);

            foreach (TMP_Text t in displayTexts)
                t.gameObject.SetActive(false);
        }

        private void PlayOpenAnimation()
        {
            if (noirRoot == null)
                return;

            shownSegment = int.MinValue;
            UpdateCenter(-1);

            // O disco volta girando ate o lugar, como um telefone de disco soltando.
            noirRoot.DOKill();
            noirRoot.localScale = Vector3.one * 0.86f;
            noirRoot.DOScale(1f, 0.38f).SetEase(Ease.OutBack).SetUpdate(true);

            DOTween.Kill(ringGraphic);
            ringGraphic.AngleOffset = -55f;
            DOTween.To(() => ringGraphic.AngleOffset, v => ringGraphic.AngleOffset = v, 0f, 0.55f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(ringGraphic);

            if (dim != null)
            {
                dim.DOKill();
                dim.color = new Color(0f, 0f, 0f, 0f);
                dim.DOFade(0.82f, 0.3f).SetUpdate(true);
            }

            if (veil != null)
            {
                veil.DOKill();
                veil.color = new Color(0f, 0f, 0f, 0f);
                veil.DOFade(0.5f, 0.3f).SetUpdate(true);
            }

            centerRoot.DOKill();
            centerRoot.localScale = Vector3.one * 0.6f;
            centerRoot.DOScale(1f, 0.45f).SetDelay(0.05f).SetEase(Ease.OutBack).SetUpdate(true);

            NoirAudio.Play(NoirSfx.Open, 0.55f, 0.8f);
            NoirAudio.Play(NoirSfx.Rummage, 0.5f);
            if (NoirAtmosphere.Exists)
                NoirAtmosphere.Get().Pulse(0.12f);
        }

        private void PlayEquipFeedback()
        {
            InventoryItemDefinition equipped = inventory != null ? inventory.EquippedItem : null;
            if (selectedSegment == 0)
            {
                NoirAudio.Play(equipped != null ? NoirSfx.Back : NoirSfx.Tick, 0.6f, 0.8f);
                return;
            }

            int itemIndex = selectedSegment - 1;
            bool same = inventory != null && itemIndex >= 0 && itemIndex < inventory.Items.Count &&
                        equipped != null && equipped.Id == inventory.Items[itemIndex].Id;
            ringGraphic.Flash();
            NoirAudio.Play(same ? NoirSfx.Tick : NoirSfx.Equip, same ? 0.5f : 0.8f, same ? 0.7f : 1f);
        }

        /// <summary>Atualiza o disco central quando a casa sob o cursor muda (-1 = nada escolhido ainda).</summary>
        private void UpdateCenter(int segment)
        {
            if (centerName == null || inventory == null || segment == shownSegment)
                return;

            shownSegment = segment;
            InventoryItemDefinition equipped = inventory.EquippedItem;
            string tag = string.Empty;
            string name;
            string description;

            if (segment < 0)
            {
                name = equipped != null ? Loc.T(equipped.DisplayName) : Loc.T("INVENTÁRIO");
                description = Loc.T("Mova o mouse para escolher. Solte para usar.");
                tag = equipped != null ? Loc.T("NA MÃO") : string.Empty;
            }
            else if (segment == 0)
            {
                name = Loc.T("MÃOS VAZIAS");
                description = Loc.T("Guardar o que estiver na mão.");
                tag = equipped == null ? Loc.T("AGORA") : string.Empty;
            }
            else
            {
                int index = Mathf.Clamp(segment - 1, 0, inventory.Items.Count - 1);
                InventoryItemDefinition item = inventory.Items.Count > 0 ? inventory.Items[index] : null;
                name = item != null ? Loc.T(item.DisplayName) : string.Empty;
                description = item != null && !string.IsNullOrEmpty(item.Description) ? Loc.T(item.Description) : string.Empty;
                if (item != null && equipped != null && equipped.Id == item.Id)
                    tag = Loc.T("NA MÃO");
            }

            centerTag.text = tag;
            centerName.text = name.ToUpperInvariant();
            centerDescription.text = description;

            // Troca com um tranco curto e a descricao datilografada.
            centerName.rectTransform.DOKill(true);
            centerName.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.22f, 6, 0.5f).SetUpdate(true);
            centerName.alpha = 0.3f;
            DOTween.To(() => centerName.alpha, v => centerName.alpha = v, 1f, 0.15f).SetUpdate(true).SetTarget(this);

            DOTween.Kill(centerDescription);
            centerDescription.maxVisibleCharacters = 0;
            DOTween.To(() => centerDescription.maxVisibleCharacters, v => centerDescription.maxVisibleCharacters = v,
                    description.Length, Mathf.Min(0.6f, description.Length * 0.012f))
                .SetEase(Ease.Linear).SetUpdate(true).SetTarget(centerDescription);
        }

        private static UnityEngine.UI.Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, float size, Color color, float spacing)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.font = NoirTheme.Font();
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        private void OnValidate()
        {
            if (inventoryKey == Key.None)
                inventoryKey = Key.Tab;
        }
    }
}
