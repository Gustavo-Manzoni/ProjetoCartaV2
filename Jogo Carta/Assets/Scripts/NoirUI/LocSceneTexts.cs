using System;
using System.Collections.Generic;
using ProjetoCarta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Traduz os textos que ja estao na cena (menu, creditos, tecla APAGAR do keypad, opcoes de dropdown) pela chave PT
    /// do Loc e desfaz ao voltar para PT. O NoirUIBootstrap chama Capture uma vez por cena, antes de criar HUD e pausa;
    /// UIs criadas por codigo depois disso traduzem a si mesmas com Loc.T.
    /// So troca um texto se ele ainda estiver como foi deixado: nao briga com codigo que mudou o texto depois.
    /// TMP com LocalizedText ficam com o LocalizationManager (os 5 botoes do menu).
    /// </summary>
    public static class LocSceneTexts
    {
        private sealed class TextEntry
        {
            public TMP_Text text;
            public string pt;
            public string en;
        }

        private sealed class OptionEntry
        {
            public TMP_Dropdown dropdown;
            public TMP_Dropdown.OptionData option;
            public string original;
            public string pt;
            public string en;
        }

        // O dropdown "Screen Mode" do OptionsCanvas guarda os nomes do enum FullScreenMode; o jogador ve estes rotulos.
        // A ordem das opcoes (e o indice salvo em "screenMode") nao muda: ver SettingsManager.SetScreeMode.
        private static readonly Dictionary<string, string> TechnicalOptionLabels = new(StringComparer.Ordinal)
        {
            { "FullScreenWindow", "Tela cheia" },
            { "ExclusiveFullScreen", "Tela cheia exclusiva" },
            { "MaximizedWindow", "Janela maximizada" },
            { "Windowed", "Janela" },
        };

        private static readonly List<TextEntry> texts = new();
        private static readonly HashSet<TMP_Text> capturedTexts = new();
        private static readonly List<OptionEntry> options = new();
        private static readonly HashSet<TMP_Dropdown> capturedDropdowns = new();
        private static readonly HashSet<Button> reboundButtons = new();

        /// <summary>
        /// Registra os textos traduziveis da cena (e de objetos DontDestroyOnLoad ainda nao vistos), re-liga os botoes
        /// BR/EN e aplica o idioma atual. Chamado pelo NoirUIBootstrap depois dos Awake/Start da cena.
        /// </summary>
        public static void Capture(Scene scene)
        {
            try
            {
                // Outra cena ja assumiu (troca rapida): a captura dela cuida dos textos.
                if (scene.IsValid() && SceneManager.GetActiveScene() != scene)
                    return;

                Prune();
                CaptureTexts();
                CaptureDropdownOptions();
                RebindLanguageButtons();
                Apply(Loc.Current);
            }
            catch (Exception e)
            {
                // Traducao nunca pode derrubar a montagem da cena (HUD, pausa).
                Debug.LogException(e);
            }
        }

        /// <summary>Aplica o idioma nos textos capturados. Assinado em Loc.Changed pelo NoirUIBootstrap.</summary>
        public static void Apply(Language language)
        {
            try
            {
                bool english = language == Language.EN;
                for (int i = texts.Count - 1; i >= 0; i--)
                {
                    TextEntry e = texts[i];
                    if (e.text == null)
                    {
                        texts.RemoveAt(i);
                        continue;
                    }

                    string from = english ? e.pt : e.en;
                    string to = english ? e.en : e.pt;
                    if (e.text.text == from)
                        e.text.text = to;
                }

                HashSet<TMP_Dropdown> changed = null;
                for (int i = options.Count - 1; i >= 0; i--)
                {
                    OptionEntry o = options[i];
                    if (o.dropdown == null)
                    {
                        options.RemoveAt(i);
                        continue;
                    }

                    string current = o.option.text;
                    if (current != o.original && current != o.pt && current != o.en)
                        continue; // codigo trocou a opcao: nao mexe

                    string to = english ? o.en : o.pt;
                    if (current == to)
                        continue;

                    o.option.text = to;
                    changed ??= new HashSet<TMP_Dropdown>();
                    changed.Add(o.dropdown);
                }

                if (changed != null)
                {
                    foreach (TMP_Dropdown d in changed)
                        d.RefreshShownValue();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void CaptureTexts()
        {
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || capturedTexts.Contains(t))
                    continue;
                // Os 5 botoes do menu continuam com o LocalizationManager.
                if (t.GetComponent<LocalizedText>() != null)
                    continue;
                // Legenda e itens do dropdown sao escritos pelo proprio dropdown a partir das opcoes.
                if (t.GetComponentInParent<TMP_Dropdown>(true) != null)
                    continue;

                string pt = t.text;
                if (!Loc.TryGetEnglish(pt, out string en) || en == pt)
                    continue;

                capturedTexts.Add(t);
                texts.Add(new TextEntry { text = t, pt = pt, en = en });
            }
        }

        private static void CaptureDropdownOptions()
        {
            foreach (TMP_Dropdown d in Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (d == null || !capturedDropdowns.Add(d) || d.options == null)
                    continue;

                foreach (TMP_Dropdown.OptionData option in d.options)
                {
                    if (option == null || string.IsNullOrEmpty(option.text))
                        continue;

                    string original = option.text;
                    string pt = TechnicalOptionLabels.TryGetValue(original, out string label) ? label : original;
                    string en = Loc.TryGetEnglish(pt, out string found) ? found : pt;
                    if (pt == original && en == pt)
                        continue;

                    options.Add(new OptionEntry { dropdown = d, option = option, original = original, pt = pt, en = en });
                }
            }
        }

        /// <summary>
        /// Os botoes BR/EN do OptionsCanvas chamam o LocalizationManager da propria cena. Ao voltar ao menu esse objeto
        /// e o duplicado destruido no Awake, e a chamada persistente passa a ser ignorada. Nesse caso, um listener de
        /// runtime troca o idioma pelo Loc, que usa o LocalizationManager vivo.
        /// </summary>
        private static void RebindLanguageButtons()
        {
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b == null || reboundButtons.Contains(b))
                    continue;

                // Identifica pelo botao e pelo painel: o BR do painel de idioma ativo nao tem chamada persistente
                // no OptionsCanvas (so as copias dos paineles de Graphics e Sound tem), entao contar pela chamada nao basta.
                Language? language = LanguageOf(b);
                if (language == null)
                    continue;

                // Chamada persistente com alvo vivo (o LocalizationManager em uso) ja funciona.
                if (HasLiveManagerCall(b))
                    continue;

                Language chosen = language.Value;
                b.onClick.AddListener(() => Loc.SetLanguage(chosen));
                reboundButtons.Add(b);
            }
        }

        /// <summary>Botoes BR/EN do painel de idioma (Sliders/Language/BR, Sliders/Language/EN); null para os outros.</summary>
        private static Language? LanguageOf(Button b)
        {
            if (b.transform.parent == null || b.transform.parent.name != "Language")
                return null;
            if (b.name == "BR")
                return Language.PT;
            if (b.name == "EN")
                return Language.EN;
            return null;
        }

        private static bool HasLiveManagerCall(Button b)
        {
            int count = b.onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                Object target = b.onClick.GetPersistentTarget(i);
                if (target != null && target == LocalizationManager.Instance)
                    return true;
            }

            return false;
        }

        private static void Prune()
        {
            texts.RemoveAll(e => e.text == null);
            options.RemoveAll(o => o.dropdown == null);
            capturedTexts.RemoveWhere(t => t == null);
            capturedDropdowns.RemoveWhere(d => d == null);
            reboundButtons.RemoveWhere(b => b == null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            texts.Clear();
            capturedTexts.Clear();
            options.Clear();
            capturedDropdowns.Clear();
            reboundButtons.Clear();
        }
    }
}
