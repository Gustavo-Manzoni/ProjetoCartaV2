using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ProjetoCarta.Core
{
    /// <summary>
    /// Traducao PT->EN em que o proprio texto em portugues e a chave (tabela em LocTable.cs). Sem entrada, devolve o PT:
    /// o jogo degrada para portugues, nunca para texto vazio.
    /// O idioma vem do PlayerPrefs "language" (mesma chave do LocalizationManager do menu) e acompanha
    /// LocalizationManager.OnChangeLanguage. Fora do Play Mode sempre devolve PT (salvo OverrideForTests), para que
    /// ferramentas de editor nunca gravem ingles na cena.
    /// A logica do jogo continua em PT (comparacoes de prompt, teste automatico): traduza so na hora de exibir e, em
    /// frases montadas, traduza o molde e as partes antes de montar (Format), nunca a frase ja montada.
    /// </summary>
    public static partial class Loc
    {
        private static Language? current;
        private static Language? testOverride;

        // Montados na primeira consulta a partir de LocTable.cs (sem depender da ordem de inicializacao entre arquivos).
        private static Dictionary<string, string> english;
        private static HashSet<string> keep;
        private static HashSet<string> englishValues;
        private static List<string[]> englishTemplates;
        private static IReadOnlyDictionary<string, string> englishView;

        private static readonly HashSet<string> reportedMissing = new(StringComparer.Ordinal);

        /// <summary>Disparado quando o idioma efetivo muda (hoje, so pelos botoes BR/EN do menu).</summary>
        public static event Action<Language> Changed;

        /// <summary>Idioma em uso. Sem override de teste, vem do PlayerPrefs ate a primeira troca.</summary>
        public static Language Current
        {
            get
            {
                if (testOverride.HasValue)
                    return testOverride.Value;
                current ??= ReadSaved();
                return current.Value;
            }
        }

        /// <summary>Tabela PT (normalizado) -> EN, so leitura. Para testes e ferramentas.</summary>
        public static IReadOnlyDictionary<string, string> EnglishTable
        {
            get
            {
                EnsureTables();
                return englishView ??= new ReadOnlyDictionary<string, string>(english);
            }
        }

        /// <summary>
        /// Traduz um texto PT para o idioma atual. Sem entrada na tabela, devolve o proprio PT.
        /// Fora do Play Mode devolve sempre o PT, salvo OverrideForTests.
        /// </summary>
        public static string T(string pt)
        {
            if (string.IsNullOrEmpty(pt))
                return pt;
            if (!testOverride.HasValue && !Application.isPlaying)
                return pt;
            if (Current != Language.EN)
                return pt;

            if (TryTranslate(pt, out string en))
                return en;

            ReportMissing(Normalize(pt));
            return pt;
        }

        /// <summary>
        /// Traduz o molde PT e formata com os argumentos (ja traduzidos por quem chama).
        /// Se o molde EN estiver quebrado, cai no molde PT; se o PT tambem estiver, devolve o molde cru.
        /// </summary>
        public static string Format(string ptTemplate, params object[] args)
        {
            if (string.IsNullOrEmpty(ptTemplate))
                return ptTemplate ?? string.Empty;

            args ??= Array.Empty<object>();
            string template = T(ptTemplate);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                if (Debug.isDebugBuild && template != ptTemplate)
                    Debug.LogWarning("[Loc] Molde EN invalido para \"" + ptTemplate + "\": \"" + template + "\"");
            }

            try
            {
                return string.Format(ptTemplate, args);
            }
            catch (FormatException)
            {
                return ptTemplate;
            }
        }

        /// <summary>Existe traducao EN para este PT? Independe do idioma atual e do Play Mode (tradutor de cena e testes).</summary>
        public static bool TryGetEnglish(string pt, out string en)
        {
            en = null;
            return !string.IsNullOrEmpty(pt) && TryTranslate(pt, out en);
        }

        /// <summary>Texto igual nos dois idiomas (nome do jogo, teclas, horas)? Nao gera aviso de traducao faltando.</summary>
        public static bool IsKeptAsIs(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            EnsureTables();
            return keep.Contains(Normalize(text));
        }

        /// <summary>
        /// Troca o idioma. Com o LocalizationManager vivo, passa por ele (atualiza os LocalizedText do menu e salva).
        /// Sem ele (cena 1 aberta direto), salva no PlayerPrefs. Changed so dispara se o idioma efetivo mudar.
        /// </summary>
        public static void SetLanguage(Language language)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager != null)
            {
                // OnChangeLanguage -> OnManagerChanged -> Apply.
                if (language == Language.EN)
                    manager.ChangeLanguageToEn();
                else
                    manager.ChangeLanguageToPt();
            }

            // Idempotente: se o evento do LocalizationManager ja aplicou, nao dispara Changed de novo.
            Apply(language);

            if (manager == null)
                PlayerPrefs.SetInt(GameSettings.LanguageKey, (int)language);
        }

        /// <summary>So para testes (EditMode e PlaythroughSmokeTest). null remove o override.</summary>
        public static void OverrideForTests(Language? language)
        {
            testOverride = language;
        }

        private static void OnManagerChanged(Language language)
        {
            Apply(language);
        }

        private static void Apply(Language language)
        {
            // Le o idioma antes de gravar: o LocalizationManager so salva no PlayerPrefs depois de disparar o evento.
            Language before = Current;
            current = language;
            if (Current != before)
                Changed?.Invoke(language);
        }

        private static Language ReadSaved()
        {
            return PlayerPrefs.GetInt(GameSettings.LanguageKey, (int)Language.PT) == (int)Language.EN
                ? Language.EN
                : Language.PT;
        }

        private static bool TryTranslate(string pt, out string en)
        {
            EnsureTables();
            if (english.TryGetValue(pt, out en))
                return true;

            string key = Normalize(pt);
            if (key.Length == 0 || key == pt || !english.TryGetValue(key, out en))
            {
                en = null;
                return false;
            }

            // Preserva espacos e quebras nas pontas do texto original (ex.: "  PISCAR\n").
            int start = pt.Length - pt.TrimStart().Length;
            int end = pt.Length - pt.TrimEnd().Length;
            if (start > 0 || end > 0)
                en = pt.Substring(0, start) + en + pt.Substring(pt.Length - end);
            return true;
        }

        /// <summary>Forma das chaves: quebras de linha \n (nunca \r\n) e sem espacos nas pontas.</summary>
        private static string Normalize(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static void EnsureTables()
        {
            if (english != null)
                return;

            int count = EnglishPairs.GetLength(0);
            Dictionary<string, string> table = new(count, StringComparer.Ordinal);
            HashSet<string> values = new(StringComparer.Ordinal);
            List<string[]> templates = new();
            for (int i = 0; i < count; i++)
            {
                string pt = EnglishPairs[i, 0];
                string en = EnglishPairs[i, 1];
                if (string.IsNullOrEmpty(pt) || string.IsNullOrEmpty(en))
                    continue;

                string key = Normalize(pt);
                en = en.Replace("\r\n", "\n");
                if (Debug.isDebugBuild && table.TryGetValue(key, out string previous) && previous != en)
                    Debug.LogWarning("[Loc] Chave repetida com traducoes diferentes: \"" + key + "\"");

                table[key] = en;
                values.Add(Normalize(en));
                string[] parts = SplitTemplate(en);
                if (parts != null)
                    templates.Add(parts);
            }

            HashSet<string> kept = new(StringComparer.Ordinal);
            foreach (string s in KeepAsIs)
            {
                if (!string.IsNullOrEmpty(s))
                    kept.Add(Normalize(s));
            }

            keep = kept;
            englishValues = values;
            englishTemplates = templates;
            english = table;
        }

        private static void ReportMissing(string key)
        {
            if (!Debug.isDebugBuild || key.Length == 0 || !reportedMissing.Add(key))
                return;

            // Ja em ingles (frase montada com um molde EN, ou um valor EN traduzido de novo) ou sem palavras: nada a avisar.
            if (!HasLetters(key) || keep.Contains(key) || englishValues.Contains(key) || MatchesEnglishTemplate(key))
                return;

            Debug.LogWarning("[Loc] Sem traducao EN: \"" + key + "\" (adicione em Core/LocTable.cs)");
        }

        private static bool HasLetters(string s)
        {
            foreach (char c in s)
            {
                if (char.IsLetter(c))
                    return true;
            }

            return false;
        }

        /// <summary>Partes literais de um molde com {0}, {1}...; null se nao houver placeholder.</summary>
        private static string[] SplitTemplate(string template)
        {
            List<string> parts = null;
            int literalStart = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] != '{')
                    continue;

                int close = template.IndexOf('}', i + 1);
                if (close <= i + 1 || !IsDigits(template, i + 1, close))
                    continue;

                parts ??= new List<string>();
                parts.Add(template.Substring(literalStart, i - literalStart));
                literalStart = close + 1;
                i = close;
            }

            if (parts == null)
                return null;

            parts.Add(template.Substring(literalStart));
            return parts.ToArray();
        }

        private static bool IsDigits(string s, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                    return false;
            }

            return true;
        }

        private static bool MatchesEnglishTemplate(string text)
        {
            foreach (string[] parts in englishTemplates)
            {
                if (MatchesTemplate(text, parts))
                    return true;
            }

            return false;
        }

        private static bool MatchesTemplate(string text, string[] parts)
        {
            string first = parts[0];
            string last = parts[parts.Length - 1];
            if (first.Length + last.Length == 0 || !text.StartsWith(first, StringComparison.Ordinal))
                return false;

            int position = first.Length;
            for (int i = 1; i < parts.Length - 1; i++)
            {
                if (parts[i].Length == 0)
                    continue;

                int found = text.IndexOf(parts[i], position, StringComparison.Ordinal);
                if (found < 0)
                    return false;
                position = found + parts[i].Length;
            }

            return text.Length - position >= last.Length && text.EndsWith(last, StringComparison.Ordinal);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            testOverride = null;
            Changed = null;
            reportedMissing.Clear();
            LocalizationManager.OnChangeLanguage -= OnManagerChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            LocalizationManager.OnChangeLanguage -= OnManagerChanged;
            LocalizationManager.OnChangeLanguage += OnManagerChanged;
        }
    }
}
