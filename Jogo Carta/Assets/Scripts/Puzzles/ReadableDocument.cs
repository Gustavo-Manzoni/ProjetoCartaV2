using DG.Tweening;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.NoirUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Papel/pista que pode ser lido em tela cheia ("Ler o papel"), mostrando a textura do documento.
    /// Com <see cref="body"/> preenchido, o texto e escrito por cima da textura (o papel) na area
    /// <see cref="bodyArea"/>, traduzido pelo Loc na hora de montar. Clique ou Esc fecha.
    /// A UI e criada por codigo na primeira leitura.
    /// </summary>
    public sealed class ReadableDocument : RaycastInteractable
    {
        [Header("Documento")]
        [SerializeField] private string title = "Papel";
        [Tooltip("Imagem do documento. Com texto, e o papel de fundo.")]
        [SerializeField] private Texture documentTexture;
        [SerializeField, TextArea(1, 2)] private string readThought;

        [Header("Texto (opcional)")]
        [Tooltip("Texto em PT escrito sobre o papel. A chave da traducao e o texto inteiro (Core/LocTable.cs).")]
        [SerializeField, TextArea(3, 14)] private string body;
        [Tooltip("Fonte do texto (ex.: manuscrita para bilhetes). Vazio = fonte da UI.")]
        [SerializeField] private TMP_FontAsset bodyFont;
        [SerializeField, Min(8f)] private float bodyFontSize = 30f;
        [Tooltip("Centraliza o texto na folha, na horizontal e na vertical.")]
        [SerializeField] private bool centerText;
        [Tooltip("Area do texto dentro do papel (0..1, origem embaixo a esquerda).")]
        [SerializeField] private Rect bodyArea = new(0.11f, 0.08f, 0.78f, 0.84f);

        private static readonly Color Dim = new(0.01f, 0.01f, 0.015f, 0.88f);
        private static readonly Color InkOnPaper = new Color32(34, 30, 28, 255);

        public string Title => title;
        public bool HasBody => !string.IsNullOrWhiteSpace(body);
        public bool IsReading => reading;

        private GameObject overlay;
        private CanvasGroup overlayGroup;
        private RectTransform documentRect;
        private bool reading;
        private float openedAt;
        private bool thoughtShown;

        public override string GetPrompt(InteractionContext context)
        {
            string prompt = InteractionPrompt;
            return string.IsNullOrWhiteSpace(prompt) || prompt == "Interagir"
                ? Loc.Format("Ler {0}", Loc.T(title).ToLowerInvariant())
                : prompt;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context) || reading)
                return;

            Open();
        }

        public void Open()
        {
            if (overlay == null)
                BuildOverlay();

            reading = true;
            openedAt = Time.unscaledTime;
            GameplayLocks.Acquire(this, GameplayLock.Modal);
            overlay.SetActive(true);
            overlayGroup.DOKill();
            overlayGroup.alpha = 0f;
            overlayGroup.DOFade(1f, 0.25f).SetUpdate(true);
            documentRect.DOKill();
            documentRect.localScale = Vector3.one * 0.92f;
            documentRect.DOScale(1f, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);
            NoirAudio.Play(NoirSfx.Paper, 0.8f);
        }

        public void Close()
        {
            if (!reading)
                return;

            reading = false;
            GameplayLocks.Release(this);
            NoirAudio.Play(NoirSfx.Paper, 0.6f, 0.9f);
            overlayGroup.DOKill();
            overlayGroup.DOFade(0f, 0.18f).SetUpdate(true).OnComplete(() =>
            {
                if (overlay != null)
                    overlay.SetActive(false);
            });

            if (!thoughtShown)
            {
                thoughtShown = true;
                TomasThoughts.Say(readThought);
            }
        }

        private void Update()
        {
            if (!reading)
                return;

            // Pequena espera para o mesmo clique que abriu nao fechar.
            if (Time.unscaledTime - openedAt < 0.2f)
                return;

            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            // So o clique esquerdo fecha: o direito e a piscada, e nao pode piscar ao fechar o documento.
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (escape)
                GameplayLocks.ConsumeEscape();

            if (escape || click)
                Close();
        }

        private void OnDisable()
        {
            if (reading)
            {
                reading = false;
                GameplayLocks.Release(this);
                if (overlay != null)
                    overlay.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (overlay != null)
                Destroy(overlay);
        }

        private void BuildOverlay()
        {
            overlay = new GameObject("Leitura_" + name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(NoirOwned));
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            overlayGroup = overlay.GetComponent<CanvasGroup>();

            Image dim = NewImage("Dim", overlay.transform, Dim);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            GameObject docGo = new("Documento", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            docGo.transform.SetParent(overlay.transform, false);
            documentRect = (RectTransform)docGo.transform;
            documentRect.anchorMin = new Vector2(0.5f, 0.08f);
            documentRect.anchorMax = new Vector2(0.5f, 0.9f);
            documentRect.sizeDelta = Vector2.zero;
            RawImage raw = docGo.GetComponent<RawImage>();
            raw.texture = documentTexture;
            raw.raycastTarget = false;
            AspectRatioFitter fitter = docGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = documentTexture != null && documentTexture.height > 0
                ? documentTexture.width / (float)documentTexture.height
                : 0.7f;

            if (HasBody)
                BuildBody(documentRect);

            TMP_Text titleText = NewText("Titulo", overlay.transform, Loc.T(title).ToUpperInvariant(), 34f, NoirTheme.Paper);
            titleText.rectTransform.anchorMin = titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.955f);
            titleText.rectTransform.sizeDelta = new Vector2(1200f, 60f);
            titleText.characterSpacing = 12f;

            TMP_Text hint = NewText("Dica", overlay.transform, Loc.T("CLIQUE OU ESC PARA FECHAR"), 20f, NoirTheme.Ash);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.035f);
            hint.rectTransform.sizeDelta = new Vector2(1200f, 40f);
            hint.characterSpacing = 8f;

            overlay.SetActive(false);
        }

        private void BuildBody(RectTransform paper)
        {
            GameObject go = new("Texto", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(paper, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = bodyArea.min;
            rect.anchorMax = bodyArea.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = bodyFont != null ? bodyFont : NoirTheme.Font();
            text.text = Loc.T(body.Replace("\r\n", "\n").Trim());
            text.color = InkOnPaper;
            text.alignment = centerText ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMax = bodyFontSize;
            text.fontSizeMin = bodyFontSize * 0.55f;
            text.lineSpacing = -4f;
            text.raycastTarget = false;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = NoirTheme.Font();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
