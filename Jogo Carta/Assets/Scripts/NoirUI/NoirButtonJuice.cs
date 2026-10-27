using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Arte noir + "game juice" para um Button existente, sem alterar layout nem listeners originais.
    /// Dois estilos, escolhidos automaticamente:
    ///  - Painel (menu de pausa): moldura de papel/tinta que vira "papel claro" no hover.
    ///  - Menu (botoes com SkillButton, como o menu principal): sem moldura; sublinhado de tinta que se
    ///    desenha, losango ambar, brilho de lampada e texto ambar. A animacao original do SkillButton
    ///    (punch/escala) e preservada, pois os ornamentos vivem dentro do "Visual" e escalam junto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoirButtonJuice : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public event Action<NoirButtonJuice> Hovered;

        [SerializeField] private bool danger;
        [SerializeField] private bool playSounds = true;

        private Button button;
        private RectTransform rect;
        private RectTransform visual;
        private Image frame;
        private Image glow;
        private Image marker;
        private Image ink;
        private Image underline;
        private readonly List<TMP_Text> labels = new();
        private readonly List<Color> labelColors = new();
        private readonly List<float> labelSpacing = new();
        private bool highlighted;
        private bool built;
        private bool menuStyle;
        private Vector3 baseScale = Vector3.one;
        private Tween scaleTween;
        private Vector2 home;
        private bool hasHome;

        public bool Danger { get => danger; set { danger = value; if (built) Apply(highlighted, false); } }
        public Button Button => button;
        public bool IsMenuStyle => menuStyle;

        private Color Accent => danger ? NoirTheme.Blood : NoirTheme.Amber;

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            if (built)
                Apply(false, true);
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            if (built)
            {
                if (!menuStyle)
                    transform.localScale = baseScale;
                Apply(false, true);
            }
        }

        public void Build()
        {
            if (built)
                return;

            button = GetComponent<Button>();
            rect = transform as RectTransform;
            if (button == null || rect == null)
                return;

            built = true;
            baseScale = transform.localScale;
            menuStyle = GetComponent<SkillButton>() != null;

            if (button.targetGraphic == null && GetComponentInChildren<Graphic>(true) == null)
            {
                Image hit = gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                hit.raycastTarget = true;
                button.targetGraphic = hit;
            }

            GetComponentsInChildren<TMP_Text>(true, labels);
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                if (labels[i].GetComponentInParent<Slider>() != null)
                    labels.RemoveAt(i);
            }

            foreach (TMP_Text t in labels)
            {
                labelColors.Add(t.color);
                labelSpacing.Add(t.characterSpacing);
            }

            if (menuStyle)
                BuildMenuStyle();
            else
                BuildPanelStyle();

            button.onClick.AddListener(OnClicked);
            Apply(false, true);
        }

        // ------------------------------------------------------------ construcao

        private Image MakeImage(string name, Transform parent, string sprite, float border)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            Image img = go.GetComponent<Image>();
            img.sprite = NoirTheme.GetSprite(sprite, border);
            img.raycastTarget = false;
            return img;
        }

        private static void Anchor(RectTransform rt, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private void BuildMenuStyle()
        {
            Transform v = transform.Find("Visual");
            visual = v != null ? (RectTransform)v : rect;

            Vector2 pref = new(320f, 90f);
            if (labels.Count > 0)
            {
                Vector2 p = labels[0].GetPreferredValues(labels[0].text);
                if (p.x > 10f && p.y > 10f)
                    pref = p;
            }

            float w = pref.x;
            float h = pref.y;

            glow = MakeImage("NoirGlow", visual, "noir_glow", 0f);
            Anchor(glow.rectTransform, Vector2.zero, new Vector2(w * 1.5f + 120f, h * 1.9f), new Vector2(0.5f, 0.5f));
            glow.transform.SetAsFirstSibling();

            underline = MakeImage("NoirUnderline", visual, "noir_divider", 0f);
            Anchor(underline.rectTransform, new Vector2(0f, -h * 0.5f + 2f), new Vector2(w + 90f, 24f), new Vector2(0.5f, 0.5f));
            underline.color = NoirTheme.WithAlpha(NoirTheme.Paper, 0f);
            underline.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            marker = MakeImage("NoirMarker", visual, "noir_glow", 0f);
            Anchor(marker.rectTransform, new Vector2(-w * 0.5f - 44f, 0f), new Vector2(20f, 20f), new Vector2(0.5f, 0.5f));
            marker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);

            ink = MakeImage("NoirInk", visual, "noir_ink", 0f);
            Anchor(ink.rectTransform, Vector2.zero, new Vector2(Mathf.Max(160f, h * 1.6f), Mathf.Max(160f, h * 1.6f)), new Vector2(0.5f, 0.5f));
            ink.color = NoirTheme.WithAlpha(NoirTheme.Paper, 0f);
        }

        private void BuildPanelStyle()
        {
            visual = rect;
            glow = MakeImage("NoirGlow", transform, "noir_glow", 0f);
            glow.color = NoirTheme.WithAlpha(NoirTheme.Amber, 0f);
            frame = MakeImage("NoirFrame", transform, "noir_btn_normal", 14f);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1.4f;
            marker = MakeImage("NoirMarker", transform, "noir_glow", 0f);
            marker.color = NoirTheme.WithAlpha(NoirTheme.Amber, 0f);
            ink = MakeImage("NoirInk", transform, "noir_ink", 0f);
            ink.color = NoirTheme.WithAlpha(NoirTheme.Paper, 0f);

            Vector2 size = rect.rect.size;
            if ((size.x < 40f || size.y < 20f) && labels.Count > 0)
            {
                Vector2 pref = labels[0].GetPreferredValues(labels[0].text);
                size = new Vector2(Mathf.Max(260f, pref.x + 90f), Mathf.Max(64f, pref.y + 30f));
                Anchor(frame.rectTransform, Vector2.zero, size, new Vector2(0.5f, 0.5f));
                Anchor(glow.rectTransform, Vector2.zero, size * 1.5f, new Vector2(0.5f, 0.5f));
            }
            else
            {
                Stretch(frame.rectTransform, 6f, 3f);
                Stretch(glow.rectTransform, -34f, -26f);
            }

            RectTransform m = marker.rectTransform;
            m.anchorMin = m.anchorMax = new Vector2(0f, 0.5f);
            m.pivot = new Vector2(0.5f, 0.5f);
            m.anchoredPosition = new Vector2(20f, 0f);
            m.sizeDelta = new Vector2(16f, 16f);
            m.localRotation = Quaternion.Euler(0, 0, 45f);

            RectTransform i = ink.rectTransform;
            i.anchorMin = i.anchorMax = new Vector2(0.5f, 0.5f);
            i.sizeDelta = new Vector2(120f, 120f);

            glow.transform.SetAsFirstSibling();
            frame.transform.SetSiblingIndex(1);
            marker.transform.SetAsLastSibling();
            ink.transform.SetAsLastSibling();
        }

        private static void Stretch(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        // ------------------------------------------------------------ estados

        public void Highlight(bool on)
        {
            if (!built || highlighted == on)
                return;

            Apply(on, false);
            if (on && playSounds)
                NoirAudio.Play(NoirSfx.Hover, 0.6f, 1f);
        }

        private void Apply(bool on, bool instant)
        {
            highlighted = on;
            if (menuStyle)
                ApplyMenu(on, instant);
            else
                ApplyPanel(on, instant);
        }

        private void TweenLabels(bool on, bool instant, Color hoverColor, float extraSpacing)
        {
            float t = instant ? 0f : 0.16f;
            for (int i = 0; i < labels.Count; i++)
            {
                TMP_Text label = labels[i];
                if (label == null)
                    continue;

                Color target = on ? hoverColor : labelColors[i];
                target.a = labelColors[i].a;
                float spacing = labelSpacing[i] + (on ? extraSpacing : 0f);
                DOTween.Kill(label);
                if (instant)
                {
                    label.color = target;
                    label.characterSpacing = spacing;
                }
                else
                {
                    DOTween.To(() => label.color, c => label.color = c, target, t).SetUpdate(true).SetTarget(label);
                    DOTween.To(() => label.characterSpacing, v => label.characterSpacing = v, spacing, t * 1.6f)
                        .SetUpdate(true).SetTarget(label);
                }
            }
        }

        private void ApplyMenu(bool on, bool instant)
        {
            float t = instant ? 0f : 0.2f;
            Color accent = Accent;

            underline.DOKill();
            underline.rectTransform.DOKill();
            glow.DOKill();
            marker.DOKill();
            marker.rectTransform.DOKill();

            float w = Mathf.Abs(marker.rectTransform.anchoredPosition.x);
            float markerX = on ? -(w - 14f) : -(w + 0f);

            if (instant)
            {
                // Sem tweens de duracao zero: no OnDisable (troca de cena) eles iniciariam com o objeto ja destruido.
                underline.color = NoirTheme.WithAlpha(on ? accent : NoirTheme.Paper, on ? 1f : 0f);
                Vector3 s = underline.rectTransform.localScale;
                underline.rectTransform.localScale = new Vector3(on ? 1f : 0f, s.y, s.z);
                glow.color = NoirTheme.WithAlpha(accent, on ? 0.2f : 0f);
                marker.color = NoirTheme.WithAlpha(accent, on ? 1f : 0f);
                Vector2 p = marker.rectTransform.anchoredPosition;
                marker.rectTransform.anchoredPosition = new Vector2(markerX, p.y);
                TweenLabels(on, true, danger ? new Color(1f, 0.55f, 0.52f) : NoirTheme.Amber, 4f);
                return;
            }

            underline.DOFade(on ? 1f : 0f, t).SetUpdate(true);
            underline.DOColor(NoirTheme.WithAlpha(on ? accent : NoirTheme.Paper, on ? 1f : 0f), t).SetUpdate(true);
            underline.rectTransform.DOScaleX(on ? 1f : 0f, instant ? 0f : (on ? 0.4f : 0.2f))
                .SetEase(on ? Ease.OutCubic : Ease.InCubic).SetUpdate(true);

            glow.DOColor(NoirTheme.WithAlpha(accent, on ? 0.2f : 0f), t * 1.4f).SetUpdate(true);

            marker.DOColor(NoirTheme.WithAlpha(accent, on ? 1f : 0f), t).SetUpdate(true);
            marker.rectTransform.DOAnchorPosX(markerX, t * 1.5f).SetEase(Ease.OutBack).SetUpdate(true);

            TweenLabels(on, instant, danger ? new Color(1f, 0.55f, 0.52f) : NoirTheme.Amber, 4f);
        }

        private void ApplyPanel(bool on, bool instant)
        {
            float t = instant ? 0f : 0.14f;

            frame.sprite = NoirTheme.GetSprite(on ? "noir_btn_hover" : "noir_btn_normal", 14f);
            Color tint = on && danger ? new Color(1f, 0.72f, 0.7f, 1f) : Color.white;
            frame.DOKill();
            glow.DOKill();
            marker.DOKill();
            marker.rectTransform.DOKill();

            if (instant)
            {
                frame.color = tint;
                glow.color = NoirTheme.WithAlpha(Accent, on ? 0.3f : 0f);
                marker.color = NoirTheme.WithAlpha(Accent, on ? 1f : 0f);
                Vector2 p = marker.rectTransform.anchoredPosition;
                marker.rectTransform.anchoredPosition = new Vector2(on ? 24f : 14f, p.y);
                TweenLabels(on, true, danger ? NoirTheme.Blood * 0.85f : NoirTheme.Ink, 3f);
                return;
            }

            frame.DOColor(tint, t).SetUpdate(true);
            glow.DOColor(NoirTheme.WithAlpha(Accent, on ? 0.3f : 0f), t * 1.5f).SetUpdate(true);
            marker.DOColor(NoirTheme.WithAlpha(Accent, on ? 1f : 0f), t).SetUpdate(true);
            marker.rectTransform.DOAnchorPosX(on ? 24f : 14f, t).SetEase(Ease.OutBack).SetUpdate(true);

            TweenLabels(on, instant, danger ? NoirTheme.Blood * 0.85f : NoirTheme.Ink, 3f);

            if (!instant)
            {
                scaleTween?.Kill();
                scaleTween = transform.DOScale(baseScale * (on ? 1.045f : 1f), 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private void OnClicked()
        {
            if (playSounds)
                NoirAudio.Play(danger ? NoirSfx.Back : NoirSfx.Click, 0.9f);

            ink.rectTransform.DOKill();
            ink.DOKill();
            ink.rectTransform.localScale = Vector3.one * 0.3f;
            ink.rectTransform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
            ink.color = NoirTheme.WithAlpha(menuStyle ? Accent : (highlighted ? NoirTheme.Ink : NoirTheme.Paper), 0.55f);
            ink.rectTransform.DOScale(1.4f, 0.45f).SetEase(Ease.OutCubic).SetUpdate(true);
            ink.DOFade(0f, 0.45f).SetUpdate(true);

            if (NoirAtmosphere.Exists)
                NoirAtmosphere.Get().Pulse(menuStyle ? 0.12f : 0.06f);

            if (menuStyle)
            {
                underline.rectTransform.DOKill(true);
                underline.rectTransform.DOPunchAnchorPos(new Vector2(0f, -8f), 0.3f, 10, 0.6f).SetUpdate(true);
                underline.DOColor(NoirTheme.Paper, 0.05f).SetUpdate(true)
                    .OnComplete(() => { if (underline != null) underline.DOColor(Accent, 0.3f).SetUpdate(true); });
                return;
            }

            scaleTween?.Kill();
            transform.localScale = baseScale * 0.94f;
            scaleTween = transform.DOScale(baseScale * (highlighted ? 1.045f : 1f), 0.35f).SetEase(Ease.OutElastic).SetUpdate(true);
            frame.rectTransform.DOKill(true);
            frame.rectTransform.DOPunchAnchorPos(new Vector2(0f, -5f), 0.25f, 12, 0.6f).SetUpdate(true);
        }

        // ------------------------------------------------------------ ponteiro

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!button.IsInteractable())
                return;

            Highlight(true);
            Hovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Highlight(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (menuStyle || !button.IsInteractable())
                return;

            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale * 0.95f, 0.07f).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (menuStyle || !built)
                return;

            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale * (highlighted ? 1.045f : 1f), 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void OnSelect(BaseEventData eventData) { Highlight(true); Hovered?.Invoke(this); }
        public void OnDeselect(BaseEventData eventData) { Highlight(false); }

        // ------------------------------------------------------------ entradas animadas

        /// <summary>Entrada animada do menu de pausa.</summary>
        public void PlayIntro(float delay)
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null)
                cg = gameObject.AddComponent<CanvasGroup>();

            RunIntro(rect, cg, delay, 70f);
        }

        /// <summary>Entrada do menu principal: o botao-raiz desliza da esquerda e aparece.</summary>
        public void PlayMenuIntro(float delay)
        {
            RectTransform root = transform.parent as RectTransform;
            if (root == null)
                return;

            CanvasGroup cg = root.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = root.gameObject.AddComponent<CanvasGroup>();

            RunIntro(root, cg, delay, 140f);
        }

        private void RunIntro(RectTransform rt, CanvasGroup cg, float delay, float distance)
        {
            if (!hasHome)
            {
                home = rt.anchoredPosition;
                hasHome = true;
            }

            rt.DOKill();
            cg.DOKill();
            cg.alpha = 0f;
            rt.anchoredPosition = home + new Vector2(-distance, 0f);
            cg.DOFade(1f, 0.35f).SetDelay(delay).SetUpdate(true);
            rt.DOAnchorPos(home, 0.6f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}
