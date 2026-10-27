using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.Puzzles;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjetoCarta.Tests
{
    /// <summary>
    /// Consistencia da tabela PT->EN (Core/LocTable.cs) e comportamento do Loc no Editor. Logica pura, sem cena.
    /// </summary>
    public sealed class LocalizationTests
    {
        /// <summary>Moldes e textos que o codigo monta com Loc.Format/Loc.T: precisam existir na tabela.</summary>
        private static readonly string[] RequiredKeys =
        {
            "Pegar {0}", "Colocar {0}", "Retirar {0}", "Colocar {0} no balde", "Painel: {0} de {1}", "Ler {0}",
            "Trocar os documentos {0} e {1} de lugar",
            "LIGADO", "DESLIGADO", "RESTAURAR PADRÃO", "JOGO", "VÍDEO", "CONTROLES", "TECLADO"
        };

        // {0}, {1}, {0:F0}, {0,5}... (o que o string.Format entende como argumento).
        private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^{}]*)?\}");

        [TearDown]
        public void TearDown()
        {
            Loc.OverrideForTests(null);
        }

        // ------------------------------------------------------------ tabela

        [Test]
        public void TabelaNaoEstaVazia()
        {
            Assert.Greater(Loc.EnglishTable.Count, 0, "Loc.EnglishTable vazia: confira Core/LocTable.cs");
        }

        [Test]
        public void PlaceholdersIguaisEmPtEEn()
        {
            List<string> wrong = new();
            foreach (KeyValuePair<string, string> pair in Loc.EnglishTable)
            {
                string pt = PlaceholderList(pair.Key);
                string en = PlaceholderList(pair.Value);
                if (pt != en)
                {
                    wrong.Add(Show(pair.Key) + " -> " + Show(pair.Value) + "  (PT {" + pt + "}, EN {" + en + "})");
                    continue;
                }

                if (pt.Length > 0 && (!Formats(pair.Key) || !Formats(pair.Value)))
                    wrong.Add(Show(pair.Key) + " -> " + Show(pair.Value) + "  (molde invalido para string.Format)");
            }

            Assert.IsEmpty(wrong, wrong.Count + " pares com placeholders diferentes:\n" + string.Join("\n", wrong));
        }

        [Test]
        public void TraducaoNaoEhChaveDeOutraTraducao()
        {
            // O interactor aplica Loc.T em prompts ja montados (e ja traduzidos): T(T(x)) precisa continuar igual a T(x).
            List<string> chains = new();
            foreach (KeyValuePair<string, string> pair in Loc.EnglishTable)
            {
                if (Loc.TryGetEnglish(pair.Value, out string again) && again != pair.Value)
                    chains.Add(Show(pair.Key) + " -> " + Show(pair.Value) + " -> " + Show(again));
            }

            Assert.IsEmpty(chains, chains.Count + " traducoes que viram outra frase se traduzidas de novo:\n" + string.Join("\n", chains));
        }

        [Test]
        public void TextoMantidoNaoTemTraducaoDiferente()
        {
            List<string> conflicts = Loc.EnglishTable
                .Where(pair => Loc.IsKeptAsIs(pair.Key) && pair.Value != pair.Key)
                .Select(pair => Show(pair.Key) + " -> " + Show(pair.Value))
                .ToList();

            Assert.IsEmpty(conflicts, "Textos em KeepAsIs e tambem traduzidos:\n" + string.Join("\n", conflicts));
        }

        [Test]
        public void MoldesUsadosPeloCodigoExistem()
        {
            List<string> missing = RequiredKeys
                .Where(key => !Loc.TryGetEnglish(key, out string en) || string.IsNullOrWhiteSpace(en) || en == key)
                .ToList();

            Assert.IsEmpty(missing, "Sem traducao EN: " + string.Join(", ", missing.Select(Show)));
        }

        [Test]
        public void ConsultaNormalizaQuebrasDeLinhaEEspacos()
        {
            // Texto de TMP salvo no Windows pode vir com \r\n; espacos nas pontas nao fazem parte da chave.
            string key = Loc.EnglishTable.Keys.FirstOrDefault(candidate => candidate.Contains("\n"));
            Assume.That(key, Is.Not.Null, "Nenhuma chave com quebra de linha na tabela");

            string fromScene = "  " + key.Replace("\n", "\r\n") + "\r\n";
            Assert.IsTrue(Loc.TryGetEnglish(fromScene, out string en), "Nao achou " + Show(fromScene));
            StringAssert.Contains(Loc.EnglishTable[key], en);

            Assert.IsTrue(Loc.IsKeptAsIs("  ENTER "));
            Assert.IsFalse(Loc.IsKeptAsIs(null));
            Assert.IsFalse(Loc.TryGetEnglish(string.Empty, out _));
        }

        // ------------------------------------------------------------ comportamento

        [Test]
        public void ForaDoPlayModeSemOverrideDevolvePt()
        {
            // Ferramentas de editor (PiscadaFatalSceneSetupEditor, MenuArtTools) nunca podem gravar ingles na cena.
            Assume.That(Application.isPlaying, Is.False);
            Loc.OverrideForTests(null);

            Assert.AreEqual("Interagir", Loc.T("Interagir"));
            Assert.AreEqual("Colocar x", Loc.Format("Colocar {0}", "x"));
        }

        [Test]
        public void NomeDoJogoSegueOIdioma()
        {
            // O titulo do menu, da pausa e do final usa a mesma chave; sem traducao cairia no PT.
            Assert.IsTrue(Loc.TryGetEnglish("PISCADA FATAL", out string en));
            Assert.AreEqual("FATAL BLINK", en);
            Assert.IsFalse(Loc.IsKeptAsIs("PISCADA FATAL"));
        }

        [Test]
        public void EmInglesFormatTraduzOMolde()
        {
            Loc.OverrideForTests(Language.EN);
            Assert.AreEqual(Language.EN, Loc.Current);

            Assert.IsTrue(Loc.TryGetEnglish("Colocar {0}", out string template));
            string expected = string.Format(template, "x");
            Assert.AreEqual(expected, Loc.Format("Colocar {0}", "x"));
            Assert.AreNotEqual("Colocar x", expected);

            Assert.IsTrue(Loc.TryGetEnglish("Painel: {0} de {1}", out string panel));
            Assert.AreEqual(string.Format(panel, 3, 5), Loc.Format("Painel: {0} de {1}", 3, 5));
        }

        [Test]
        public void EmInglesTextoConhecidoEhTraduzido()
        {
            Loc.OverrideForTests(Language.EN);

            Assert.IsTrue(Loc.TryGetEnglish("Interagir", out string en));
            Assert.AreEqual(en, Loc.T("Interagir"));
            Assert.AreNotEqual("Interagir", en);
        }

        [Test]
        public void EmInglesTextoDesconhecidoVoltaIgual()
        {
            Loc.OverrideForTests(Language.EN);

            const string unknown = "Frase de teste que nao esta na tabela 4821";
            Assert.AreEqual(unknown, Loc.T(unknown));
            Assert.AreEqual("Molde de teste 4821: 7", Loc.Format("Molde de teste 4821: {0}", 7));
            Assert.AreEqual(string.Empty, Loc.T(string.Empty));
            Assert.IsNull(Loc.T(null));
        }

        [Test]
        public void EmPortuguesNadaMuda()
        {
            Loc.OverrideForTests(Language.PT);

            Assert.AreEqual(Language.PT, Loc.Current);
            Assert.AreEqual("Interagir", Loc.T("Interagir"));
            Assert.AreEqual("Painel: 3 de 5", Loc.Format("Painel: {0} de {1}", 3, 5));
        }

        // ------------------------------------------------------------ apoio

        private static string PlaceholderList(string text)
        {
            return string.Join(",", Placeholder.Matches(text)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .OrderBy(index => index, StringComparer.Ordinal));
        }

        private static bool Formats(string template)
        {
            int count = Placeholder.Matches(template)
                .Cast<Match>()
                .Select(match => int.Parse(match.Groups[1].Value) + 1)
                .DefaultIfEmpty(0)
                .Max();

            try
            {
                string.Format(template, Enumerable.Range(0, count).Select(i => (object)("arg" + i)).ToArray());
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        internal static string Show(string text)
        {
            return text == null ? "(null)" : "\"" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }
    }

    /// <summary>
    /// Cobertura da traducao nas cenas do build: todo texto que o jogador pode ver precisa ter EN na tabela ou estar em
    /// KeepAsIs (Core/LocTable.cs). Protege contra editar uma frase PT na cena e perder o EN sem aviso.
    /// Abre as cenas de forma aditiva (sem Play Mode) e fecha no fim; nada e salvo.
    /// </summary>
    public sealed class LocalizationCoverageTests
    {
        private const string ItemsFolder = "Assets/PiscadaFatal/Items";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/1.unity";

        /// <summary>Os 15 itens do fluxo (mesma lista do ProjectDataTests).</summary>
        private static readonly string[] FlowItems =
        {
            "abridor_cartas", "chave_prateada", "ima", "chave_amarela", "maca", "balde", "pe_cabra",
            "pasta_arquivos", "olho_vidro", "caneta_tinteiro", "envelope_lacrado", "ficha_telefone",
            "relogio_bolso", "chave_camara", "manivela_poco"
        };

        private readonly List<Scene> openedScenes = new();
        private Scene gameplayScene;
        private Scene menuScene;

        [OneTimeSetUp]
        public void OpenScenes()
        {
            gameplayScene = Open(GameplayScenePath);
            menuScene = Open(MenuScenePath);
        }

        [OneTimeTearDown]
        public void CloseScenes()
        {
            foreach (Scene scene in openedScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            openedScenes.Clear();
        }

        private Scene Open(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded)
                return scene;

            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            openedScenes.Add(scene);
            return scene;
        }

        private static List<T> Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
        }

        [Test]
        public void TextosDasCenasDoBuildTemTraducaoOuSaoIguaisNosDoisIdiomas()
        {
            // Texto normalizado -> onde aparece (a falha lista todos de uma vez).
            Dictionary<string, List<string>> missing = new(StringComparer.Ordinal);

            foreach (Scene scene in new[] { menuScene, gameplayScene })
            {
                CollectSceneTexts(scene, missing);
                CollectDropdownOptions(scene, missing);
                CollectInteractableTexts(scene, missing);
                CollectFinaleLines(scene, missing);
            }

            int items = CollectItemNames(missing);

            if (missing.Count > 0)
                Assert.Fail(Report(missing));

            Assert.AreEqual(FlowItems.Length, items, "Itens do fluxo nao encontrados em " + ItemsFolder);
        }

        // ------------------------------------------------------------ fontes de texto

        private static void CollectSceneTexts(Scene scene, Dictionary<string, List<string>> missing)
        {
            foreach (TMP_Text text in Find<TMP_Text>(scene))
            {
                // Os 5 botoes do menu sao do LocalizationManager (tabela propria).
                if (text.GetComponent<LocalizedText>() != null)
                    continue;
                // Tooltip sem chamador no build (canvas inativo, textos de exemplo).
                if (text.GetComponentInParent<TooltipManager>(true) != null)
                    continue;
                // Trocado pelo numero de FPS em runtime.
                if (text.GetComponent<FpsText>() != null)
                    continue;
                // Legenda e itens do dropdown sao escritos pelo proprio dropdown a partir das opcoes (ver abaixo).
                if (text.GetComponentInParent<TMP_Dropdown>(true) != null)
                    continue;

                Check(text.text, Where(scene, text, "text"), missing);
            }
        }

        private static void CollectDropdownOptions(Scene scene, Dictionary<string, List<string>> missing)
        {
            foreach (TMP_Dropdown dropdown in Find<TMP_Dropdown>(scene))
            {
                if (dropdown.options == null)
                    continue;

                for (int i = 0; i < dropdown.options.Count; i++)
                {
                    string option = dropdown.options[i]?.text;
                    // Nomes do enum FullScreenMode: o LocSceneTexts troca pelos rotulos PT/EN da tabela em runtime.
                    if (string.IsNullOrEmpty(option) || Enum.IsDefined(typeof(FullScreenMode), option))
                        continue;

                    Check(option, Where(scene, dropdown, "options[" + i + "]"), missing);
                }
            }
        }

        private static void CollectInteractableTexts(Scene scene, Dictionary<string, List<string>> missing)
        {
            // Interagiveis e os controladores de puzzle que falam por pensamentos (bandeja de protocolo, trava de discos).
            List<MonoBehaviour> speakers = new();
            speakers.AddRange(Find<RaycastInteractable>(scene));
            speakers.AddRange(Find<ProtocolTray>(scene));
            speakers.AddRange(Find<DialLockMechanism>(scene));
            foreach (MonoBehaviour interactable in speakers)
            {
                SerializedObject serialized = new(interactable);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    // Entra em structs e listas (ex.: WellController.recipes[].placedThought), nunca dentro de strings.
                    enterChildren = property.propertyType == SerializedPropertyType.Generic;
                    if (property.propertyType != SerializedPropertyType.String || !IsShownTextField(property.name))
                        continue;
                    // Campo morto: InventoryItemPlacement.GetPrompt monta "Colocar {0}" e ignora o interactionPrompt.
                    if (interactable is InventoryItemPlacement && property.propertyPath == "interactionPrompt")
                        continue;

                    Check(property.stringValue, Where(scene, interactable, property.propertyPath), missing);
                }
            }
        }

        private static void CollectFinaleLines(Scene scene, Dictionary<string, List<string>> missing)
        {
            foreach (PiscadaFinale finale in Find<PiscadaFinale>(scene))
            {
                SerializedProperty lines = new SerializedObject(finale).FindProperty("lines");
                Assert.IsNotNull(lines, "PiscadaFinale sem o campo serializado 'lines'");

                for (int i = 0; i < lines.arraySize; i++)
                    Check(lines.GetArrayElementAtIndex(i).stringValue, Where(scene, finale, "lines[" + i + "]"), missing);
            }
        }

        private static int CollectItemNames(Dictionary<string, List<string>> missing)
        {
            HashSet<string> found = new(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:InventoryItemDefinition", new[] { ItemsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
                if (item == null || Array.IndexOf(FlowItems, item.Id) < 0)
                    continue;

                found.Add(item.Id);
                Check(item.DisplayName, path + " [InventoryItemDefinition.displayName]", missing);
            }

            return found.Count;
        }

        // ------------------------------------------------------------ apoio

        /// <summary>
        /// Prompts e pensamentos por estado (interactionPrompt, lockedPrompt, promptWithItem, ...Thought), title, thought
        /// e o texto dos documentos (ReadableDocument.body).
        /// </summary>
        private static bool IsShownTextField(string name)
        {
            return name.IndexOf("prompt", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("thought", StringComparison.OrdinalIgnoreCase) >= 0
                || name == "title"
                || name == "body";
        }

        private static void Check(string text, string where, Dictionary<string, List<string>> missing)
        {
            if (string.IsNullOrWhiteSpace(text) || !HasLetters(text))
                return;
            // O Loc normaliza (\r\n -> \n, Trim) nas duas consultas: o texto vai cru.
            if (Loc.TryGetEnglish(text, out _) || Loc.IsKeptAsIs(text))
                return;

            string key = text.Replace("\r\n", "\n").Trim();
            if (!missing.TryGetValue(key, out List<string> places))
            {
                places = new List<string>();
                missing.Add(key, places);
            }

            places.Add(where);
        }

        private static bool HasLetters(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                    return true;
            }

            return false;
        }

        private static string Where(Scene scene, Component component, string field)
        {
            List<string> names = new();
            for (Transform t = component.transform; t != null; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return scene.name + ": " + string.Join("/", names) + " [" + component.GetType().Name + "." + field + "]";
        }

        private static string Report(Dictionary<string, List<string>> missing)
        {
            StringBuilder report = new();
            report.Append(missing.Count)
                .Append(" textos sem traducao EN (adicione o par em Core/LocTable.cs, ou em KeepAsIs se for igual nos dois idiomas):");

            foreach (KeyValuePair<string, List<string>> pair in missing.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                report.Append('\n').Append(LocalizationTests.Show(pair.Key)).Append("\n    em ").Append(pair.Value[0]);
                if (pair.Value.Count > 1)
                    report.Append(" (+").Append(pair.Value.Count - 1).Append(" outros)");
            }

            return report.ToString();
        }
    }
}
