using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjetoCarta.Blink;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjetoCarta.Tests
{
    /// <summary>
    /// Regressao dos dados do jogo: itens, senha do cofre de simbolos, Build Settings e a fiacao dos puzzles da
    /// cena 1. Abre as cenas de forma aditiva (sem Play Mode) e fecha no fim; nada e salvo.
    /// </summary>
    public sealed class ProjectDataTests
    {
        private const string ItemsFolder = "Assets/PiscadaFatal/Items";
        private const string SymbolCodePath = "Assets/PiscadaFatal/Simbolos/CodigoCofreSimbolos.asset";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/1.unity";

        /// <summary>Os 15 itens do fluxo: cada um precisa de exatamente uma origem na cena 1.</summary>
        private static readonly string[] FlowItems =
        {
            "abridor_cartas", "chave_prateada", "ima", "chave_amarela", "maca", "balde", "pe_cabra",
            "pasta_arquivos", "olho_vidro", "caneta_tinteiro", "envelope_lacrado", "ficha_telefone",
            "relogio_bolso", "chave_camara", "manivela_poco"
        };

        private static readonly string[] RitualItems =
        {
            "caneta_tinteiro", "maca", "relogio_bolso", "ficha_telefone", "olho_vidro"
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

        private static string IdOf(Object asset)
        {
            return asset is InventoryItemDefinition item ? item.Id : null;
        }

        // ------------------------------------------------------------ itens e senha

        [Test]
        public void ItensTemIdsUnicosENomes()
        {
            string[] guids = AssetDatabase.FindAssets("t:InventoryItemDefinition", new[] { ItemsFolder });
            List<InventoryItemDefinition> items = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null)
                .ToList();

            Assert.IsNotEmpty(items);
            List<string> duplicated = items.GroupBy(item => item.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            Assert.IsEmpty(duplicated, "Ids repetidos: " + string.Join(", ", duplicated));

            foreach (string id in FlowItems)
            {
                InventoryItemDefinition item = items.FirstOrDefault(candidate => candidate.Id == id);
                Assert.IsNotNull(item, "Item do fluxo sem asset: " + id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.DisplayName), "Item sem nome: " + id);
                Assert.IsNotNull(item.Icon, "Item sem icone na roda: " + id);
            }
        }

        [Test]
        public void SenhaDoCofreDeSimbolosEhVermelhoAzulAmareloCinzaMarrom()
        {
            SymbolCodeDefinition code = AssetDatabase.LoadAssetAtPath<SymbolCodeDefinition>(SymbolCodePath);
            Assert.IsNotNull(code, "Asset nao encontrado: " + SymbolCodePath);

            string[] solution = code.Solution.Select(index => code.GetSymbol(index)?.id).ToArray();
            CollectionAssert.AreEqual(new[] { "vermelho", "azul", "amarelo", "cinza", "marrom" }, solution);

            // Cada simbolo aparece uma vez no keypad, e todos tem imagem. O papel mostra a cor e o cofre mostra
            // outro desenho (o objeto do painel com aquela cor): o jogador associa, nao copia.
            CollectionAssert.AreEquivalent(Enumerable.Range(0, code.Symbols.Count), code.KeypadLayout);
            foreach (SymbolCodeDefinition.Symbol symbol in code.Symbols)
            {
                Assert.IsNotNull(symbol.sprite, "Simbolo sem imagem: " + symbol.id);
                Assert.IsNotNull(symbol.keypadSprite, "Simbolo sem icone de objeto no cofre: " + symbol.id);
                Assert.AreNotEqual(symbol.sprite, symbol.keypadSprite, "O cofre repete o desenho do papel: " + symbol.id);
            }

            Assert.AreEqual(code.Symbols.Count, code.Symbols.Select(symbol => symbol.keypadSprite).Distinct().Count(),
                "Dois botoes do cofre com o mesmo objeto");
        }

        [Test]
        public void BuildComecaPeloMenuESegueParaACena1()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.GreaterOrEqual(scenes.Length, 2);
            Assert.AreEqual(MenuScenePath, scenes[0].path);
            Assert.AreEqual(GameplayScenePath, scenes[1].path);
        }

        // ------------------------------------------------------------ menu

        [Test]
        public void FotoDoCasoCarregaACenaDeGameplay()
        {
            List<CaseFileLauncher> launchers = Find<CaseFileLauncher>(menuScene);
            Assert.AreEqual(1, launchers.Count, "Esperada uma foto do caso no menu");
            Assert.AreEqual("1", new SerializedObject(launchers[0]).FindProperty("gameplayScene").stringValue);
        }

        // ------------------------------------------------------------ cena 1

        [Test]
        public void JogoComecaEmP1ComTresMomentos()
        {
            List<BlinkSceneController> controllers = Find<BlinkSceneController>(gameplayScene);
            Assert.AreEqual(1, controllers.Count);

            SerializedObject serialized = new(controllers[0]);
            Assert.AreEqual(0, serialized.FindProperty("startingMoment").intValue);

            SerializedProperty moments = serialized.FindProperty("moments");
            Assert.AreEqual(3, moments.arraySize);
            string[] names = Enumerable.Range(0, 3)
                .Select(i => moments.GetArrayElementAtIndex(i).objectReferenceValue?.name ?? string.Empty)
                .ToArray();
            StringAssert.Contains("SENTENCA", names[0]);
            StringAssert.Contains("ULTIMA_NOITE", names[1]);
            StringAssert.Contains("PERDAO", names[2]);
        }

        [Test]
        public void CadaItemDoFluxoTemExatamenteUmaOrigem()
        {
            Dictionary<string, int> origins = FlowItems.ToDictionary(id => id, _ => 0);
            foreach (InventoryPickup pickup in Find<InventoryPickup>(gameplayScene))
            {
                string id = pickup.Item != null ? pickup.Item.Id : null;
                if (id != null && origins.ContainsKey(id))
                    origins[id]++;
            }

            List<string> wrong = origins.Where(pair => pair.Value != 1).Select(pair => pair.Key + "=" + pair.Value).ToList();
            Assert.IsEmpty(wrong, "Itens sem origem unica: " + string.Join(", ", wrong));
        }

        [Test]
        public void PainelDoRitualPedeOsCincoObjetos()
        {
            List<RitualPanel> panels = Find<RitualPanel>(gameplayScene);
            Assert.AreEqual(1, panels.Count);

            SerializedProperty slots = new SerializedObject(panels[0]).FindProperty("slots");
            List<string> required = Enumerable.Range(0, slots.arraySize)
                .Select(i => slots.GetArrayElementAtIndex(i).objectReferenceValue as InventoryItemPlacement)
                .Select(slot => slot != null && slot.RequiredItem != null ? slot.RequiredItem.Id : "(vazio)")
                .ToList();

            CollectionAssert.AreEquivalent(RitualItems, required);
            Assert.IsNotNull(new SerializedObject(panels[0]).FindProperty("paper").objectReferenceValue, "Painel sem papel");
        }

        [Test]
        public void CofresTemKeypadsESenhasDiferentes()
        {
            List<NumericKeypad> numeric = Find<NumericKeypad>(gameplayScene);
            List<SymbolKeypad> symbol = Find<SymbolKeypad>(gameplayScene);
            Assert.AreEqual(1, numeric.Count, "Esperado um keypad numerico (P3)");
            Assert.AreEqual(1, symbol.Count, "Esperado um keypad de simbolos (P1)");

            Assert.AreEqual("2358", new SerializedObject(numeric[0]).FindProperty("code").stringValue);
            Object symbolCode = new SerializedObject(symbol[0]).FindProperty("code").objectReferenceValue;
            Assert.AreEqual(SymbolCodePath, AssetDatabase.GetAssetPath(symbolCode));

            foreach (SafeInteractable safe in Find<SafeInteractable>(gameplayScene))
                Assert.IsNotNull(new SerializedObject(safe).FindProperty("keypad").objectReferenceValue, safe.name + " sem keypad");
        }

        [Test]
        public void PocoTrocaPastaPorEnvelopeEImaPorRelogio()
        {
            List<WellController> wells = Find<WellController>(gameplayScene);
            Assert.AreEqual(1, wells.Count);

            SerializedObject serialized = new(wells[0]);
            Assert.AreEqual("balde", IdOf(serialized.FindProperty("bucketItem").objectReferenceValue));

            SerializedProperty recipes = serialized.FindProperty("recipes");
            Dictionary<string, string> results = new();
            for (int i = 0; i < recipes.arraySize; i++)
            {
                SerializedProperty recipe = recipes.GetArrayElementAtIndex(i);
                string bait = IdOf(recipe.FindPropertyRelative("bait").objectReferenceValue);
                InventoryPickup result = recipe.FindPropertyRelative("result").objectReferenceValue as InventoryPickup;
                if (bait != null)
                    results[bait] = result != null && result.Item != null ? result.Item.Id : null;
            }

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("envelope_lacrado", results["pasta_arquivos"]);
            Assert.AreEqual("relogio_bolso", results["ima"]);
        }

        // ------------------------------------------------------------ puzzles de P3 e manivela do poco

        [Test]
        public void PocoPedeAManivelaNoEixoEOBaldeNoGancho()
        {
            WellController well = Find<WellController>(gameplayScene).Single();
            SerializedObject serialized = new(well);
            Assert.AreEqual("manivela_poco", IdOf(serialized.FindProperty("crankItem").objectReferenceValue));

            Transform socket = serialized.FindProperty("crankSocket").objectReferenceValue as Transform;
            Assert.IsNotNull(socket, "Poco sem encaixe da manivela");
            Transform drum = serialized.FindProperty("crank").objectReferenceValue as Transform;
            Assert.IsTrue(socket.IsChildOf(drum), "O encaixe precisa girar com o tambor");
            Assert.IsNotNull(serialized.FindProperty("bottomAnchor").objectReferenceValue, "Poco sem fundo para a pasta");

            List<WellPart> parts = well.GetComponentsInChildren<WellPart>(true).ToList();
            Assert.AreEqual(1, parts.Count(part => part.Role == WellController.PartRole.CrankSocket), "Um encaixe de manivela");
            Assert.AreEqual(1, parts.Count(part => part.Role == WellController.PartRole.BucketHook), "Um gancho para o balde");
            Assert.IsTrue(parts.All(part => part.GetComponent<Collider>() != null), "Ponto do poco sem collider");

            SerializedProperty recipes = serialized.FindProperty("recipes");
            for (int i = 0; i < recipes.arraySize; i++)
            {
                SerializedProperty recipe = recipes.GetArrayElementAtIndex(i);
                string bait = IdOf(recipe.FindPropertyRelative("bait").objectReferenceValue);
                bool staysAtBottom = recipe.FindPropertyRelative("baitStaysAtBottom").boolValue;
                bool returns = recipe.FindPropertyRelative("baitReturnsWithResult").boolValue;
                if (bait == "pasta_arquivos")
                    Assert.IsTrue(staysAtBottom && !returns, "A pasta fica no fundo do poco");
                else if (bait == "ima")
                    Assert.IsTrue(staysAtBottom && !returns, "O ima e consumido: fica no fundo e nao volta ao inventario");
            }

            // Mira permissiva: um volume trigger cobre a estrutura toda e contem o gancho e o encaixe da manivela.
            InteractionVolume volume = well.GetComponentsInChildren<InteractionVolume>(true).SingleOrDefault();
            Assert.IsNotNull(volume, "Poco sem volume de mira amplo");
            BoxCollider box = volume.GetComponent<BoxCollider>();
            Assert.IsTrue(box != null && box.isTrigger, "O volume de mira e um BoxCollider trigger");
            foreach (WellPart part in parts)
            {
                Vector3 local = volume.transform.InverseTransformPoint(part.transform.position) - box.center;
                Assert.IsTrue(Mathf.Abs(local.x) <= box.size.x * 0.5f && Mathf.Abs(local.y) <= box.size.y * 0.5f &&
                              Mathf.Abs(local.z) <= box.size.z * 0.5f, "Ponto fora do volume de mira: " + part.name);
            }
        }

        [Test]
        public void BandejaDeProtocoloLiberaAPastaComOsTresDocumentosEmOrdem()
        {
            ProtocolTray tray = Find<ProtocolTray>(gameplayScene).Single();
            StringAssert.Contains("PERDAO", Moment(tray.transform));
            SerializedObject serialized = new(tray);

            SerializedProperty order = serialized.FindProperty("startingOrder");
            Assert.AreEqual(3, order.arraySize);
            Assert.AreEqual(3, serialized.FindProperty("slots").arraySize);
            List<int> ranks = new();
            for (int i = 0; i < order.arraySize; i++)
            {
                SerializedProperty entry = order.GetArrayElementAtIndex(i);
                ReadableDocument document = entry.FindPropertyRelative("document").objectReferenceValue as ReadableDocument;
                Assert.IsNotNull(document, "Espaco " + i + " sem documento");
                Assert.IsTrue(document.HasBody, document.name + " sem texto");
                ranks.Add(entry.FindPropertyRelative("chronologicalRank").intValue);
            }

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, ranks, "Ordem cronologica deve ser 0, 1 e 2");
            CollectionAssert.AreNotEqual(new[] { 0, 1, 2 }, ranks, "A bandeja nao pode comecar resolvida");
            Assert.AreEqual(2, tray.GetComponentsInChildren<ProtocolSwapHandle>(true).Length, "Duas alavancas de troca");

            OpenableInteractable reward = serialized.FindProperty("reward").objectReferenceValue as OpenableInteractable;
            Assert.IsNotNull(reward, "Bandeja sem gaveta de recompensa");
            Assert.IsTrue(reward.IsLockedByPuzzle && reward.RequiredItem == null, "A gaveta so abre pela bandeja");
            Assert.IsTrue(ContentIds(reward).Contains("pasta_arquivos"), "A pasta esta na gaveta do arquivo");
        }

        [Test]
        public void TravaDeDiscosBateComOEsquemaDoRelatorioELiberaAManivela()
        {
            DialLockMechanism mechanism = Find<DialLockMechanism>(gameplayScene).Single();
            StringAssert.Contains("PERDAO", Moment(mechanism.transform));
            Assert.AreEqual(3, mechanism.Dials.Count);
            Assert.IsTrue(mechanism.Dials.All(dial => dial != null && dial.Positions == mechanism.Dials[0].Positions));
            Assert.IsFalse(mechanism.Dials.Select((dial, i) => new SerializedObject(dial).FindProperty("startPosition").intValue == mechanism.Solution[i]).All(x => x),
                "A trava nao pode comecar resolvida");

            // A pista nao desenha a trava: o relatorio da os horarios do mecanico, e cada disco de N posicoes e um
            // mostrador de relogio (posicao k = k * 12/N horas, 0 = meio-dia). Os horarios, na ordem, sao a solucao.
            ReadableDocument report = Find<ReadableDocument>(gameplayScene).Single(doc => doc.name == "Relatorio_Manutencao");
            string body = new SerializedObject(report).FindProperty("body").stringValue;
            int positions = mechanism.Dials[0].Positions;
            int searchFrom = 0;
            foreach (int step in mechanism.Solution)
            {
                int minutes = step * 720 / positions;
                string time = (minutes / 60 == 0 ? 12 : minutes / 60) + "h" + (minutes % 60).ToString("00");
                int at = body.IndexOf(time, searchFrom, System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(at, 0, "O relatorio nao traz o horario " + time + " (na ordem dos discos)");
                searchFrom = at + time.Length;
            }

            Texture paper = new SerializedObject(report).FindProperty("documentTexture").objectReferenceValue as Texture;
            Assert.IsNotNull(paper);
            StringAssert.EndsWith("papel_em_branco.png", AssetDatabase.GetAssetPath(paper), "O relatorio voltou a mostrar um desenho da trava");

            OpenableInteractable reward = new SerializedObject(mechanism).FindProperty("reward").objectReferenceValue as OpenableInteractable;
            Assert.IsNotNull(reward);
            Assert.IsTrue(reward.IsLockedByPuzzle && reward.RequiredItem == null, "O compartimento so abre pela trava");
            Assert.IsTrue(ContentIds(reward).Contains("manivela_poco"), "A manivela esta no compartimento");
        }

        [Test]
        public void DocumentosDoCasoEstaoNosTresMomentos()
        {
            List<ReadableDocument> documents = Find<ReadableDocument>(gameplayScene).Where(doc => doc.HasBody).ToList();
            Assert.GreaterOrEqual(documents.Count, 8);
            HashSet<string> moments = new(documents.Select(doc => Moment(doc.transform)));
            Assert.AreEqual(3, moments.Count, "Documentos em: " + string.Join(", ", moments));
        }

        [Test]
        public void TexturasDosDocumentosNaoSaoRedimensionadas()
        {
            // A leitura em tela cheia usa a proporcao da textura importada: com Non-Power of 2 o Unity muda o tamanho
            // (1200x900 vira 1024x1024) e o papel aparece esticado.
            foreach (ReadableDocument document in Find<ReadableDocument>(gameplayScene))
            {
                Texture texture = new SerializedObject(document).FindProperty("documentTexture").objectReferenceValue as Texture;
                if (texture == null)
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                Assert.IsNotNull(importer, document.name + ": textura sem importer");
                Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale,
                    document.name + ": " + importer.assetPath + " precisa de Non-Power of 2 = None");
            }
        }

        private static string Moment(Transform t)
        {
            while (t.parent != null && t.parent.parent != null)
                t = t.parent;
            return t.name;
        }

        private static List<string> ContentIds(OpenableInteractable openable)
        {
            SerializedProperty contents = new SerializedObject(openable).FindProperty("contents");
            List<string> ids = new();
            for (int i = 0; i < contents.arraySize; i++)
            {
                if (contents.GetArrayElementAtIndex(i).objectReferenceValue is InventoryPickup pickup && pickup.Item != null)
                    ids.Add(pickup.Item.Id);
            }

            return ids;
        }

        [Test]
        public void FechadurasPedemOItemCerto()
        {
            Dictionary<string, string> expected = new()
            {
                { "Bau_Prateado", "chave_prateada" },
                { "Porta_Armario_Maca", "chave_amarela" },
                { "Caixa_Metalica_Parede", "pe_cabra" },
                { "Tampa_Metalica", "pe_cabra" },
                { "Caixa_Controle_Cadeira", "chave_camara" },
                { "Gaveta_Superior", null },
                { "Gaveta_Inferior", null },
                // Trancadas por puzzle (sem item): bandeja de protocolo e trava de discos.
                { "Drawer_L_Big", null },
                { "Portinhola", null },
                // Portas do armario alto de P3: abrem livres, cada uma na sua dobradica.
                { "Dobradica_Porta_Direita", null },
                { "Dobradica_Porta_Esquerda", null },
                // Gavetas da direita da mesa do cofre numerico (P3): abrem livres.
                { "Drawer_R_Upper", null },
                { "Drawer_R_Lower", null }
            };

            Dictionary<string, OpenableInteractable> openables = new();
            foreach (OpenableInteractable openable in Find<OpenableInteractable>(gameplayScene))
                openables[openable.name] = openable;

            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.IsTrue(openables.ContainsKey(pair.Key), "Nao encontrado: " + pair.Key);
                InventoryItemDefinition required = openables[pair.Key].RequiredItem;
                Assert.AreEqual(pair.Value, required != null ? required.Id : null, pair.Key);
            }
        }
    }
}
