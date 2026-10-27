using System;
using System.Collections.Generic;
using DG.Tweening;
using ProjetoCarta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Tela de opcoes da pausa: abas (Jogo, Audio, Video, Controles, Teclado) trocadas pelas setas do cabecalho ou
    /// Q/E, linhas de opcao navegaveis por teclado e mouse e um dropdown proprio (idioma, resolucao).
    /// Tudo salvo pelo GameSettings, entao vale para o resto do jogo e para o menu principal.
    /// </summary>
    public sealed class PauseOptions
    {
        private const float HeaderY = 322f;
        private const float PagerY = 276f;
        private const float RowsTop = 200f;
        private const float RowStep = 60f;
        private const float InfoStep = 47f;
        private const float DescriptionY = -362f;
        private const float FooterY = -452f;
        private const int PopupVisible = 7;
        private const float PopupEntryHeight = 46f;

        private sealed class Option
        {
            public NoirOptionRow row;
            public Action<int> adjust;
            public Action activate;
            public Action<bool> refresh;
            public Action reset;
            public Func<string[]> popupLabels;
            public Func<int> popupGet;
            public Action<int> popupSet;
        }

        private sealed class Tab
        {
            public string title;
            public string idleDescription;
            public RectTransform root;
            public CanvasGroup group;
            public readonly List<Option> options = new();
            public readonly List<CanvasGroup> extras = new();
            public float nextY = RowsTop;
            public int index = -1;
        }

        private readonly RectTransform root;
        private readonly float blockerMargin;
        private readonly List<Tab> tabs = new();
        private readonly List<Image> pagerDots = new();
        private readonly FirstPersonController player;

        private TMP_Text header;
        private RectTransform headerLeft;
        private RectTransform headerRight;
        private TMP_Text description;
        private int tabIndex;

        // dropdown
        private RectTransform popupRoot;
        private RectTransform popupPanel;
        private readonly List<TMP_Text> popupTexts = new();
        private readonly List<Image> popupBands = new();
        private readonly List<Image> popupMarks = new();
        private RectTransform popupUp;
        private RectTransform popupDown;
        private Option popupOption;
        private string[] popupItems;
        private int popupCursor;
        private int popupTop;

        // resolucoes disponiveis (w, h), da maior para a menor
        private readonly List<Vector2Int> resolutions = new();
        private int resolutionIndex;
        private int lastSliderTick = -1;

        public int TabIndex => tabIndex;
        public int OptionIndex => tabs[tabIndex].index;
        public bool PopupOpen => popupOption != null;

        /// <param name="includeCredits">Aba extra de creditos: so no menu principal, nunca na pausa do jogo.</param>
        public PauseOptions(RectTransform parent, float popupBlockerMargin = 2000f, bool includeCredits = false)
        {
            root = parent;
            blockerMargin = popupBlockerMargin;
            player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            CollectResolutions();

            BuildHeader();
            BuildGameTab();
            BuildAudioTab();
            BuildVideoTab();
            BuildControlsTab();
            BuildKeysTab();
            if (includeCredits)
                BuildCreditsTab();
            BuildPager();
            BuildFooter();
            BuildPopup();

            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].group.alpha = 0f;
                tabs[i].root.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------ construcao: moldura

        private void BuildHeader()
        {
            header = NoirUIKit.NewText("Aba", root, string.Empty, 44f, NoirTheme.Paper, 22f);
            NoirUIKit.Center(header.rectTransform, new Vector2(0f, HeaderY), new Vector2(700f, 70f));

            headerLeft = HeaderArrow(-1);
            headerRight = HeaderArrow(1);

            description = NoirUIKit.NewText("Descricao", root, string.Empty, 24f, NoirTheme.WithAlpha(NoirTheme.Ash, 0.9f), 1.5f);
            NoirUIKit.Center(description.rectTransform, new Vector2(0f, DescriptionY), new Vector2(1200f, 60f));
            description.fontStyle = FontStyles.Italic;
        }

        /// <summary>Um losango por aba, centrados sob o titulo (5 na pausa, 6 no menu com creditos).</summary>
        private void BuildPager()
        {
            const float spacing = 30f;
            float first = -(tabs.Count - 1) * 0.5f * spacing;
            for (int i = 0; i < tabs.Count; i++)
            {
                Image dot = NoirUIKit.NewImage("Pagina" + i, root, null, NoirTheme.WithAlpha(NoirTheme.Ash, 0.35f));
                NoirUIKit.Center(dot.rectTransform, new Vector2(first + i * spacing, PagerY), new Vector2(9f, 9f));
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                pagerDots.Add(dot);
            }
        }

        private RectTransform HeaderArrow(int dir)
        {
            Image hit = NoirUIKit.NewImage(dir < 0 ? "AbaAnterior" : "AbaSeguinte", root, null, new Color(0, 0, 0, 0));
            NoirUIKit.Center(hit.rectTransform, new Vector2(dir * 300f, HeaderY), new Vector2(90f, 90f));
            hit.raycastTarget = true;
            NoirPointerRelay relay = hit.gameObject.AddComponent<NoirPointerRelay>();
            relay.Clicked += () => ChangeTab(dir);
            relay.Entered += () => NoirAudio.Play(NoirSfx.Hover, 0.4f, 1.1f);

            RectTransform chevron = NoirUIKit.Chevron("Seta", hit.rectTransform, 30f, 3.5f, NoirTheme.Paper);
            chevron.localRotation = Quaternion.Euler(0f, 0f, dir < 0 ? 180f : 0f);

            TMP_Text key = NoirUIKit.NewText("Tecla", hit.rectTransform, dir < 0 ? "Q" : "E", 17f, NoirTheme.WithAlpha(NoirTheme.Amber, 0.8f), 2f);
            NoirUIKit.Center(key.rectTransform, new Vector2(0f, -38f), new Vector2(40f, 24f));
            return chevron;
        }

        private void BuildFooter()
        {
            string Key(string k) => "<color=#DEA846>" + k + "</color> ";
            string text = Key("W/S") + Loc.T("ESCOLHER") + "      " +
                          Key("A/D") + Loc.T("AJUSTAR") + "      " +
                          Key("Q/E") + Loc.T("TROCAR ABA") + "      " +
                          Key("ENTER") + Loc.T("CONFIRMAR") + "      " +
                          Key("ESC") + Loc.T("VOLTAR");
            TMP_Text footer = NoirUIKit.NewText("Rodape", root, text, 19f, NoirTheme.WithAlpha(NoirTheme.Ash, 0.75f), 3f);
            footer.richText = true;
            NoirUIKit.Center(footer.rectTransform, new Vector2(0f, FooterY), new Vector2(1500f, 40f));
        }

        private Tab NewTab(string title, string idleDescription)
        {
            RectTransform rt = NoirUIKit.NewRect("Aba_" + title, root);
            NoirUIKit.Stretch(rt);
            Tab tab = new() { title = title, idleDescription = idleDescription, root = rt, group = rt.gameObject.AddComponent<CanvasGroup>() };
            tabs.Add(tab);
            return tab;
        }

        // ------------------------------------------------------------ abas

        private void BuildGameTab()
        {
            Tab t = NewTab("JOGO", null);

            AddDropdown(t, "IDIOMA", "O idioma dos textos, das placas e dos pensamentos de Tomás.",
                () => new[] { "Português", "English" },
                () => Loc.Current == Language.EN ? 1 : 0,
                i => Loc.SetLanguage(i == 1 ? Language.EN : Language.PT),
                null);

            AddToggle(t, "PAINEL DE CONTROLES", "As teclas de piscar, inventário e pausa no canto da tela.",
                () => GameSettings.ShowHints, v => GameSettings.SetBool(GameSettings.ShowHintsKey, v),
                () => GameSettings.ResetKey(GameSettings.ShowHintsKey));

            AddToggle(t, "MIRA", "O ponto no centro da tela. Ele acende quando algo pode ser tocado.",
                () => GameSettings.ShowReticle, v => GameSettings.SetBool(GameSettings.ShowReticleKey, v),
                () => GameSettings.ResetKey(GameSettings.ShowReticleKey));

            AddChoice(t, "TAMANHO DOS PENSAMENTOS", "O tamanho do texto quando Tomás pensa em voz alta.",
                () => new[] { "PEQUENO", "NORMAL", "GRANDE" },
                () => Mathf.Clamp(GameSettings.GetInt(GameSettings.ThoughtSizeKey, 1), 0, 2),
                i => GameSettings.SetInt(GameSettings.ThoughtSizeKey, i),
                () => GameSettings.ResetKey(GameSettings.ThoughtSizeKey), false);

            AddToggle(t, "MOSTRAR FPS", "Um contador de quadros por segundo no canto superior.",
                () => GameSettings.ShowFps, v => GameSettings.SetBool(GameSettings.ShowFpsKey, v),
                () => GameSettings.SetBool(GameSettings.ShowFpsKey, false));

            AddResetButton(t);
        }

        private void BuildAudioTab()
        {
            Tab t = NewTab("ÁUDIO", null);

            AddVolume(t, "VOLUME GERAL", "Tudo o que a cela tem a dizer.", VolumeChannel.Master);
            AddVolume(t, "MÚSICA E AMBIENTE", "A chuva, o zumbido da cadeira e o silêncio entre eles.", VolumeChannel.Music);
            AddVolume(t, "EFEITOS SONOROS", "Gavetas, chaves, papéis e o estalo da piscada.", VolumeChannel.Sfx);

            AddToggle(t, "SOM EM SEGUNDO PLANO", "Continua tocando quando a janela do jogo perde o foco.",
                () => !GameSettings.MuteUnfocused, v => GameSettings.SetBool(GameSettings.MuteUnfocusedKey, !v),
                () => GameSettings.ResetKey(GameSettings.MuteUnfocusedKey));

            AddResetButton(t);
        }

        private void BuildVideoTab()
        {
            Tab t = NewTab("VÍDEO", null);

            AddChoice(t, "MODO DE TELA", "Como o jogo ocupa o monitor.",
                () => new[] { "Tela cheia", "Tela cheia exclusiva", "Janela maximizada", "Janela" },
                CurrentScreenModeIndex,
                i =>
                {
                    if (SettingsManager.TryGetScreenMode(i, out FullScreenMode mode))
                        GameSettings.SetScreenMode(i, mode);
                },
                null, true);

            AddDropdown(t, "RESOLUÇÃO", "O tamanho da imagem em pixels.",
                ResolutionLabels,
                () => resolutionIndex,
                i =>
                {
                    resolutionIndex = Mathf.Clamp(i, 0, resolutions.Count - 1);
                    Vector2Int r = resolutions[resolutionIndex];
                    GameSettings.SetResolution(r.x, r.y);
                },
                null);

            AddChoice(t, "QUALIDADE", "Sombras, luzes e detalhes. Baixe se o jogo engasgar.",
                QualityLabels,
                () => GameSettings.QualityLevel,
                GameSettings.SetQuality,
                null, false);

            AddChoice(t, "SUAVIZAÇÃO DE BORDAS", "Apara os serrilhados das bordas dos objetos.",
                () => new[] { "DESLIGADA", "FXAA", "SMAA" },
                () => GameSettings.AntiAliasing,
                GameSettings.SetAntiAliasing,
                () => GameSettings.SetAntiAliasing(2), false);

            AddToggle(t, "SINCRONIA VERTICAL", "Evita que a imagem rasgue. Ligada, ignora o limite de FPS.",
                () => GameSettings.VSync, GameSettings.SetVSync,
                () => GameSettings.SetVSync(true));

            AddChoice(t, "LIMITE DE FPS", "Quantos quadros por segundo o jogo tenta desenhar.",
                FpsLabels,
                () => Mathf.Max(0, Array.IndexOf(GameSettings.FpsLimits, GameSettings.FpsLimit)),
                i => GameSettings.SetFpsLimit(GameSettings.FpsLimits[i]),
                () => GameSettings.SetFpsLimit(0), false);

            AddSlider(t, "BRILHO", "Ajuste até a sombra do canto quase sumir. Quase.",
                0f, 1f, 0.05f,
                () => GameSettings.Brightness,
                v => GameSettings.SetFloat(GameSettings.BrightnessKey, v),
                Percent,
                () => GameSettings.ResetKey(GameSettings.BrightnessKey));

            AddSlider(t, "GRÃO DE FILME", "A textura de película velha por cima da imagem.",
                0f, 1f, 0.05f,
                () => GameSettings.FilmGrain,
                v => GameSettings.SetFloat(GameSettings.FilmGrainKey, v),
                Percent,
                () => GameSettings.ResetKey(GameSettings.FilmGrainKey));

            AddResetButton(t);
        }

        private void BuildControlsTab()
        {
            Tab t = NewTab("CONTROLES", null);

            AddSlider(t, "SENSIBILIDADE DO MOUSE", "A velocidade com que Tomás vira a cabeça.",
                GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity, 0.1f,
                () => GameSettings.MouseSensitivity,
                v => GameSettings.SetFloat(GameSettings.MouseSensitivityKey, v),
                v => Decimal(v) + "×",
                () => GameSettings.ResetKey(GameSettings.MouseSensitivityKey));

            AddToggle(t, "INVERTER EIXO VERTICAL", "Mouse para cima olha para baixo.",
                () => GameSettings.InvertY, v => GameSettings.SetBool(GameSettings.InvertYKey, v),
                () => GameSettings.ResetKey(GameSettings.InvertYKey));

            AddSlider(t, "CAMPO DE VISÃO", "O quanto da sala cabe na tela de uma vez.",
                GameSettings.MinFov, GameSettings.MaxFov, 1f,
                () => GameSettings.Fov > 0f ? GameSettings.Fov : (player != null ? player.SceneFov : 60f),
                v => GameSettings.SetFloat(GameSettings.FovKey, v),
                v => Mathf.RoundToInt(v) + "°",
                () => GameSettings.ResetKey(GameSettings.FovKey));

            AddToggle(t, "BALANÇO DA CÂMERA", "O balanço dos passos e a inclinação nas curvas. Desligue se enjoar.",
                () => GameSettings.HeadBob, v => GameSettings.SetBool(GameSettings.HeadBobKey, v),
                () => GameSettings.ResetKey(GameSettings.HeadBobKey));

            AddResetButton(t);
        }

        private void BuildKeysTab()
        {
            Tab t = NewTab("TECLADO", "As mãos de Tomás. Ainda não dá para trocá-las.");
            t.nextY = RowsTop + 6f;

            AddInfo(t, "ANDAR", "W A S D");
            AddInfo(t, "OLHAR", Loc.T("MOUSE"));
            AddInfo(t, "CORRER", "SHIFT");
            AddInfo(t, "AGACHAR", "CTRL");
            AddInfo(t, "PULAR", Loc.T("ESPAÇO"));
            AddInfo(t, "ZOOM", Loc.T("BOTÃO DO MEIO (SEGURAR)"));
            AddInfo(t, "INTERAGIR", Loc.T("CLIQUE ESQUERDO"));
            AddInfo(t, "PISCAR", Loc.T("CLIQUE DIREITO"));
            AddInfo(t, "INVENTÁRIO", Loc.T("TAB (SEGURAR)"));
            AddInfo(t, "PAUSA", "ESC");
        }

        /// <summary>Creditos: so os dois nomes, centralizados, sem cargos nem outras linhas.</summary>
        private void BuildCreditsTab()
        {
            Tab t = NewTab("CRÉDITOS", null);

            RectTransform rt = NoirUIKit.NewRect("Equipe", t.root);
            NoirUIKit.Center(rt, new Vector2(0f, 20f), new Vector2(1100f, 240f));
            CanvasGroup team = rt.gameObject.AddComponent<CanvasGroup>();
            t.extras.Add(team);

            TMP_Text names = NoirUIKit.NewText("Nomes", team.transform,
                "Guilherme Dominoni Gonçalves\nGustavo Serafim Manzoni", 46f, NoirTheme.Paper, 3f);
            NoirUIKit.Stretch(names.rectTransform);
            names.alignment = TextAlignmentOptions.Center;
            names.lineSpacing = 40f;
        }

        // ------------------------------------------------------------ tipos de opcao

        private Option AddRow(Tab tab, NoirOptionKind kind, string label, string desc, bool danger = false)
        {
            float step = kind == NoirOptionKind.Info ? InfoStep : RowStep;
            if (kind == NoirOptionKind.Button)
                tab.nextY -= 14f;

            NoirOptionRow row = new(tab.root, kind, label, desc, tab.nextY, step - 6f, danger);
            tab.nextY -= step;

            Option option = new() { row = row };
            tab.options.Add(option);
            int index = tab.options.Count - 1;

            row.Hovered += () => Select(tab, index, false);
            row.Clicked += () =>
            {
                Select(tab, index, false);
                Activate();
            };
            row.ArrowClicked += dir =>
            {
                Select(tab, index, false);
                option.adjust?.Invoke(dir);
            };
            return option;
        }

        private void AddToggle(Tab tab, string label, string desc, Func<bool> get, Action<bool> set, Action reset)
        {
            Option o = AddRow(tab, NoirOptionKind.Toggle, label, desc);
            o.refresh = animate => o.row.SetValue(Loc.T(get() ? "LIGADO" : "DESLIGADO"), animate ? 1 : 0);
            o.adjust = dir =>
            {
                set(!get());
                o.row.SetValue(Loc.T(get() ? "LIGADO" : "DESLIGADO"), dir);
                NoirAudio.Play(NoirSfx.Tick, 0.9f, get() ? 1.25f : 0.85f);
            };
            o.activate = () => o.adjust(1);
            o.reset = reset;
        }

        private void AddChoice(Tab tab, string label, string desc, Func<string[]> labels, Func<int> get, Action<int> set,
            Action reset, bool wrap)
        {
            Option o = AddRow(tab, NoirOptionKind.Choice, label, desc);
            o.refresh = animate => o.row.SetValue(Loc.T(SafeLabel(labels(), get())), animate ? 1 : 0);
            o.adjust = dir =>
            {
                string[] all = labels();
                int count = all.Length;
                int current = get();
                int next = wrap ? (current + dir + count) % count : Mathf.Clamp(current + dir, 0, count - 1);
                if (next == current)
                {
                    o.row.Nudge(dir);
                    NoirAudio.Play(NoirSfx.Locked, 0.35f, 1.3f);
                    return;
                }

                set(next);
                o.row.SetValue(Loc.T(SafeLabel(all, next)), dir);
                NoirAudio.Play(NoirSfx.Tick, 0.9f, 0.85f + 0.5f * next / Mathf.Max(1, count - 1));
            };
            o.activate = () =>
            {
                string[] all = labels();
                int next = (get() + 1) % all.Length;
                set(next);
                o.row.SetValue(Loc.T(SafeLabel(all, next)), 1);
                NoirAudio.Play(NoirSfx.Tick, 0.9f, 1.1f);
            };
            o.reset = reset;
        }

        private void AddDropdown(Tab tab, string label, string desc, Func<string[]> labels, Func<int> get, Action<int> set, Action reset)
        {
            Option o = AddRow(tab, NoirOptionKind.Dropdown, label, desc);
            o.popupLabels = labels;
            o.popupGet = get;
            o.popupSet = set;
            o.refresh = animate => o.row.SetValue(SafeLabel(labels(), get()), animate ? 1 : 0);
            o.activate = () => OpenPopup(o);
            o.adjust = _ => OpenPopup(o);
            o.reset = reset;
        }

        private void AddSlider(Tab tab, string label, string desc, float min, float max, float step, Func<float> get,
            Action<float> set, Func<float, string> format, Action reset)
        {
            Option o = AddRow(tab, NoirOptionKind.Slider, label, desc);

            void Apply(float v, bool animate)
            {
                v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
                set(v);
                o.row.SetSlider(Mathf.InverseLerp(min, max, v), format(v), animate);

                // Um tique por degrau, com o tom subindo junto com o valor.
                int tick = Mathf.RoundToInt(v / step);
                if (tick != lastSliderTick)
                {
                    lastSliderTick = tick;
                    NoirAudio.Play(NoirSfx.Tick, 0.75f, 0.75f + Mathf.InverseLerp(min, max, v) * 0.7f);
                }
            }

            o.refresh = animate =>
            {
                float v = get();
                o.row.SetSlider(Mathf.InverseLerp(min, max, v), format(v), animate);
            };
            o.adjust = dir =>
            {
                float before = get();
                Apply(before + dir * step, true);
                if (Mathf.Approximately(before, get()))
                    o.row.Nudge(dir);
            };
            o.row.SliderDragged += t => Apply(Mathf.Lerp(min, max, t), false);
            o.reset = reset;
        }

        private void AddVolume(Tab tab, string label, string desc, VolumeChannel channel)
        {
            AddSlider(tab, label, desc, 0f, 1f, 0.05f,
                () => GameSettings.GetVolume(channel),
                v => GameSettings.SetVolume(channel, v),
                Percent,
                () => GameSettings.SetVolume(channel, 1f));
        }

        private void AddResetButton(Tab tab)
        {
            Option o = AddRow(tab, NoirOptionKind.Button, "RESTAURAR PADRÃO", "Devolve as opções desta aba ao que eram no começo.", true);
            o.activate = () =>
            {
                foreach (Option other in tab.options)
                    other.reset?.Invoke();
                foreach (Option other in tab.options)
                    other.refresh?.Invoke(true);

                o.row.Flash();
                NoirAudio.Play(NoirSfx.Back, 0.9f, 0.8f);
                if (NoirAtmosphere.Exists)
                    NoirAtmosphere.Get().Pulse(0.2f);
            };
        }

        private void AddInfo(Tab tab, string action, string keys)
        {
            Option o = AddRow(tab, NoirOptionKind.Info, action, null);
            o.row.SetValue(keys);
        }

        // ------------------------------------------------------------ valores

        private static string SafeLabel(string[] labels, int i)
        {
            return labels == null || labels.Length == 0 ? string.Empty : labels[Mathf.Clamp(i, 0, labels.Length - 1)];
        }

        private static string Percent(float v)
        {
            return Mathf.RoundToInt(v * 100f) + "%";
        }

        private static string Decimal(float v)
        {
            string s = v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            return Loc.Current == Language.EN ? s : s.Replace('.', ',');
        }

        private static int CurrentScreenModeIndex()
        {
            if (PlayerPrefs.HasKey(GameSettings.ScreenModeKey))
                return Mathf.Clamp(PlayerPrefs.GetInt(GameSettings.ScreenModeKey), 0, 3);

            return Screen.fullScreenMode switch
            {
                FullScreenMode.ExclusiveFullScreen => 1,
                FullScreenMode.MaximizedWindow => 2,
                FullScreenMode.Windowed => 3,
                _ => 0
            };
        }

        private static string[] QualityLabels()
        {
            string[] names = QualitySettings.names;
            string[] labels = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                labels[i] = names[i] switch
                {
                    "Mobile" => "BAIXA",
                    "PC" => "ALTA",
                    _ => names[i].ToUpperInvariant()
                };
            }

            return labels;
        }

        private static string[] FpsLabels()
        {
            string[] labels = new string[GameSettings.FpsLimits.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = GameSettings.FpsLimits[i] > 0 ? GameSettings.FpsLimits[i].ToString() : "SEM LIMITE";
            return labels;
        }

        private void CollectResolutions()
        {
            resolutions.Clear();
            foreach (Resolution r in Screen.resolutions)
            {
                Vector2Int size = new(r.width, r.height);
                if (!resolutions.Contains(size))
                    resolutions.Add(size);
            }

            Vector2Int current = new(Screen.width, Screen.height);
            if (!resolutions.Contains(current))
                resolutions.Add(current);

            resolutions.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            resolutionIndex = resolutions.IndexOf(current);
        }

        private string[] ResolutionLabels()
        {
            string[] labels = new string[resolutions.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = resolutions[i].x + " × " + resolutions[i].y;
            return labels;
        }

        // ------------------------------------------------------------ mostrar, abas e selecao

        /// <summary>Chamado quando a pagina de opcoes aparece.</summary>
        public void Show(int tab = -1, int option = -1)
        {
            ClosePopup(true);
            foreach (Tab t in tabs)
                RefreshTab(t);

            ShowTab(tab >= 0 ? Mathf.Clamp(tab, 0, tabs.Count - 1) : tabIndex, 0, option);
        }

        private void RefreshTab(Tab t)
        {
            foreach (Option o in t.options)
                o.refresh?.Invoke(false);
        }

        public void ChangeTab(int dir)
        {
            if (PopupOpen)
                ClosePopup(false);

            int next = (tabIndex + dir + tabs.Count) % tabs.Count;
            NoirAudio.Play(NoirSfx.TabSwitch, 0.7f);

            RectTransform arrow = dir < 0 ? headerLeft : headerRight;
            arrow.DOKill(true);
            arrow.DOPunchAnchorPos(new Vector2(dir * 16f, 0f), 0.3f, 6, 0.4f).SetUpdate(true);

            ShowTab(next, dir, -1);
        }

        private void ShowTab(int index, int dir, int option)
        {
            Tab old = tabs[tabIndex];
            Tab tab = tabs[index];

            if (old != tab)
            {
                foreach (Option o in old.options)
                    o.row.SetSelected(false, true);
                old.group.DOKill();
                old.root.DOKill();
                old.group.interactable = false;
                old.group.blocksRaycasts = false;
                old.root.DOAnchorPos(new Vector2(-dir * 90f, 0f), 0.2f).SetEase(Ease.InCubic).SetUpdate(true);
                old.group.DOFade(0f, 0.16f).SetUpdate(true).OnComplete(() => old.root.gameObject.SetActive(false));
            }

            tabIndex = index;
            tab.root.gameObject.SetActive(true);
            tab.group.DOKill();
            tab.root.DOKill();
            tab.root.anchoredPosition = Vector2.zero;
            tab.group.alpha = 1f;
            tab.group.interactable = true;
            tab.group.blocksRaycasts = true;

            for (int i = 0; i < tab.options.Count; i++)
            {
                tab.options[i].row.SetSelected(false, true);
                tab.options[i].row.PlayIntro(0.035f * i, dir == 0 ? 0 : dir);
            }

            // Blocos livres (creditos): entram em sequencia, deslizando do lado da troca.
            for (int i = 0; i < tab.extras.Count; i++)
            {
                CanvasGroup g = tab.extras[i];
                RectTransform rt = (RectTransform)g.transform;
                g.DOKill();
                rt.DOKill(true);
                Vector2 home = rt.anchoredPosition;
                g.alpha = 0f;
                rt.anchoredPosition = home + new Vector2(dir * 70f, -12f);
                g.DOFade(1f, 0.4f).SetDelay(0.08f + 0.12f * i).SetUpdate(true);
                rt.DOAnchorPos(home, 0.55f).SetDelay(0.08f + 0.12f * i).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            FlickerHeader(Loc.T(tab.title));
            UpdatePager(index);

            tab.index = -1;
            int first = option >= 0 && option < tab.options.Count && tab.options[option].row.Selectable
                ? option
                : FirstSelectable(tab);
            if (first >= 0)
                Select(tab, first, true);
            else
                SetDescription(tab.idleDescription);
        }

        private void FlickerHeader(string text)
        {
            header.text = text;
            header.DOKill();
            header.rectTransform.DOKill(true);
            header.characterSpacing = 42f;
            DOTween.To(() => header.characterSpacing, v => header.characterSpacing = v, 22f, 0.5f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(header);

            // Acende como lampada ruim: falha duas vezes antes de firmar.
            header.alpha = 0f;
            Sequence s = DOTween.Sequence().SetUpdate(true).SetTarget(header);
            s.Append(DOTween.To(() => header.alpha, v => header.alpha = v, 1f, 0.05f));
            s.Append(DOTween.To(() => header.alpha, v => header.alpha = v, 0.2f, 0.05f));
            s.Append(DOTween.To(() => header.alpha, v => header.alpha = v, 0.9f, 0.06f));
            s.AppendInterval(0.05f);
            s.Append(DOTween.To(() => header.alpha, v => header.alpha = v, 0.35f, 0.04f));
            s.Append(DOTween.To(() => header.alpha, v => header.alpha = v, 1f, 0.12f));
        }

        private void UpdatePager(int index)
        {
            for (int i = 0; i < pagerDots.Count; i++)
            {
                Image dot = pagerDots[i];
                bool on = i == index;
                dot.DOKill();
                dot.rectTransform.DOKill();
                dot.DOColor(on ? NoirTheme.Amber : NoirTheme.WithAlpha(NoirTheme.Ash, 0.35f), 0.2f).SetUpdate(true);
                dot.rectTransform.DOScale(on ? 1.45f : 1f, 0.3f).SetEase(on ? Ease.OutBack : Ease.OutCubic).SetUpdate(true);
            }
        }

        private static int FirstSelectable(Tab tab)
        {
            for (int i = 0; i < tab.options.Count; i++)
                if (tab.options[i].row.Selectable)
                    return i;
            return -1;
        }

        private void Select(Tab tab, int index, bool silent)
        {
            if (tab != tabs[tabIndex] || PopupOpen || index == tab.index || !tab.options[index].row.Selectable)
                return;

            if (tab.index >= 0)
                tab.options[tab.index].row.SetSelected(false);

            tab.index = index;
            tab.options[index].row.SetSelected(true);
            SetDescription(tab.options[index].row.Description);
            lastSliderTick = -1;
            if (!silent)
                NoirAudio.Play(NoirSfx.Hover, 0.5f, 1f);
        }

        private void SetDescription(string pt)
        {
            description.DOKill();
            description.text = string.IsNullOrEmpty(pt) ? string.Empty : Loc.T(pt);
            description.maxVisibleCharacters = 0;
            int length = description.text.Length;
            // Datilografado rapido, como uma nota sendo batida a maquina.
            DOTween.To(() => description.maxVisibleCharacters, v => description.maxVisibleCharacters = v, length,
                Mathf.Min(0.9f, length * 0.014f)).SetEase(Ease.Linear).SetUpdate(true).SetTarget(description);
        }

        // ------------------------------------------------------------ entrada

        public void HandleInput(Keyboard kb)
        {
            if (PopupOpen)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) MovePopup(1);
                else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) MovePopup(-1);
                else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                    ChoosePopup(popupCursor);
                else if (kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                    ClosePopup(false);
                return;
            }

            if (kb.qKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) ChangeTab(-1);
            else if (kb.eKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) ChangeTab(1);
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Adjust(-1);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Adjust(1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                Activate();
        }

        /// <summary>Esc: fecha o dropdown se estiver aberto. Devolve true se consumiu.</summary>
        public bool HandleEscape()
        {
            if (!PopupOpen)
                return false;

            ClosePopup(false);
            return true;
        }

        private void Move(int dir)
        {
            Tab tab = tabs[tabIndex];
            int count = tab.options.Count;
            if (count == 0 || FirstSelectable(tab) < 0)
                return;

            int i = tab.index < 0 ? (dir > 0 ? -1 : count) : tab.index;
            for (int n = 0; n < count; n++)
            {
                i = (i + dir + count) % count;
                if (tab.options[i].row.Selectable)
                {
                    Select(tab, i, false);
                    return;
                }
            }
        }

        private void Adjust(int dir)
        {
            Tab tab = tabs[tabIndex];
            if (tab.index >= 0)
                tab.options[tab.index].adjust?.Invoke(dir);
        }

        private void Activate()
        {
            Tab tab = tabs[tabIndex];
            if (tab.index >= 0)
                tab.options[tab.index].activate?.Invoke();
        }

        public void Tick()
        {
            float t = Time.unscaledTime;
            Tab tab = tabs[tabIndex];
            if (tab.index >= 0)
                tab.options[tab.index].row.Tick(t);

            // Setas do cabecalho respiram de leve, chamando para a troca de aba.
            float sway = Mathf.Sin(t * 2.2f) * 3f;
            if (!DOTween.IsTweening(headerLeft))
                headerLeft.anchoredPosition = new Vector2(-sway, 0f);
            if (!DOTween.IsTweening(headerRight))
                headerRight.anchoredPosition = new Vector2(sway, 0f);

            if (PopupOpen && popupBands.Count > 0)
            {
                int slot = popupCursor - popupTop;
                if (slot >= 0 && slot < popupBands.Count && !DOTween.IsTweening(popupBands[slot]))
                    popupBands[slot].color = NoirTheme.WithAlpha(NoirTheme.Paper, 0.07f + Mathf.PerlinNoise(t * 2.5f, 7f) * 0.05f);
            }
        }

        // ------------------------------------------------------------ dropdown

        private void BuildPopup()
        {
            popupRoot = NoirUIKit.NewRect("Dropdown", root);
            NoirUIKit.Stretch(popupRoot);

            // Clique fora fecha.
            Image blocker = NoirUIKit.NewImage("Bloqueio", popupRoot, null, new Color(0f, 0f, 0f, 0.35f));
            NoirUIKit.Stretch(blocker.rectTransform);
            blocker.rectTransform.offsetMin = new Vector2(-blockerMargin, -blockerMargin);
            blocker.rectTransform.offsetMax = new Vector2(blockerMargin, blockerMargin);
            blocker.raycastTarget = true;
            NoirPointerRelay blockRelay = blocker.gameObject.AddComponent<NoirPointerRelay>();
            blockRelay.Clicked += () => ClosePopup(false);

            popupPanel = NoirUIKit.NewRect("Painel", popupRoot);
            popupPanel.anchorMin = popupPanel.anchorMax = new Vector2(0.5f, 0.5f);
            popupPanel.pivot = new Vector2(0.5f, 1f);
            popupPanel.sizeDelta = new Vector2(440f, PopupVisible * PopupEntryHeight + 28f);

            Image back = NoirUIKit.NewImage("Fundo", popupPanel, null, new Color(0.035f, 0.035f, 0.045f, 0.97f));
            NoirUIKit.Stretch(back.rectTransform);
            back.raycastTarget = true;
            NoirPointerRelay scroll = back.gameObject.AddComponent<NoirPointerRelay>();
            scroll.Scrolled += delta => ScrollPopup(delta > 0f ? -1 : 1);

            Image frame = NoirUIKit.NewImage("Moldura", popupPanel, NoirTheme.GetSprite("noir_btn_normal", 14f), NoirTheme.WithAlpha(NoirTheme.Paper, 0.9f));
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1.4f;
            NoirUIKit.Stretch(frame.rectTransform);

            for (int i = 0; i < PopupVisible; i++)
            {
                int slot = i;
                float y = -14f - PopupEntryHeight * (i + 0.5f);

                Image hit = NoirUIKit.NewImage("Item" + i, popupPanel, null, new Color(0, 0, 0, 0));
                hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                hit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                hit.rectTransform.anchoredPosition = new Vector2(0f, y);
                hit.rectTransform.sizeDelta = new Vector2(420f, PopupEntryHeight);
                hit.raycastTarget = true;
                NoirPointerRelay relay = hit.gameObject.AddComponent<NoirPointerRelay>();
                relay.Entered += () => SetPopupCursor(popupTop + slot, true);
                relay.Clicked += () => ChoosePopup(popupTop + slot);
                relay.Scrolled += delta => ScrollPopup(delta > 0f ? -1 : 1);

                Image band = NoirUIKit.NewImage("Faixa", hit.rectTransform, NoirTheme.GetSprite("noir_glow"), NoirTheme.WithAlpha(NoirTheme.Paper, 0f));
                NoirUIKit.Center(band.rectTransform, Vector2.zero, new Vector2(520f, PopupEntryHeight * 1.7f));
                popupBands.Add(band);

                Image mark = NoirUIKit.NewImage("Atual", hit.rectTransform, null, NoirTheme.WithAlpha(NoirTheme.Amber, 0f));
                NoirUIKit.Center(mark.rectTransform, new Vector2(-180f, 0f), new Vector2(9f, 9f));
                mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                popupMarks.Add(mark);

                TMP_Text text = NoirUIKit.NewText("Texto", hit.rectTransform, string.Empty, 25f, NoirTheme.Ash, 3f);
                NoirUIKit.Stretch(text.rectTransform);
                popupTexts.Add(text);
            }

            popupUp = NoirUIKit.Chevron("Mais acima", popupPanel, 14f, 2.5f, NoirTheme.Ash);
            popupUp.anchorMin = popupUp.anchorMax = new Vector2(0.5f, 1f);
            popupUp.anchoredPosition = new Vector2(0f, -8f);
            popupUp.localRotation = Quaternion.Euler(0f, 0f, 90f);

            popupDown = NoirUIKit.Chevron("Mais abaixo", popupPanel, 14f, 2.5f, NoirTheme.Ash);
            popupDown.anchorMin = popupDown.anchorMax = new Vector2(0.5f, 0f);
            popupDown.anchoredPosition = new Vector2(0f, 8f);
            popupDown.localRotation = Quaternion.Euler(0f, 0f, -90f);

            popupRoot.gameObject.SetActive(false);
        }

        private void OpenPopup(Option option)
        {
            popupItems = option.popupLabels();
            if (popupItems == null || popupItems.Length == 0)
                return;

            popupOption = option;
            popupCursor = Mathf.Clamp(option.popupGet(), 0, popupItems.Length - 1);
            popupTop = Mathf.Clamp(popupCursor - PopupVisible / 2, 0, Mathf.Max(0, popupItems.Length - PopupVisible));

            int visible = Mathf.Min(PopupVisible, popupItems.Length);
            float height = visible * PopupEntryHeight + 28f;
            popupPanel.sizeDelta = new Vector2(440f, height);

            // Abre embaixo do valor; se nao couber, abre para cima.
            float rowY = option.row.Y;
            bool below = rowY - 34f - height > -420f;
            popupPanel.pivot = new Vector2(0.5f, below ? 1f : 0f);
            popupPanel.anchoredPosition = new Vector2(NoirOptionRow.Width * 0.5f - 250f, below ? rowY - 30f : rowY + 30f);

            for (int i = 0; i < PopupVisible; i++)
                popupTexts[i].transform.parent.gameObject.SetActive(i < visible);

            popupRoot.gameObject.SetActive(true);
            popupRoot.SetAsLastSibling();
            RedrawPopup();

            popupPanel.DOKill();
            popupPanel.localScale = new Vector3(1f, 0f, 1f);
            popupPanel.DOScaleY(1f, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
            option.row.Flash();
            NoirAudio.Play(NoirSfx.Paper, 0.6f, 1.05f);
        }

        private void ClosePopup(bool instant)
        {
            if (popupRoot == null)
                return;

            popupOption = null;
            popupPanel.DOKill();
            if (instant || !popupRoot.gameObject.activeSelf)
            {
                popupRoot.gameObject.SetActive(false);
                return;
            }

            NoirAudio.Play(NoirSfx.Back, 0.45f, 1.2f);
            popupPanel.DOScaleY(0f, 0.12f).SetEase(Ease.InCubic).SetUpdate(true)
                .OnComplete(() => popupRoot.gameObject.SetActive(false));
        }

        private void MovePopup(int dir)
        {
            SetPopupCursor(Mathf.Clamp(popupCursor + dir, 0, popupItems.Length - 1), false);
        }

        private void ScrollPopup(int dir)
        {
            if (!PopupOpen)
                return;

            int max = Mathf.Max(0, popupItems.Length - PopupVisible);
            int top = Mathf.Clamp(popupTop + dir, 0, max);
            if (top == popupTop)
                return;

            popupTop = top;
            popupCursor = Mathf.Clamp(popupCursor, popupTop, popupTop + PopupVisible - 1);
            RedrawPopup();
        }

        private void SetPopupCursor(int index, bool fromPointer)
        {
            if (!PopupOpen || index < 0 || index >= popupItems.Length || index == popupCursor)
                return;

            popupCursor = index;
            if (!fromPointer)
            {
                if (popupCursor < popupTop)
                    popupTop = popupCursor;
                else if (popupCursor >= popupTop + PopupVisible)
                    popupTop = popupCursor - PopupVisible + 1;
            }

            RedrawPopup();
            NoirAudio.Play(NoirSfx.Hover, 0.45f, 1.1f);
        }

        private void RedrawPopup()
        {
            int current = popupOption.popupGet();
            for (int i = 0; i < PopupVisible; i++)
            {
                int item = popupTop + i;
                if (item >= popupItems.Length)
                    continue;

                bool cursor = item == popupCursor;
                popupTexts[i].text = popupItems[item];
                popupTexts[i].color = cursor ? NoirTheme.Paper : NoirTheme.WithAlpha(NoirTheme.Ash, 0.75f);
                popupTexts[i].characterSpacing = cursor ? 5f : 3f;
                popupBands[i].DOKill();
                popupBands[i].color = NoirTheme.WithAlpha(NoirTheme.Paper, cursor ? 0.09f : 0f);
                popupMarks[i].color = NoirTheme.WithAlpha(NoirTheme.Amber, item == current ? 1f : 0f);
            }

            popupUp.gameObject.SetActive(popupTop > 0);
            popupDown.gameObject.SetActive(popupTop + PopupVisible < popupItems.Length);
        }

        private void ChoosePopup(int index)
        {
            if (!PopupOpen || index < 0 || index >= popupItems.Length)
                return;

            Option option = popupOption;
            int before = option.popupGet();
            ClosePopup(false);
            if (index == before)
                return;

            NoirAudio.Play(NoirSfx.Click, 0.8f);
            // Pode trocar o idioma: a pausa se reconstroi no proximo frame e esta tela some.
            option.popupSet(index);
            option.row.SetValue(SafeLabel(option.popupLabels(), option.popupGet()), index > before ? 1 : -1);
        }
    }
}
