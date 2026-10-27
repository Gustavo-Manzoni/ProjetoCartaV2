using DG.Tweening;
using ProjetoCarta.Blink;
using ProjetoCarta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Ensina a piscada, a mecanica central: depois do cartao do primeiro momento, um destaque no centro da tela
    /// (mouse com o botao direito pulsando, "PISQUE") e as palpebras fechando de leve nas bordas, como quem luta
    /// contra o sono. Some de vez no primeiro piscar. Enquanto isso, a linha de piscar das dicas fica em destaque.
    /// </summary>
    public sealed class BlinkTutorial : MonoBehaviour
    {
        /// <summary>O jogador ja piscou nesta partida (a HUD tira o destaque da dica).</summary>
        public static bool Learned { get; private set; }

        private const float StartDelay = 5.6f;

        private BlinkSceneController blink;
        private CanvasGroup group;
        private RectTransform panel;
        private Image rightButton, glow;
        private RectTransform lidTop, lidBottom;
        private float timer, lidPhase;
        private bool shown, done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Learned = false;

        private void Start()
        {
            blink = FindFirstObjectByType<BlinkSceneController>(FindObjectsInactive.Include);
            if (blink == null || Learned) { Destroy(gameObject); return; }
            Build();
        }

        private void OnDestroy()
        {
            if (group != null) group.DOKill();
            if (panel != null) panel.DOKill();
        }

        private void Build()
        {
            GameObject canvasGo = new("NoirBlinkTutorial", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(NoirOwned), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 790;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Palpebras: degrade preto vindo de cima e de baixo.
            lidTop = Lid(canvasGo.transform, LidSprite(true), true);
            lidBottom = Lid(canvasGo.transform, LidSprite(false), false);

            panel = new GameObject("Painel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, -315f);
            panel.sizeDelta = new Vector2(900f, 260f);

            // Sombra macia atras do bloco: o texto continua legivel sobre o quadro iluminado.
            Image shade = Img("Sombra", panel, NoirTheme.GetSprite("noir_dust"), new Color(0f, 0f, 0f, 0.78f));
            shade.rectTransform.anchoredPosition = new Vector2(20f, 0f);
            shade.rectTransform.sizeDelta = new Vector2(1250f, 520f);

            glow = Img("Brilho", panel, NoirTheme.GetSprite("noir_dust"), NoirTheme.WithAlpha(NoirTheme.Amber, 0f));
            glow.rectTransform.anchoredPosition = new Vector2(-250f, 10f);
            glow.rectTransform.sizeDelta = new Vector2(260f, 260f);

            // Mouse desenhado: corpo, dois botoes e o fio.
            Color ink = new(0.86f, 0.82f, 0.74f);
            Image cable = Img("Fio", panel, null, ink);
            cable.rectTransform.anchoredPosition = new Vector2(-250f, 92f);
            cable.rectTransform.sizeDelta = new Vector2(4f, 40f);
            Image body = Img("Corpo", panel, null, ink);
            body.rectTransform.anchoredPosition = new Vector2(-250f, 0f);
            body.rectTransform.sizeDelta = new Vector2(96f, 150f);
            Image inner = Img("Corpo_Dentro", body.rectTransform, null, new Color(0.06f, 0.05f, 0.05f, 0.96f));
            inner.rectTransform.sizeDelta = new Vector2(88f, 142f);
            Image left = Img("Botao_Esq", body.rectTransform, null, new Color(0.18f, 0.16f, 0.15f));
            left.rectTransform.anchoredPosition = new Vector2(-22f, 38f);
            left.rectTransform.sizeDelta = new Vector2(40f, 58f);
            rightButton = Img("Botao_Dir", body.rectTransform, null, NoirTheme.Amber);
            rightButton.rectTransform.anchoredPosition = new Vector2(22f, 38f);
            rightButton.rectTransform.sizeDelta = new Vector2(40f, 58f);

            TMP_Text title = Text("Titulo", panel, Loc.T("PISQUE"), 92f, NoirTheme.Paper, 18f);
            title.alignment = TextAlignmentOptions.Left;
            title.rectTransform.anchoredPosition = new Vector2(160f, 52f);
            title.rectTransform.sizeDelta = new Vector2(640f, 110f);

            TMP_Text key = Text("Tecla", panel, "<color=#DEA846>[" + Loc.T("CLIQUE DIREITO") + "]</color>", 30f, NoirTheme.Paper, 6f);
            key.alignment = TextAlignmentOptions.Left;
            key.rectTransform.anchoredPosition = new Vector2(160f, -18f);
            key.rectTransform.sizeDelta = new Vector2(640f, 44f);

            TMP_Text sub = Text("Sub", panel, Loc.T("Feche os olhos e o momento muda."), 26f, NoirTheme.Ash, 2f);
            sub.alignment = TextAlignmentOptions.Left;
            sub.fontStyle = FontStyles.Italic;
            sub.rectTransform.anchoredPosition = new Vector2(160f, -66f);
            sub.rectTransform.sizeDelta = new Vector2(640f, 44f);
        }

        private void Update()
        {
            if (done || group == null) return;

            if (blink.IsBlinking)
            {
                Finish();
                return;
            }

            timer += Time.unscaledDeltaTime;
            bool covered = PauseState.IsPaused || GameplayLocks.IsLocked(GameplayLock.Modal) || GameplayLocks.BlinkLocked;
            bool want = timer > StartDelay && !covered;
            if (want && !shown)
            {
                shown = true;
                panel.localScale = Vector3.one * 0.92f;
                panel.DOScale(1f, 0.6f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            group.alpha = Mathf.MoveTowards(group.alpha, want ? 1f : 0f, Time.unscaledDeltaTime * (want ? 1.2f : 3f));
            if (!shown) return;

            // Botao direito pulsando e brilho atras do mouse.
            float t = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5.2f);
            rightButton.color = Color.Lerp(NoirTheme.Amber * 0.55f, NoirTheme.Amber * 1.25f, pulse);
            glow.color = NoirTheme.WithAlpha(NoirTheme.Amber, 0.08f + 0.14f * pulse);

            // Palpebras pesando: a cada ~3 s descem um pouco e voltam, como quem quase pisca.
            lidPhase += Time.unscaledDeltaTime;
            float cycle = lidPhase % 3.4f;
            float close = cycle < 0.35f ? Mathf.SmoothStep(0f, 1f, cycle / 0.35f)
                        : cycle < 0.55f ? 1f
                        : cycle < 1.2f ? 1f - Mathf.SmoothStep(0f, 1f, (cycle - 0.55f) / 0.65f) : 0f;
            float h = 140f + close * 230f;
            lidTop.sizeDelta = new Vector2(0f, h);
            lidBottom.sizeDelta = new Vector2(0f, h);
        }

        private void Finish()
        {
            done = true;
            Learned = true;
            group.DOKill();
            panel.DOKill();
            panel.DOScale(1.15f, 0.45f).SetEase(Ease.OutCubic).SetUpdate(true);
            group.DOFade(0f, 0.45f).SetUpdate(true).OnComplete(() => Destroy(gameObject));
        }

        // ------------------------------------------------------------ pecas

        private static readonly Sprite[] lidSprites = new Sprite[2];

        /// <summary>Degrade preto, opaco na borda da tela (em cima para a palpebra de cima, embaixo para a de baixo).</summary>
        private static Sprite LidSprite(bool top)
        {
            int i = top ? 0 : 1;
            if (lidSprites[i] != null) return lidSprites[i];
            Texture2D tex = new(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float edge = top ? y / 63f : 1f - y / 63f;
                float a = Mathf.Pow(edge, 1.4f);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, a * 0.92f));
            }
            tex.Apply();
            lidSprites[i] = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
            return lidSprites[i];
        }

        private static RectTransform Lid(Transform parent, Sprite sprite, bool top)
        {
            Image img = Img(top ? "Palpebra_Cima" : "Palpebra_Baixo", parent, sprite, Color.white);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 140f);
            return rt;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            return img;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, float spacing)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.font = NoirTheme.Font();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return t;
        }
    }
}
