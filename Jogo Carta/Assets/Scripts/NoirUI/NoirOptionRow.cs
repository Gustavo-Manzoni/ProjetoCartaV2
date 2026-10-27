using System;
using DG.Tweening;
using ProjetoCarta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    public enum NoirOptionKind
    {
        Toggle,
        Choice,
        Slider,
        Dropdown,
        Button,
        Info
    }

    /// <summary>
    /// Uma linha da tela de opcoes: rotulo a esquerda, valor (ou slider) a direita, faixa de luz quando selecionada e
    /// setas nas pontas para ajustar. So visual e ponteiro; a regra de cada opcao fica em PauseOptions.
    /// </summary>
    public sealed class NoirOptionRow
    {
        public const float Width = 1080f;

        private const float LabelInset = 56f;
        private const float TrackWidth = 300f;
        private const float TrackRight = Width * 0.5f - 180f;

        public readonly RectTransform Rect;
        public readonly NoirOptionKind Kind;
        public readonly string Description;
        public readonly float Y;

        public event Action Hovered;
        public event Action Clicked;
        public event Action<int> ArrowClicked;
        public event Action<float> SliderDragged;

        public bool Selectable => Kind != NoirOptionKind.Info;
        public bool Danger { get; }

        private readonly CanvasGroup group;
        private readonly RectTransform body;
        private readonly TMP_Text label;
        private readonly TMP_Text value;
        private readonly Image band;
        private readonly Image underline;
        private readonly Image marker;
        private readonly RectTransform chevronLeft;
        private readonly RectTransform chevronRight;
        private readonly CanvasGroup chevronsGroup;
        private readonly RectTransform track;
        private readonly RectTransform fill;
        private readonly RectTransform handle;
        private readonly Image handleGlow;
        private readonly float seed;
        private bool selected;

        private Color Accent => Danger ? NoirTheme.Blood : NoirTheme.Amber;
        private bool Adjustable => Kind == NoirOptionKind.Toggle || Kind == NoirOptionKind.Choice ||
                                   Kind == NoirOptionKind.Slider || Kind == NoirOptionKind.Dropdown;

        public NoirOptionRow(Transform parent, NoirOptionKind kind, string labelPt, string descriptionPt, float y,
            float height, bool danger = false)
        {
            Kind = kind;
            Description = descriptionPt;
            Danger = danger;
            Y = y;
            seed = UnityEngine.Random.value * 50f;

            Rect = NoirUIKit.NewRect("Opcao_" + labelPt, parent);
            NoirUIKit.Center(Rect, new Vector2(0f, y), new Vector2(Width, height));
            group = Rect.gameObject.AddComponent<CanvasGroup>();

            // Area de clique da linha inteira (o slider tem a sua propria).
            Image hit = NoirUIKit.NewImage("Hit", Rect, null, new Color(0, 0, 0, 0));
            NoirUIKit.Stretch(hit.rectTransform);
            hit.raycastTarget = Selectable;
            if (Selectable)
            {
                NoirPointerRelay relay = hit.gameObject.AddComponent<NoirPointerRelay>();
                relay.Entered += () => Hovered?.Invoke();
                relay.Clicked += () => Clicked?.Invoke();
            }

            band = NoirUIKit.NewImage("Faixa", Rect, NoirTheme.GetSprite("noir_glow"), NoirTheme.WithAlpha(NoirTheme.Paper, 0f));
            NoirUIKit.Center(band.rectTransform, Vector2.zero, new Vector2(Width + 260f, height * 1.9f));

            body = NoirUIKit.NewRect("Corpo", Rect);
            NoirUIKit.Stretch(body);

            underline = NoirUIKit.NewImage("Sublinhado", body, NoirTheme.GetSprite("noir_divider"), NoirTheme.WithAlpha(NoirTheme.Paper, 0f));
            NoirUIKit.Center(underline.rectTransform, new Vector2(0f, -height * 0.5f + 2f), new Vector2(Width - 60f, 16f));
            underline.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            marker = NoirUIKit.NewImage("Marcador", body, null, NoirTheme.WithAlpha(Accent, 0f));
            bool centered = kind == NoirOptionKind.Button;
            label = NoirUIKit.NewText("Rotulo", body, Loc.T(labelPt), kind == NoirOptionKind.Info ? 25f : 29f, NoirTheme.Ash, 3f);

            if (centered)
            {
                NoirUIKit.Stretch(label.rectTransform);
                float w = label.GetPreferredValues(label.text).x;
                NoirUIKit.Center(marker.rectTransform, new Vector2(-w * 0.5f - 30f, 0f), new Vector2(11f, 11f));
            }
            else
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                NoirUIKit.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(LabelInset, 0f);
                label.rectTransform.offsetMax = new Vector2(-Width * 0.4f, 0f);
                NoirUIKit.Center(marker.rectTransform, new Vector2(-Width * 0.5f + 26f, 0f), new Vector2(11f, 11f));
            }

            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            marker.rectTransform.localScale = Vector3.zero;

            if (!centered)
            {
                value = NoirUIKit.NewText("Valor", body, string.Empty, kind == NoirOptionKind.Info ? 24f : 27f,
                    kind == NoirOptionKind.Info ? NoirTheme.Amber : NoirTheme.Paper, 3f);
                value.alignment = TextAlignmentOptions.MidlineRight;
                NoirUIKit.Stretch(value.rectTransform);
                float right = kind == NoirOptionKind.Dropdown ? 70f : LabelInset;
                value.rectTransform.offsetMin = new Vector2(Width * 0.45f, 0f);
                value.rectTransform.offsetMax = new Vector2(-right, 0f);
            }

            if (kind == NoirOptionKind.Dropdown)
            {
                RectTransform caret = NoirUIKit.Chevron("Seta", body, 14f, 2.5f, NoirTheme.Ash);
                caret.anchoredPosition = new Vector2(Width * 0.5f - 40f, -2f);
                caret.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }

            if (kind == NoirOptionKind.Slider)
            {
                value.rectTransform.offsetMin = new Vector2(Width - 160f, 0f);

                track = NoirUIKit.NewRect("Trilho", body);
                NoirUIKit.Center(track, new Vector2(TrackRight - TrackWidth * 0.5f, 0f), new Vector2(TrackWidth, 4f));
                Image bg = NoirUIKit.NewImage("Fundo", track, null, NoirTheme.WithAlpha(NoirTheme.Ash, 0.25f));
                NoirUIKit.Stretch(bg.rectTransform);

                Image fillImg = NoirUIKit.NewImage("Preenchido", track, null, NoirTheme.WithAlpha(NoirTheme.Paper, 0.85f));
                fill = fillImg.rectTransform;
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(0f, 1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(0f, 0f);

                handleGlow = NoirUIKit.NewImage("Brilho", track, NoirTheme.GetSprite("noir_glow"), NoirTheme.WithAlpha(NoirTheme.Amber, 0f));
                NoirUIKit.Center(handleGlow.rectTransform, Vector2.zero, new Vector2(70f, 70f));
                handleGlow.rectTransform.anchorMin = handleGlow.rectTransform.anchorMax = new Vector2(0f, 0.5f);

                Image h = NoirUIKit.NewImage("Puxador", track, null, NoirTheme.Paper);
                handle = h.rectTransform;
                handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
                handle.sizeDelta = new Vector2(14f, 14f);
                handle.localRotation = Quaternion.Euler(0f, 0f, 45f);

                // Area de arrasto maior que o trilho fino.
                Image dragArea = NoirUIKit.NewImage("AreaArrasto", track, null, new Color(0, 0, 0, 0));
                NoirUIKit.Center(dragArea.rectTransform, Vector2.zero, new Vector2(TrackWidth + 30f, 44f));
                dragArea.raycastTarget = true;
                NoirPointerRelay drag = dragArea.gameObject.AddComponent<NoirPointerRelay>();
                drag.Entered += () => Hovered?.Invoke();
                drag.Pressed += OnTrackPointer;
                drag.Dragged += OnTrackPointer;
            }

            // Setas nas pontas da linha (so nas opcoes ajustaveis).
            RectTransform chevrons = NoirUIKit.NewRect("Setas", Rect);
            NoirUIKit.Stretch(chevrons);
            chevronsGroup = chevrons.gameObject.AddComponent<CanvasGroup>();
            chevronsGroup.alpha = 0f;
            chevronsGroup.blocksRaycasts = Adjustable;
            if (Adjustable)
            {
                chevronLeft = ArrowButton(chevrons, -1);
                chevronRight = ArrowButton(chevrons, 1);
            }

            label.color = NoirTheme.WithAlpha(NoirTheme.Ash, kind == NoirOptionKind.Info ? 0.85f : 0.7f);
        }

        private RectTransform ArrowButton(Transform parent, int dir)
        {
            Image hit = NoirUIKit.NewImage(dir < 0 ? "SetaEsquerda" : "SetaDireita", parent, null, new Color(0, 0, 0, 0));
            NoirUIKit.Center(hit.rectTransform, new Vector2(dir * (Width * 0.5f + 26f), 0f), new Vector2(54f, 54f));
            hit.raycastTarget = true;
            NoirPointerRelay relay = hit.gameObject.AddComponent<NoirPointerRelay>();
            relay.Entered += () => Hovered?.Invoke();
            relay.Clicked += () => ArrowClicked?.Invoke(dir);

            RectTransform chevron = NoirUIKit.Chevron("Seta", hit.rectTransform, 22f, 3f, NoirTheme.Paper);
            chevron.localRotation = Quaternion.Euler(0f, 0f, dir < 0 ? 180f : 0f);
            return chevron;
        }

        private void OnTrackPointer(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera,
                    out Vector2 local))
                return;

            float t = Mathf.Clamp01((local.x + track.rect.width * track.pivot.x) / track.rect.width);
            SliderDragged?.Invoke(t);
        }

        // ------------------------------------------------------------ valor

        /// <summary>Troca o texto do valor; com direcao, o texto da um tranco para o lado do ajuste.</summary>
        public void SetValue(string text, int dir = 0)
        {
            if (value == null)
                return;

            bool changed = value.text != text;
            value.text = text;
            if (dir == 0 || !changed)
                return;

            RectTransform rt = value.rectTransform;
            rt.DOKill(true);
            rt.DOPunchAnchorPos(new Vector2(dir * 16f, 0f), 0.28f, 7, 0.4f).SetUpdate(true);
            value.DOKill();
            value.color = Accent;
            value.DOColor(NoirTheme.Paper, 0.4f).SetUpdate(true);
            Nudge(dir);
        }

        /// <summary>Posiciona o slider (0..1) e escreve o valor ao lado.</summary>
        public void SetSlider(float t, string text, bool animate)
        {
            if (track == null)
                return;

            t = Mathf.Clamp01(t);
            float x = t * TrackWidth;
            fill.DOKill();
            handle.DOKill();
            handleGlow.rectTransform.DOKill();
            if (animate)
            {
                fill.DOSizeDelta(new Vector2(x, 0f), 0.12f).SetEase(Ease.OutCubic).SetUpdate(true);
                handle.DOAnchorPos(new Vector2(x, 0f), 0.12f).SetEase(Ease.OutCubic).SetUpdate(true);
                handleGlow.rectTransform.DOAnchorPos(new Vector2(x, 0f), 0.12f).SetEase(Ease.OutCubic).SetUpdate(true);
                handle.DOPunchScale(Vector3.one * 0.45f, 0.22f, 6, 0.5f).SetUpdate(true);
            }
            else
            {
                fill.sizeDelta = new Vector2(x, 0f);
                handle.anchoredPosition = new Vector2(x, 0f);
                handleGlow.rectTransform.anchoredPosition = new Vector2(x, 0f);
            }

            value.text = text;
        }

        /// <summary>Tranco na seta do lado do ajuste.</summary>
        public void Nudge(int dir)
        {
            RectTransform c = dir < 0 ? chevronLeft : chevronRight;
            if (c == null)
                return;

            c.DOKill(true);
            c.DOPunchAnchorPos(new Vector2(dir * 12f, 0f), 0.25f, 6, 0.4f).SetUpdate(true);
        }

        /// <summary>Clarão curto de confirmacao (botoes e abrir dropdown).</summary>
        public void Flash()
        {
            body.DOKill(true);
            body.DOPunchScale(new Vector3(0.025f, 0.08f, 0f), 0.3f, 6, 0.5f).SetUpdate(true);
            band.DOKill();
            band.color = NoirTheme.WithAlpha(Accent, 0.35f);
            band.DOColor(NoirTheme.WithAlpha(NoirTheme.Paper, selected ? 0.08f : 0f), 0.5f).SetUpdate(true);
        }

        // ------------------------------------------------------------ selecao e entrada

        public void SetSelected(bool on, bool instant = false)
        {
            if (selected == on && !instant)
                return;

            selected = on;
            float d = instant ? 0f : 0.2f;

            label.DOKill();
            label.rectTransform.DOKill();
            marker.DOKill();
            marker.rectTransform.DOKill();
            underline.DOKill();
            underline.rectTransform.DOKill();
            band.DOKill();
            chevronsGroup.DOKill();

            Color labelColor = on ? NoirTheme.Paper : NoirTheme.WithAlpha(NoirTheme.Ash, 0.7f);
            float shift = on && Kind != NoirOptionKind.Button ? 12f : 0f;
            Vector2 offsetMin = label.rectTransform.offsetMin;
            Vector2 offsetMax = label.rectTransform.offsetMax;
            float baseMin = Kind == NoirOptionKind.Button ? 0f : LabelInset;
            float baseMax = Kind == NoirOptionKind.Button ? 0f : -Width * 0.4f;

            if (instant)
            {
                label.color = labelColor;
                label.characterSpacing = on ? 5f : 3f;
                label.rectTransform.offsetMin = new Vector2(baseMin + shift, offsetMin.y);
                label.rectTransform.offsetMax = new Vector2(baseMax + shift, offsetMax.y);
                marker.rectTransform.localScale = on ? Vector3.one : Vector3.zero;
                marker.color = NoirTheme.WithAlpha(Accent, on ? 1f : 0f);
                underline.rectTransform.localScale = new Vector3(on ? 1f : 0f, 1f, 1f);
                underline.color = NoirTheme.WithAlpha(NoirTheme.Paper, on ? 0.35f : 0f);
                band.color = NoirTheme.WithAlpha(NoirTheme.Paper, on ? 0.08f : 0f);
                chevronsGroup.alpha = on ? 1f : 0f;
                if (handleGlow != null)
                    handleGlow.color = NoirTheme.WithAlpha(NoirTheme.Amber, on ? 0.45f : 0f);
                return;
            }

            label.DOColor(labelColor, d).SetUpdate(true);
            DOTween.To(() => label.characterSpacing, v => label.characterSpacing = v, on ? 5f : 3f, d).SetUpdate(true).SetTarget(label);
            DOTween.To(() => label.rectTransform.offsetMin.x, v =>
            {
                label.rectTransform.offsetMin = new Vector2(v, label.rectTransform.offsetMin.y);
                label.rectTransform.offsetMax = new Vector2(baseMax + (v - baseMin), label.rectTransform.offsetMax.y);
            }, baseMin + shift, d * 1.4f).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(label.rectTransform);

            marker.rectTransform.DOScale(on ? 1f : 0f, on ? 0.3f : 0.12f).SetEase(on ? Ease.OutBack : Ease.InCubic).SetUpdate(true);
            marker.DOFade(on ? 1f : 0f, d).SetUpdate(true);
            underline.rectTransform.DOScaleX(on ? 1f : 0f, on ? 0.45f : 0.15f).SetEase(Ease.OutCubic).SetUpdate(true);
            underline.DOFade(on ? 0.35f : 0f, d).SetUpdate(true);
            band.DOFade(on ? 0.08f : 0f, on ? 0.25f : 0.15f).SetUpdate(true);
            chevronsGroup.DOFade(on ? 1f : 0f, d).SetUpdate(true);
            if (handleGlow != null)
            {
                handleGlow.DOKill();
                handleGlow.DOFade(on ? 0.45f : 0f, d).SetUpdate(true);
            }

            if (on && chevronLeft != null)
            {
                chevronLeft.DOKill(true);
                chevronRight.DOKill(true);
                chevronLeft.anchoredPosition = new Vector2(14f, 0f);
                chevronRight.anchoredPosition = new Vector2(-14f, 0f);
                chevronLeft.DOAnchorPos(Vector2.zero, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
                chevronRight.DOAnchorPos(Vector2.zero, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        /// <summary>Entrada da linha ao abrir a aba: desliza do lado da troca e acende.</summary>
        public void PlayIntro(float delay, int dir)
        {
            group.DOKill();
            body.DOKill();
            group.alpha = 0f;
            body.anchoredPosition = new Vector2(dir * 70f, 0f);
            group.DOFade(1f, 0.28f).SetDelay(delay).SetUpdate(true);
            body.DOAnchorPos(Vector2.zero, 0.42f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        /// <summary>Faixa da linha selecionada tremula como luz de abajur ruim.</summary>
        public void Tick(float time)
        {
            if (!selected || DOTween.IsTweening(band))
                return;

            float n = Mathf.PerlinNoise(time * 2.3f, seed);
            float drop = Mathf.PerlinNoise(time * 9f, seed + 3f) > 0.82f ? 0.035f : 0f;
            band.color = NoirTheme.WithAlpha(NoirTheme.Paper, 0.06f + n * 0.04f - drop);

            if (handleGlow != null && !DOTween.IsTweening(handleGlow))
                handleGlow.color = NoirTheme.WithAlpha(NoirTheme.Amber, 0.35f + n * 0.2f);
        }

        public void SetActive(bool on)
        {
            Rect.gameObject.SetActive(on);
        }
    }

    /// <summary>Atalhos de construcao de UI por codigo usados pelas opcoes da pausa.</summary>
    public static class NoirUIKit
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Texto ja traduzido por quem chama (aqui nao passa pelo Loc).</summary>
        public static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, float spacing)
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
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Seta "&gt;" feita de dois tracos (a fonte do jogo nao tem glifos de seta).</summary>
        public static RectTransform Chevron(string name, Transform parent, float size, float thickness, Color color)
        {
            RectTransform root = NewRect(name, parent);
            Center(root, Vector2.zero, new Vector2(size, size));
            float a = size * 0.5f;
            float length = a * 1.414f + thickness * 0.5f;

            Image upper = NewImage("A", root, null, color);
            Center(upper.rectTransform, new Vector2(0f, a * 0.5f), new Vector2(length, thickness));
            upper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

            Image lower = NewImage("B", root, null, color);
            Center(lower.rectTransform, new Vector2(0f, -a * 0.5f), new Vector2(length, thickness));
            lower.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return root;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
