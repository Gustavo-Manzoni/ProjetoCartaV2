using System;
using System.Collections.Generic;
using DG.Tweening;
using ProjetoCarta.Blink;
using ProjetoCarta.Core;
using ProjetoCarta.Inventory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>Menu de pausa noir (Esc). Construido por codigo: nao precisa de nada na cena.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string MenuScene = "MainMenu";

        private sealed class Item
        {
            public NoirButtonJuice juice;
            public Action activate;
            public Action<int> adjust;
        }

        private sealed class Page
        {
            public string subtitle;
            public CanvasGroup group;
            public readonly List<Item> items = new();
            public int index;
            public int defaultIndex;
        }

        private Canvas canvas;
        private CanvasGroup rootGroup;
        private Image dim;
        private RectTransform emblem;
        private Image emblemImage;
        private TMP_Text title;
        private TMP_Text subtitle;
        private Image divider;
        private RectTransform content;
        private TMP_Text confirmText;
        private PauseOptions options;

        private Page mainPage, optionsPage, confirmPage, current;
        private Action pendingConfirm;

        private BlinkSceneController blink;
        private RadialInventoryView radial;
        private float prevScale = 1f;
        private float duck = 1f;
        private bool open;
        private bool leaving;
        private bool rebuildPending;
        private int restoreTab = -1;
        private int restoreOption = -1;

        private void Awake()
        {
            blink = FindFirstObjectByType<BlinkSceneController>(FindObjectsInactive.Include);
            radial = FindFirstObjectByType<RadialInventoryView>(FindObjectsInactive.Include);
            ApplyVolume();
            Build();
            SetOpenVisual(false, true);
            Loc.Changed += OnLanguageChanged;
        }

        private void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;

            if (open)
            {
                GameplayLocks.Release(this);
                Time.timeScale = 1f;
                PauseState.IsPaused = false;
                duck = 1f;
                ApplyVolume();
            }

            DOTween.Kill(this);
        }

        private void ApplyVolume()
        {
            GameSettings.SetDuck(duck);
        }

        // ------------------------------------------------------------ construcao

        private void Build()
        {
            GameObject root = new("NoirPauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(NoirOwned));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            rootGroup = root.GetComponent<CanvasGroup>();

            dim = NewImage("Dim", root.transform, null, new Color(0.02f, 0.02f, 0.03f, 0.8f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            emblemImage = NewImage("Emblem", root.transform, NoirTheme.GetSprite("noir_eye"), NoirTheme.WithAlpha(NoirTheme.Paper, 0.07f));
            emblem = emblemImage.rectTransform;
            Center(emblem, new Vector2(0, 10), new Vector2(900, 900));

            content = NewRect("Content", root.transform);
            Stretch(content);

            title = NewText("Title", content, "PISCADA FATAL", 96, NoirTheme.Paper, 6f);
            Center(title.rectTransform, new Vector2(0, 330), new Vector2(1400, 130));

            divider = NewImage("Divider", content, NoirTheme.GetSprite("noir_divider"), Color.white);
            Center(divider.rectTransform, new Vector2(0, 258), new Vector2(620, 24));

            subtitle = NewText("Subtitle", content, "P A U S A", 28, NoirTheme.Amber, 16f);
            Center(subtitle.rectTransform, new Vector2(0, 214), new Vector2(900, 50));

            BuildMainPage();
            BuildOptionsPage();
            BuildConfirmPage();
        }

        private void BuildMainPage()
        {
            mainPage = NewPage("Main", "P A U S A");
            AddButton(mainPage, "CONTINUAR", new Vector2(0, 100), () => Close(), false);
            AddButton(mainPage, "OPÇÕES", new Vector2(0, 0), () => ShowPage(optionsPage), false);
            AddButton(mainPage, "MENU PRINCIPAL", new Vector2(0, -100), () =>
                AskConfirm("VOLTAR AO MENU PRINCIPAL?", "O progresso desta sessão será perdido.", LoadMenu), false);
            AddButton(mainPage, "SAIR DO JOGO", new Vector2(0, -200), () =>
                AskConfirm("SAIR DO JOGO?", "Tem certeza?", QuitGame), true);
        }

        private void BuildOptionsPage()
        {
            // Abas, linhas e dropdown ficam no PauseOptions; esta pagina so tem a moldura.
            optionsPage = NewPage("Options", "O P Ç Õ E S");
            options = new PauseOptions((RectTransform)optionsPage.group.transform);
        }

        private void BuildConfirmPage()
        {
            confirmPage = NewPage("Confirm", "C O N F I R M A R");
            confirmText = NewText("ConfirmText", confirmPage.group.transform, "", 40, NoirTheme.Paper, 4f);
            Center(confirmText.rectTransform, new Vector2(0, 110), new Vector2(1300, 160));
            confirmText.lineSpacing = 18f;
            AddButton(confirmPage, "SIM", new Vector2(0, -40), () => pendingConfirm?.Invoke(), true);
            AddButton(confirmPage, "NÃO", new Vector2(0, -140), () => ShowPage(mainPage), false);
            confirmPage.defaultIndex = 1;
        }

        private Page NewPage(string name, string sub)
        {
            RectTransform rt = NewRect("Page_" + name, content);
            Stretch(rt);
            CanvasGroup g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
            return new Page { subtitle = Loc.T(sub), group = g };
        }

        private Item AddButton(Page page, string label, Vector2 pos, Action onClick, bool danger)
        {
            GameObject go = new("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(page.group.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            Center(rt, pos, new Vector2(440, 76));
            Image hit = go.GetComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Button b = go.GetComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };

            TMP_Text t = NewText("Label", go.transform, label, 30, NoirTheme.Paper, 8f);
            Stretch(t.rectTransform);

            NoirButtonJuice juice = go.AddComponent<NoirButtonJuice>();
            juice.Danger = danger;
            if (onClick != null)
                b.onClick.AddListener(() => onClick());

            Item item = new() { juice = juice, activate = () => b.onClick.Invoke() };
            if (onClick == null)
                item.activate = null;
            page.items.Add(item);
            juice.Hovered += _ => SetIndex(page, page.items.IndexOf(item));
            return item;
        }

        // ------------------------------------------------------------ pequenos helpers de UI

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, float spacing)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.font = NoirTheme.Font();
            // Todo texto da pausa nasce aqui: traduz na criacao (os nomes dos GameObjects continuam em PT).
            t.text = Loc.T(text);
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ------------------------------------------------------------ abrir / fechar

        private void Update()
        {
            if (rebuildPending)
                Rebuild();

            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (!open)
                    TryOpen();
                else if (current != optionsPage || !options.HandleEscape())
                    Back();
                return;
            }

            if (!open || current == null)
                return;

            if (current == optionsPage)
            {
                options.HandleInput(kb);
                options.Tick();
            }
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Adjust(-1);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Adjust(1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                Activate();

            // balanco lento do emblema
            emblem.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 0.35f) * 2.5f);
            emblem.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 0.7f) * 0.012f);
        }

        private void TryOpen()
        {
            // Piscada, keypad, leitura de papel e final travam a pausa; o Esc usado para sair deles nao abre a pausa.
            if (leaving || GameplayLocks.PauseLocked || GameplayLocks.EscapeConsumedThisFrame ||
                (blink != null && blink.IsBlinking) || (radial != null && radial.IsOpen))
                return;

            open = true;
            PauseState.IsPaused = true;
            prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            GameplayLocks.Acquire(this, GameplayLock.Movement | GameplayLock.Interaction | GameplayLock.Inventory |
                                        GameplayLock.Blink | GameplayLock.Cursor);

            NoirAudio.Play(NoirSfx.Open, 1f);
            if (NoirAtmosphere.Exists)
                NoirAtmosphere.Get().Pulse(0.22f);

            DOTween.Kill(this);
            DOTween.To(() => duck, v => { duck = v; ApplyVolume(); }, 0.35f, 0.3f).SetUpdate(true).SetTarget(this);

            SetOpenVisual(true, false);
            ShowPage(mainPage, true);
        }

        private void Close()
        {
            if (!open)
                return;

            open = false;
            PauseState.IsPaused = false;
            Time.timeScale = prevScale;

            GameplayLocks.Release(this);

            NoirAudio.Play(NoirSfx.Close, 0.9f);
            DOTween.To(() => duck, v => { duck = v; ApplyVolume(); }, 1f, 0.35f).SetUpdate(true).SetTarget(this);
            SetOpenVisual(false, false);
        }

        private void SetOpenVisual(bool show, bool instant)
        {
            rootGroup.DOKill();
            dim.DOKill();
            emblemImage.DOKill();

            if (instant)
            {
                rootGroup.alpha = 0f;
                rootGroup.blocksRaycasts = false;
                rootGroup.interactable = false;
                return;
            }

            if (show)
            {
                rootGroup.blocksRaycasts = true;
                rootGroup.interactable = true;
                rootGroup.alpha = 0f;
                rootGroup.DOFade(1f, 0.2f).SetUpdate(true);
                dim.color = NoirTheme.WithAlpha(dim.color, 0f);
                dim.DOFade(0.8f, 0.3f).SetUpdate(true);

                emblem.localScale = Vector3.one * 0.85f;
                emblemImage.color = NoirTheme.WithAlpha(NoirTheme.Paper, 0f);
                emblemImage.DOFade(0.07f, 0.9f).SetUpdate(true);
                emblem.DOScale(1f, 0.9f).SetEase(Ease.OutCubic).SetUpdate(true);

                title.DOKill();
                title.rectTransform.DOKill();
                title.rectTransform.anchoredPosition = new Vector2(0, 372);
                title.rectTransform.localScale = Vector3.one;
                title.rectTransform.DOAnchorPosY(330f, 0.55f).SetEase(Ease.OutCubic).SetUpdate(true);
                title.alpha = 0f;
                DOTween.To(() => title.alpha, v => title.alpha = v, 1f, 0.45f).SetUpdate(true);
                title.characterSpacing = 26f;
                DOTween.To(() => title.characterSpacing, v => title.characterSpacing = v, 6f, 0.9f).SetEase(Ease.OutCubic).SetUpdate(true);

                divider.rectTransform.DOKill();
                divider.rectTransform.localScale = new Vector3(0, 1, 1);
                divider.rectTransform.DOScaleX(1f, 0.6f).SetDelay(0.15f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            else
            {
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
                rootGroup.DOFade(0f, 0.18f).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------ paginas

        private void ShowPage(Page page, bool first = false, int focus = -1)
        {
            SetCompactLayout(page == optionsPage, first);

            if (!first)
                NoirAudio.Play(page == mainPage ? NoirSfx.Back : NoirSfx.Click, 0.7f);

            if (current != null && current != page)
            {
                foreach (Item it in current.items)
                    it.juice.Highlight(false);
                current.group.DOKill();
                CanvasGroup old = current.group;
                old.interactable = false;
                old.blocksRaycasts = false;
                old.DOFade(0f, 0.1f).SetUpdate(true);
            }

            foreach (Item it in page.items)
                it.juice.Highlight(false);

            current = page;
            page.group.DOKill();
            page.group.alpha = 0f;
            page.group.interactable = true;
            page.group.blocksRaycasts = true;
            page.group.DOFade(1f, 0.15f).SetUpdate(true);

            subtitle.text = page.subtitle;
            subtitle.rectTransform.DOKill(true);
            subtitle.rectTransform.DOPunchAnchorPos(new Vector2(0, -8), 0.3f, 8, 0.5f).SetUpdate(true);

            if (page == confirmPage)
                confirmText.rectTransform.DOPunchScale(Vector3.one * 0.06f, 0.35f, 6, 0.6f).SetUpdate(true);

            if (page == optionsPage)
            {
                options.Show(restoreTab, restoreOption);
                restoreTab = restoreOption = -1;
            }

            for (int i = 0; i < page.items.Count; i++)
                page.items[i].juice.PlayIntro(0.05f * i + (first ? 0.25f : 0f));

            if (page.items.Count == 0)
                return;

            page.index = Mathf.Clamp(focus >= 0 ? focus : page.defaultIndex, 0, page.items.Count - 1);
            page.items[page.index].juice.Highlight(true);
        }

        /// <summary>
        /// Nas opcoes o titulo encolhe e sobe para dar lugar as abas; nas outras paginas volta ao centro.
        /// </summary>
        private void SetCompactLayout(bool compact, bool instant)
        {
            float titleY = compact ? 428f : 330f;
            float titleScale = compact ? 0.6f : 1f;
            float dividerY = compact ? 382f : 258f;
            float dividerScale = compact ? 0.7f : 1f;
            const float d = 0.45f;

            RectTransform t = title.rectTransform;
            RectTransform div = divider.rectTransform;
            Vector2 dividerSize = new(620f * dividerScale, 24f);
            subtitle.DOKill();

            // Nas opcoes o quarto quase some e o olho do emblema recua, para as linhas lerem bem.
            float dimAlpha = compact ? 0.92f : 0.8f;
            float emblemAlpha = compact ? 0.025f : 0.07f;

            if (instant)
            {
                if (compact)
                {
                    dim.DOKill();
                    emblemImage.DOKill();
                    dim.color = NoirTheme.WithAlpha(dim.color, dimAlpha);
                    emblemImage.color = NoirTheme.WithAlpha(NoirTheme.Paper, emblemAlpha);
                }

                // Na abertura o titulo ja tem a propria animacao de entrada (SetOpenVisual).
                if (compact)
                {
                    t.DOKill();
                    t.anchoredPosition = new Vector2(0f, titleY);
                    t.localScale = Vector3.one * titleScale;
                }

                div.anchoredPosition = new Vector2(0f, dividerY);
                div.sizeDelta = dividerSize;
                subtitle.alpha = compact ? 0f : 1f;
                return;
            }

            dim.DOKill();
            emblemImage.DOKill();
            dim.DOFade(dimAlpha, 0.35f).SetUpdate(true);
            emblemImage.DOFade(emblemAlpha, 0.5f).SetUpdate(true);

            t.DOKill();
            t.DOAnchorPosY(titleY, d).SetEase(Ease.OutCubic).SetUpdate(true);
            t.DOScale(titleScale, d).SetEase(Ease.OutCubic).SetUpdate(true);
            div.DOAnchorPosY(dividerY, d).SetEase(Ease.OutCubic).SetUpdate(true);
            div.DOSizeDelta(dividerSize, d).SetEase(Ease.OutCubic).SetUpdate(true);
            DOTween.To(() => subtitle.alpha, v => subtitle.alpha = v, compact ? 0f : 1f, 0.2f)
                .SetUpdate(true).SetTarget(subtitle);
        }

        private void SetIndex(Page page, int i)
        {
            if (page != current || i < 0 || i >= page.items.Count || i == page.index)
                return;

            page.items[page.index].juice.Highlight(false);
            page.index = i;
        }

        private void Move(int dir)
        {
            Page p = current;
            int count = p.items.Count;
            p.items[p.index].juice.Highlight(false);
            p.index = (p.index + dir + count) % count;
            p.items[p.index].juice.Highlight(true);
        }

        private void Adjust(int dir)
        {
            Item it = current.items[current.index];
            it.adjust?.Invoke(dir);
        }

        private void Activate()
        {
            Item it = current.items[current.index];
            if (it.adjust != null && it.activate == null)
                return;

            it.activate?.Invoke();
        }

        private void Back()
        {
            if (current == mainPage || current == null)
                Close();
            else
                ShowPage(mainPage, false, current == optionsPage ? 1 : -1);
        }

        // ------------------------------------------------------------ idioma

        private void OnLanguageChanged(Language language)
        {
            // Reconstroi no proximo frame: a troca vem de dentro do clique no dropdown, que ainda esta rodando.
            rebuildPending = true;
        }

        /// <summary>
        /// Refaz a pausa inteira no idioma novo (todos os textos nascem traduzidos) e volta para a mesma aba e linha.
        /// </summary>
        private void Rebuild()
        {
            rebuildPending = false;
            bool wasOpen = open;
            bool onOptions = current == optionsPage;
            int tab = options.TabIndex;
            int option = options.OptionIndex;

            Destroy(canvas.gameObject);
            current = null;
            Build();

            if (!wasOpen)
            {
                SetOpenVisual(false, true);
                return;
            }

            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
            if (onOptions)
            {
                restoreTab = tab;
                restoreOption = option;
                ShowPage(optionsPage, true);
            }
            else
            {
                ShowPage(mainPage, true);
            }

            // A lampada falha e as palavras voltam em outra lingua.
            NoirAudio.Play(NoirSfx.LampFlicker, 0.5f, 1.1f);
            if (NoirAtmosphere.Exists)
                NoirAtmosphere.Get().Pulse(0.3f);
            Sequence s = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            CanvasGroup cg = content.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            s.Append(cg.DOFade(1f, 0.06f));
            s.Append(cg.DOFade(0.15f, 0.06f));
            s.Append(cg.DOFade(1f, 0.08f));
            s.AppendInterval(0.06f);
            s.Append(cg.DOFade(0.4f, 0.05f));
            s.Append(cg.DOFade(1f, 0.15f));
        }

        private void AskConfirm(string line1, string line2, Action onYes)
        {
            pendingConfirm = onYes;
            confirmText.text = Loc.T(line1) + "\n<size=60%><color=#BEBAAC>" + Loc.T(line2) + "</color></size>";
            ShowPage(confirmPage);
        }

        // ------------------------------------------------------------ saidas

        private void LoadMenu()
        {
            if (leaving)
                return;

            leaving = true;
            NoirAtmosphere.Get().FadeOut(0.55f, () =>
            {
                open = false;
                PauseState.IsPaused = false;
                Time.timeScale = 1f;
                duck = 1f;
                ApplyVolume();
                GameplayLocks.Release(this);
                GameplayLocks.DisableCursorManagement();
                // Tweens da HUD/pausa iniciados neste frame nao podem sobreviver aos objetos da cena.
                DOTween.KillAll();
                SceneManager.LoadScene(MenuScene);
            });
        }

        private void QuitGame()
        {
            if (leaving)
                return;

            leaving = true;
            NoirAtmosphere.Get().FadeOut(0.5f, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
