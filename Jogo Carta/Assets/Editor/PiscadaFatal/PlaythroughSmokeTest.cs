using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjetoCarta.Blink;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.Puzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.EditorTools
{
    /// <summary>
    /// Teste de fumaca do fluxo completo da Piscada Fatal (cena 1, em Play Mode).
    /// Anda ate cada objeto, mira com a camera e "clica" pelo mesmo caminho do mouse
    /// (<see cref="PlayerRaycastInteractor.InteractWithFocused"/>), incluindo tentativas erradas.
    /// O relatorio vai para o Console (PASS/FAIL por etapa).
    /// </summary>
    public static class PlaythroughSmokeTest
    {
        private static readonly StringBuilder report = new();
        private static readonly List<string> thoughts = new();

        private static FirstPersonController player;
        private static CharacterController controller;
        private static PlayerRaycastInteractor interactor;
        private static InventoryController inventory;
        private static BlinkSceneController blink;
        private static Transform scenes;
        private static float groundedY;

        public static bool Running { get; private set; }
        public static int Failures { get; private set; }
        public static string Report => report.ToString();

        [MenuItem("Projeto Carta/Piscada Fatal/Teste automatico do fluxo (Play Mode)")]
        public static void StartTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Abra a cena 1 e entre em Play Mode antes de rodar o teste.");
                return;
            }

            if (Running)
                return;

            CoroutineHost.Run(Run());
        }

        private static IEnumerator Run()
        {
            Running = true;
            Failures = 0;
            report.Clear();
            thoughts.Clear();
            TomasThoughts.Raised += OnThought;
            // Os prompts montados (Colocar {0}, isca do poco, painel) saem traduzidos do GetPrompt: o teste confere em PT
            // qualquer que seja o idioma salvo. Restaurado no fim (e zerado pelo Loc a cada Play).
            Loc.OverrideForTests(Language.PT);
            try
            {
                yield return new WaitForSeconds(2f);
                player = Object.FindFirstObjectByType<FirstPersonController>();
                controller = player.GetComponent<CharacterController>();
                interactor = Object.FindFirstObjectByType<PlayerRaycastInteractor>();
                inventory = Object.FindFirstObjectByType<InventoryController>();
                blink = Object.FindFirstObjectByType<BlinkSceneController>();
                scenes = blink.transform;
                groundedY = player.transform.position.y;

                Check(blink.CurrentMoment == 0, "Comeca em P1 (A Sentenca)");

                // ---------------------------------------------------------------- P1
                string p1 = "CENA_1_SENTENCA/Pontos_Fixos_3x3/";
                yield return Use(p1 + "P5/P5_Mesa_Escritorio/Gavetas/handle2_lp.003", Vector3.back, 1.0f, "Abrir gaveta");
                yield return new WaitForSeconds(0.6f);
                yield return Use(p1 + "P5/P5_Mesa_Escritorio/Gavetas/handle2_lp.003/Abridor_de_Cartas", new Vector3(0f, 1.6f, -1f), 1.0f, "abridor");
                Check(Has("abridor_cartas"), "Gaveta -> abridor de cartas");

                yield return Use(p1 + "P5/Envelope_Inicial", Vector3.back, 1.1f, "abridor");
                yield return new WaitForSeconds(1.5f);
                yield return Use(p1 + "P5/Envelope_Inicial/Chave_Prateada", new Vector3(0f, 1.6f, -1f), 1.1f, "prateada");
                Check(Has("chave_prateada"), "Envelope inicial -> chave prateada");
                Check(Has("abridor_cartas"), "Abridor nao foi consumido (ainda serve para o envelope do poco)");

                // tentativa errada: chave prateada no armario da maca
                string closet = p1 + "P1/Armario_Maca/Porta_Armario_Maca/";
                yield return Use(closet + "Door", Vector3.left, 1.3f, null);
                Check(LastThoughtContains("chave"), "Chave errada no armario gera pensamento");

                yield return Equip("chave_prateada");
                yield return Use(p1 + "P1/Bau_Prateado/Abertura/Tampa", Vector3.left, 1.3f, "prateada");
                yield return new WaitForSeconds(1.0f);
                Check(!Has("chave_prateada"), "Chave prateada usada no primeiro bau");
                yield return Use(p1 + "P1/Bau_Prateado/Abertura/Conteudo/Ima", new Vector3(-1f, 1.6f, 0f), 0.6f, "ímã");
                yield return Use(p1 + "P1/Bau_Prateado/Abertura/Conteudo/Chave_Amarela", new Vector3(-1f, 1.6f, 0f), 0.6f, "amarela");
                Check(Has("ima") && Has("chave_amarela"), "Primeiro bau -> ima e chave amarela");

                InventoryPickup apple = Find(p1 + "P1/Armario_Maca/Maca").GetComponent<InventoryPickup>();
                Check(!apple.CanInteract(new InteractionContext(interactor, inventory)), "Maca inacessivel com o armario trancado");
                yield return Equip("chave_amarela");
                yield return Use(closet + "Door", Vector3.left, 1.3f, "amarela");
                yield return new WaitForSeconds(1.0f);
                Check(!Has("chave_amarela"), "Chave amarela usada no armario");
                yield return Use(p1 + "P1/Armario_Maca/Maca", new Vector3(-1f, 0.4f, 0f), 0.9f, "maçã");
                Check(Has("maca"), "Armario trancado -> maca");

                // fechar e reabrir a porta do armario (prompt muda)
                yield return Use(closet + "Door", Vector3.left, 1.3f, "Fechar");
                yield return new WaitForSeconds(0.8f);
                yield return Use(closet + "Door", Vector3.left, 1.3f, "Abrir");
                yield return new WaitForSeconds(0.8f);

                // ---------------------------------------------------------------- piscadas ate P3
                yield return BlinkOnce(1);
                yield return BlinkOnce(2);

                // ---------------------------------------------------------------- P3
                string p3 = "CENA_3_PERDAO/Pontos_Fixos_3x3/";
                yield return Use(p3 + "P5/Armario_Baixo/Gaveta_Inferior/Frente", Vector3.back, 1.2f, "Abrir gaveta");
                yield return new WaitForSeconds(1.0f);
                yield return Use(p3 + "P5/Armario_Baixo/Gaveta_Inferior/Conteudo/Pe_de_Cabra", new Vector3(0f, 1.6f, -1f), 1.0f, "cabra");
                yield return Use(p3 + "P5/Armario_Baixo/Gaveta_Inferior/Frente", Vector3.back, 1.2f, "Fechar gaveta");
                yield return new WaitForSeconds(0.8f);
                yield return Use(p3 + "P5/Armario_Baixo/Gaveta_Superior/Frente", Vector3.back, 1.2f, "Abrir gaveta");
                yield return new WaitForSeconds(1.0f);
                yield return Use(p3 + "P5/Armario_Baixo/Gaveta_Superior/Frente/Balde", new Vector3(0f, 1.6f, -1f), 1.1f, "balde");
                Check(Has("balde") && Has("pe_cabra"), "Armario baixo abre sem chave -> balde e pe de cabra");

                // Bandeja de protocolo: documentos legiveis, gaveta trancada ate a ordem cronologica.
                // A mesa do arquivo fica no meio da parede direita (P6), separada do cofre numerico; frente para -x.
                string desk = p3 + "P6/";
                string archive = desk + "Arquivo_Protocolo/";
                string drawer = desk + "Desk_Animated/Desk/Drawer_L_Big/";
                ProtocolTray tray = Find(desk + "Arquivo_Protocolo").GetComponent<ProtocolTray>();
                Check(!tray.IsInChronologicalOrder(), "Bandeja de protocolo comeca fora de ordem");
                yield return Use(desk + "Protocolo_Regra", Vector3.left, 0.9f, "protocolo");
                yield return new WaitForSeconds(0.5f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Pista do protocolo lida em tela cheia");
                yield return CloseDocuments();
                yield return Use(archive + "Doc_Sentenca", Vector3.left, 0.9f, "sentença");
                yield return new WaitForSeconds(0.5f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Documento da bandeja lido em tela cheia");
                yield return CloseDocuments();

                yield return Use(drawer + "Colisor_Frente", Vector3.left, 1.0f, "trancada");
                Check(LastThoughtContains("protocolo"), "Gaveta do arquivo trancada explica o motivo");
                yield return Use(archive + "Alavanca_I_II", Vector3.left, 0.9f, "Trocar");
                yield return new WaitForSeconds(0.8f);
                Check(!tray.IsSolved, "Uma troca so: ordem ainda errada, gaveta continua trancada");
                yield return Use(archive + "Alavanca_II_III", Vector3.left, 0.9f, "Trocar");
                yield return new WaitForSeconds(2.0f);
                Check(tray.IsSolved && tray.IsInChronologicalOrder(), "Sentenca -> pedido -> despacho resolve a bandeja");
                Check(LastThoughtContains("gaveta"), "Bandeja resolvida: Tomas comenta");
                yield return Use(drawer + "Pasta_Arquivos", Vector3.left, 0.8f, "pasta");
                Check(Has("pasta_arquivos"), "Gaveta do arquivo -> pasta com arquivos");
                yield return Use(drawer + "Relatorio_Manutencao", Vector3.left, 0.8f, "relatório");
                yield return new WaitForSeconds(0.5f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Relatorio de manutencao (anexo da pasta) legivel");
                yield return CloseDocuments();

                // Compartimento de manutencao (parede da janela): cada disco na hora anotada no relatorio.
                string box = p3 + "P1/Compartimento_Manutencao/";
                DialLockMechanism dialLock = Find(p3 + "P1/Compartimento_Manutencao").GetComponent<DialLockMechanism>();
                yield return Use(box + "Portinhola/Tampa", Vector3.right, 1.0f, "trancado");
                Check(LastThoughtContains("discos"), "Compartimento trancado aponta os discos");
                int[] turns = { dialLock.Solution[0], dialLock.Solution[1], dialLock.Solution[2] - 1 };
                string[] dialNames = { "Disco_Esquerda", "Disco_Meio", "Disco_Direita" };
                for (int d = 0; d < 3; d++)
                {
                    for (int k = 0; k < turns[d]; k++)
                        yield return Use(box + dialNames[d], Vector3.right, 0.9f, "Girar");
                }
                yield return new WaitForSeconds(0.6f);
                Check(!dialLock.IsSolved, "Disco da direita um dente antes: a trava nao solta");
                yield return Use(box + "Disco_Direita", Vector3.right, 0.9f, "Girar");
                yield return new WaitForSeconds(1.6f);
                Check(dialLock.IsSolved, "Discos na posicao do esquema destravam o compartimento");
                yield return Use(box + "Manivela", Vector3.right, 0.9f, "manivela");
                Check(Has("manivela_poco"), "Compartimento de manutencao -> manivela");

                yield return Equip("maca");
                yield return Use(p3 + "P7/Caixa_Metalica_Parede/Dobradica_Tampa", Vector3.right, 1.3f, null);
                Check(LastThoughtContains("ferramenta") || LastThoughtContains("metal"), "Caixa metalica sem pe de cabra gera pensamento");
                yield return Equip("pe_cabra");
                yield return Use(p3 + "P7/Caixa_Metalica_Parede/Dobradica_Tampa", Vector3.right, 1.3f, "pé de cabra");
                yield return new WaitForSeconds(1.0f);
                yield return Use(p3 + "P7/Caixa_Metalica_Parede/Corpo/Olho_de_Vidro", Vector3.right, 1.0f, "olho");
                Check(Has("olho_vidro"), "Pe de cabra -> caixa metalica -> olho de vidro");

                yield return SolveNumericSafe(p3 + "P3/Cofre_Numerico");
                yield return Use(p3 + "P3/Cofre_Numerico/Caneta_Tinteiro", Vector3.back, 1.1f, "caneta");
                Check(Has("caneta_tinteiro"), "Cofre numerico 2358 -> caneta-tinteiro");

                // ---------------------------------------------------------------- P3 -> P1 -> P2
                yield return BlinkOnce(0);
                yield return BlinkOnce(1);

                // ---------------------------------------------------------------- P2
                string p2 = "CENA_2_ULTIMA_NOITE/Pontos_Fixos_3x3/";
                yield return Use(p2 + "P3/Relogio", Vector3.left, 1.6f, "Relógio");
                Check(LastThoughtContains("23:58"), "Relogio parado em 23:58 comentado");

                yield return Equip("pe_cabra");
                yield return Use(p2 + "P1/Tampa_Metalica/Dobradica_Tampa/Tampa", Vector3.back, 1.4f, "pé de cabra");
                yield return new WaitForSeconds(1.2f);
                yield return Use(p2 + "P1/Tampa_Metalica/Esquema_Estrela_5_Posicoes", Vector3.back, 1.2f, "esquema");
                yield return new WaitForSeconds(0.5f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Esquema aberto em tela cheia (cursor liberado)");
                Object.FindFirstObjectByType<ReadableDocument>()?.Close();
                foreach (ReadableDocument doc in Object.FindObjectsByType<ReadableDocument>(FindObjectsSortMode.None))
                    doc.Close();
                yield return new WaitForSeconds(0.4f);
                Check(!GameplayLocks.IsLocked(GameplayLock.Movement), "Fechar o esquema devolve o controle");

                yield return Use(p2 + "P5/P5_Poco/Placa_Guincho", Vector3.right, 1.0f, "placa");
                yield return new WaitForSeconds(0.5f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Placa do guincho legivel em tela cheia");
                yield return CloseDocuments();

                string well = p2 + "P5/P5_Poco/Poço";
                string hook = p2 + "P5/P5_Poco/Gancho_Balde";
                string socket = p2 + "P5/P5_Poco/Poço/crank/Encaixe_Manivela";
                WellController wellController = Find(p2 + "P5/P5_Poco").GetComponent<WellController>();
                yield return Equip("ima");
                yield return Use(well, Vector3.back, 1.3f, null);
                Check(LastThoughtContains("poço") || LastThoughtContains("fundo"), "Poco sem balde gera pensamento");
                // Area de mira ampla: mirando no poste do guincho (longe do gancho), o balde vai para o gancho.
                yield return Equip("balde");
                yield return Use(p2 + "P5/P5_Poco/Poço/Cube", Vector3.back, 1.3f, "Prender o balde");
                yield return new WaitForSeconds(0.8f);
                Check(wellController.HasBucket && !Has("balde") && Find(hook).childCount > 3,
                      "Balde preso no gancho da corda mirando o poste do poco");
                yield return Use(well, Vector3.back, 1.3f, null);
                Check(LastThoughtContains("balde"), "Baixar sem isca gera pensamento");

                yield return Equip("pasta_arquivos");
                yield return Use(well, Vector3.back, 1.3f, "pasta");
                yield return new WaitForSeconds(0.7f);
                yield return Use(well, Vector3.back, 1.3f, "sem manivela");
                Check(!wellController.IsRunning && LastThoughtContains("manivela"), "Sem manivela o guincho nao gira e explica");

                // Manivela: mirando no telhado do poco ela encaixa na ponta do eixo (um clique, uma instalacao).
                yield return Equip("manivela_poco");
                yield return Use(p2 + "P5/P5_Poco/Poço/Roof", Vector3.back, 1.4f, "Encaixar");
                yield return new WaitForSeconds(0.8f);
                Check(wellController.InstalledCrank != null && wellController.InstalledCrank.transform.parent == Find(socket),
                      "Manivela encaixada na ponta do eixo mirando o telhado");
                Transform drum = Find(p2 + "P5/P5_Poco/Poço/crank");
                Check(wellController.HasCrank && !Has("manivela_poco") && wellController.InstalledCrank != null &&
                      wellController.InstalledCrank.transform.IsChildOf(drum), "Manivela encaixada vira parte do guincho");

                Quaternion drumBefore = drum.localRotation;
                yield return Use(well, Vector3.back, 1.3f, "Baixar");
                yield return new WaitForSeconds(1.2f);
                Check(wellController.IsRunning && Quaternion.Angle(drumBefore, drum.localRotation) > 5f, "Tambor e manivela giram enquanto o balde desce");
                yield return new WaitForSeconds(6.3f);
                Check(LastThoughtContains("fisgou"), "Envelope sobe preso pelo clipe da pasta");
                Check(Find(p2 + "P5/P5_Poco/Fundo_do_Poco").childCount == 1, "A pasta ficou no fundo do poco");
                yield return Use(p2 + "P5/P5_Poco/Gancho_Balde/Conteudo/Envelope_Lacrado", Vector3.back, 1.2f, "envelope");
                Check(Has("envelope_lacrado"), "Pasta no poco -> envelope lacrado");

                yield return Equip("ima");
                yield return Use(well, Vector3.back, 1.3f, "ímã");
                yield return new WaitForSeconds(0.7f);
                yield return Use(well, Vector3.back, 1.3f, "Baixar");
                yield return new WaitForSeconds(7.5f);
                Check(LastThoughtContains("ímã"), "Relogio sobe no balde e o ima fica no fundo");
                Check(Find(p2 + "P5/P5_Poco/Fundo_do_Poco").childCount == 2, "O ima ficou no fundo do poco, com a pasta");
                yield return Use(p2 + "P5/P5_Poco/Gancho_Balde/Conteudo/Relogio_de_Bolso", Vector3.back, 1.2f, "relógio");
                Check(Has("relogio_bolso") && !Has("ima"), "Ima no poco -> relogio de bolso; o ima foi consumido");
                Check(Find(p2 + "P5/P5_Poco/Gancho_Balde/Conteudo").GetComponentsInChildren<Renderer>().Length == 0,
                      "Nada volta no balde depois do relogio (o ima nao reaparece)");

                yield return Equip("relogio_bolso");
                yield return Use(well, Vector3.back, 1.3f, null);
                Check(LastThoughtContains("jogar") || LastThoughtContains("fundo"), "Outro item no poco nao gera recompensa");
                Check(CountPickups("envelope_lacrado") == 1 && CountPickups("relogio_bolso") == 1, "Recompensas do poco nao duplicam");

                // ---------------------------------------------------------------- P2 -> P3 -> P1
                yield return BlinkOnce(2);
                yield return BlinkOnce(0);

                // ---------------------------------------------------------------- P1: envelope, painel, cofre de simbolos
                yield return Equip("envelope_lacrado");
                yield return Use(p1 + "P5/Mesa_Ponto_Envelope", Vector3.back, 1.0f, "envelope");
                yield return new WaitForSeconds(0.6f);
                yield return Equip("abridor_cartas");
                yield return Use(p1 + "P5/Mesa_Ponto_Envelope", Vector3.back, 1.0f, "abridor");
                yield return new WaitForSeconds(1.6f);
                yield return Use(p1 + "P5/Mesa_Ponto_Envelope/Ficha_Telefone", new Vector3(0f, 1.6f, -1f), 0.9f, "ficha");
                Check(Has("ficha_telefone"), "Envelope do poco aberto em P1 -> ficha de telefone");
                Transform letter = Find(p1 + "P5/Bilhete_Helena");
                Check(letter != null && letter.gameObject.activeInHierarchy, "Envelope aberto tambem revela o bilhete de Helena");
                yield return Use(p1 + "P5/Bilhete_Helena", new Vector3(0f, 1.6f, -1f), 0.9f, "bilhete");
                yield return new WaitForSeconds(0.5f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Bilhete de Helena legivel");
                yield return CloseDocuments();

                // Encaixes aceitam qualquer item do ritual; o painel so completa com cada item na posicao certa.
                string slots = p1 + "P3/Painel_do_Ritual/Encaixes_5_Objetos/";
                RitualPanel ritual = Find(p1 + "P3/Painel_do_Ritual").GetComponent<RitualPanel>();
                InventoryItemPlacement caneta = Find(slots + "Encaixe_CANETA").GetComponent<InventoryItemPlacement>();
                InventoryItemPlacement maca = Find(slots + "Encaixe_MACA").GetComponent<InventoryItemPlacement>();

                // 1) Maca no encaixe da caneta: entra (errado), e Retirar devolve a maca a mao.
                yield return Equip("maca");
                yield return Use(slots + "Encaixe_CANETA", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                Check(caneta.IsOccupied && !caneta.IsCorrect, "Objeto de outra posicao entra no encaixe (fica errado)");
                yield return Use(slots + "Encaixe_CANETA", Vector3.back, 1.7f, "Retirar");
                yield return new WaitForSeconds(0.6f);
                Check(!caneta.IsOccupied && inventory.EquippedItem != null && inventory.EquippedItem.Id == "maca",
                    "Retirar devolve o item a mao e libera o encaixe");

                // 2) Caneta e maca trocadas de lugar, o resto certo: cinco preenchidos, mas fora do lugar -> nao completa.
                yield return Equip("maca");
                yield return Use(slots + "Encaixe_CANETA", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                yield return Equip("caneta_tinteiro");
                yield return Use(slots + "Encaixe_MACA", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                yield return Equip("relogio_bolso");
                yield return Use(slots + "Encaixe_RELOGIO", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                yield return Equip("ficha_telefone");
                yield return Use(slots + "Encaixe_FICHA", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                yield return Equip("olho_vidro");
                yield return Use(slots + "Encaixe_OLHO", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                Check(!ritual.IsCompleted && ritual.CorrectCount == 3, "Cinco no painel com duas trocadas: nao completa");
                Check(LastThoughtContains("lugar"), "Painel cheio fora do lugar: Tomas comenta");

                // 3) Corrige: tira caneta e maca dos encaixes trocados e coloca cada uma na sua posicao.
                yield return Use(slots + "Encaixe_MACA", Vector3.back, 1.7f, "Retirar");
                yield return new WaitForSeconds(0.6f);
                yield return Use(slots + "Encaixe_CANETA", Vector3.back, 1.7f, "Retirar");
                yield return new WaitForSeconds(0.6f);
                Check(!maca.IsOccupied && !caneta.IsOccupied, "Dois encaixes trocados liberados");
                yield return Equip("maca");
                yield return Use(slots + "Encaixe_MACA", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                yield return Equip("caneta_tinteiro");
                yield return Use(slots + "Encaixe_CANETA", Vector3.back, 1.7f, "Colocar");
                yield return new WaitForSeconds(0.8f);
                Check(maca.IsCorrect && caneta.IsCorrect, "Maca e caneta na posicao certa");

                yield return new WaitForSeconds(2.6f);
                Transform paper = Find(p1 + "P3/Papel_Senha_Simbolos");
                Check(paper != null && paper.gameObject.activeInHierarchy, "Cinco objetos no painel -> papel revelado");
                Check(ritual.IsCompleted, "Painel marcado como completo");

                yield return SolveSymbolSafe(p1 + "P8/Cofre_Simbolos");
                yield return Use(p1 + "P8/Cofre_Simbolos/Chave_Camara", Vector3.right, 1.1f, "câmara");
                Check(Has("chave_camara"), "Cofre de simbolos -> chave da camara");
                Check(CountPickups("chave_camara") == 1, "Chave da camara tem uma unica origem");

                // ---------------------------------------------------------------- P1 -> P2: final
                yield return BlinkOnce(1);
                string door = p2 + "P6/P6_Porta_da_Camara/Porta_da_Camara/Folha_Porta";
                yield return Use(door, Vector3.left, 1.5f, "Porta");
                Check(LastThoughtContains("A chave não entra") || LastThoughtContains("cadeira"), "Porta trancada antes de desligar a cadeira");

                yield return Equip("chave_camara");
                yield return Use(p2 + "P8/Caixa_Controle_Cadeira/Dobradica_Porta/Porta", Vector3.forward, 1.2f, "chave da câmara");
                yield return new WaitForSeconds(1.2f);
                yield return Use(p2 + "P8/Caixa_Controle_Cadeira/Alavanca", Vector3.forward, 1.0f, "Desligar");
                yield return new WaitForSeconds(2.5f);
                Check(Find(door).GetComponentInParent<FinalDoor>().IsUnlocked, "Desligar a cadeira destrava a porta");

                yield return Use(door, Vector3.left, 1.5f, "Abrir a porta");
                yield return new WaitForSeconds(22f);
                Check(GameplayLocks.IsLocked(GameplayLock.Cursor), "Final termina com botoes (cursor liberado)");

                report.Insert(0, "=== Teste do fluxo Piscada Fatal: " + (Failures == 0 ? "TUDO OK" : Failures + " FALHA(S)") + " ===\n");
                Debug.Log(report.ToString());
            }
            finally
            {
                // Tambem quando uma etapa lanca excecao (objeto da cena sumiu): o idioma do jogador volta,
                // o pensamento deixa de ser escutado e o teste pode ser rodado de novo sem sair do Play Mode.
                TomasThoughts.Raised -= OnThought;
                Loc.OverrideForTests(null);
                Running = false;
            }
        }

        private static IEnumerator CloseDocuments()
        {
            foreach (ReadableDocument doc in Object.FindObjectsByType<ReadableDocument>(FindObjectsSortMode.None))
                doc.Close();
            yield return new WaitForSeconds(0.4f);
        }

        // ---------------------------------------------------------------- cofres

        private static IEnumerator SolveNumericSafe(string path)
        {
            SafeInteractable safe = Find(path).GetComponent<SafeInteractable>();
            yield return Use(path + "/SafeDoor", Vector3.back, 1.3f, "teclado");
            yield return new WaitForSeconds(0.8f);
            Check(safe.IsInSafeView && GameplayLocks.IsLocked(GameplayLock.Movement | GameplayLock.Blink | GameplayLock.Pause),
                "Cofre numerico: camera de aproximacao e jogador travado");

            NumericKeypad keypad = safe.GetComponentInChildren<NumericKeypad>(true);
            yield return Press(keypad, "Tecla_1", "Tecla_1", "Tecla_1", "Tecla_1", "Tecla_9", "Tecla_ENTER");
            Check(keypad.Entry.Count <= 4, "Keypad numerico limita a 4 digitos");
            yield return new WaitForSeconds(1.0f);
            Check(!safe.IsSolved && keypad.Entry.Count == 0 && LastThoughtContains("código"), "Codigo errado: feedback e telinha limpa");

            // Esc (via saida do cofre) devolve tudo
            safe.ExitSafeView();
            yield return new WaitForSeconds(0.5f);
            Check(!GameplayLocks.IsLocked(GameplayLock.Movement) && !GameplayLocks.IsLocked(GameplayLock.Cursor), "Sair do keypad restaura movimento e cursor");

            yield return Use(path + "/SafeDoor", Vector3.back, 1.3f, "teclado");
            yield return new WaitForSeconds(0.8f);
            yield return Press(keypad, "Tecla_2", "Tecla_3", "Tecla_APAGAR", "Tecla_3", "Tecla_5", "Tecla_8", "Tecla_ENTER");
            yield return new WaitForSeconds(2.2f);
            Check(safe.IsSolved, "Codigo 2358 abre o cofre numerico");
        }

        private static IEnumerator SolveSymbolSafe(string path)
        {
            SafeInteractable safe = Find(path).GetComponent<SafeInteractable>();
            yield return Use(path + "/SafeDoor", Vector3.right, 1.2f, "símbolos");
            yield return new WaitForSeconds(0.8f);
            SymbolKeypad keypad = safe.GetComponentInChildren<SymbolKeypad>(true);
            yield return Press(keypad, "Tecla_azul", "Tecla_vermelho", "Tecla_amarelo", "Tecla_cinza", "Tecla_marrom", "Tecla_ENTER");
            yield return new WaitForSeconds(1.0f);
            Check(!safe.IsSolved && LastThoughtContains("sequência"), "Sequencia errada: feedback");
            yield return Press(keypad, "Tecla_vermelho", "Tecla_azul", "Tecla_amarelo", "Tecla_cinza", "Tecla_marrom", "Tecla_ENTER");
            yield return new WaitForSeconds(2.2f);
            Check(safe.IsSolved, "Vermelho-Azul-Amarelo-Cinza-Marrom abre o cofre de simbolos");
        }

        private static IEnumerator Press(KeypadPanel keypad, params string[] keys)
        {
            foreach (string key in keys)
            {
                Transform t = FindDeep(keypad.transform, key);
                if (t == null)
                {
                    Fail("Tecla nao encontrada: " + key);
                    continue;
                }

                HoldButton hold = t.GetComponent<HoldButton>();
                if (hold != null)
                    hold.OnShortPress.Invoke();
                else
                    t.GetComponent<Button>().onClick.Invoke();

                yield return new WaitForSeconds(0.12f);
            }
        }

        // ---------------------------------------------------------------- acoes

        private static IEnumerator Use(string path, Vector3 approachDirection, float distance, string expectedPrompt)
        {
            Transform target = Find(path);
            if (target == null)
            {
                Fail("Objeto nao encontrado: " + path);
                yield break;
            }

            Vector3 aim = AimPoint(target, approachDirection);
            Vector3 flat = new Vector3(approachDirection.x, 0f, approachDirection.z).normalized;

            // Como um jogador: se algo na frente (borda da mesa, frente da gaveta) rouba a mira, chega mais perto.
            float[] approach = { 1f, 0.7f, 0.45f };
            for (int attempt = 0; attempt < approach.Length; attempt++)
            {
                Vector3 stand = aim + flat * (distance * approach[attempt]);
                stand.y = groundedY;
                Teleport(stand);
                // Encosta no movel mais proximo como um jogador faria (nunca fica dentro de colliders).
                PlayerSafePlacement.Resolve(controller, CurrentFloor());
                yield return null;
                // Reaplica a mira a cada frame: o mouse real (eixos legados) pode girar a camera durante a espera.
                for (int frame = 0; frame < 4; frame++)
                {
                    AimAt(aim);
                    yield return null;
                }

                AimAt(aim);
                yield return null;

                if (FocusIsOn(target))
                    break;
            }

            RaycastInteractable focused = interactor.FocusedInteractable;
            string prompt = interactor.CurrentPrompt ?? string.Empty;
            if (focused == null)
            {
                Fail("Sem foco em " + target.name + " (mirando " + aim.ToString("F2") + ")" + DescribeRay());
                yield break;
            }

            if (!string.IsNullOrEmpty(expectedPrompt) &&
                prompt.IndexOf(expectedPrompt, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Fail("Prompt inesperado em " + target.name + ": \"" + prompt + "\" (esperava \"" + expectedPrompt + "\")" + DescribeRay());
            }

            bool performed = interactor.InteractWithFocused();
            report.Append("   - ").Append(target.name).Append(" | prompt \"").Append(prompt).Append("\" | ")
                .Append(performed ? "acao executada" : "bloqueado").Append('\n');
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// O foco esta no interagivel do alvo (o primeiro subindo a partir dele) ou em algo dentro do alvo.
        /// Um recipiente em volta (a gaveta de um papel) nao conta: ai o teste chega mais perto.
        /// </summary>
        private static bool FocusIsOn(Transform target)
        {
            RaycastInteractable focused = interactor.FocusedInteractable;
            if (focused == null)
                return false;

            RaycastInteractable own = target.GetComponentInParent<RaycastInteractable>(true);
            return focused == own || focused.transform.IsChildOf(target);
        }

        private static IEnumerator Equip(string id)
        {
            InventoryItemDefinition item = null;
            foreach (InventoryItemDefinition candidate in inventory.Items)
            {
                if (candidate != null && candidate.Id == id)
                    item = candidate;
            }

            if (item == null)
            {
                Fail("Item nao esta no inventario: " + id);
                yield break;
            }

            inventory.Equip(item);
            yield return new WaitForSeconds(0.35f);
        }

        private static IEnumerator BlinkOnce(int expectedMoment)
        {
            yield return new WaitForSeconds(0.2f);
            bool started = blink.Blink();
            Check(started, "Piscada iniciada");
            while (blink.IsBlinking)
                yield return null;

            yield return new WaitForSeconds(0.2f);
            Check(blink.CurrentMoment == expectedMoment, "Piscada leva ao momento " + (expectedMoment + 1));
            Check(PlayerIsFree(), "Jogador em posicao livre apos a piscada (" + player.transform.position.ToString("F2") + ")");
            Check(!GameplayLocks.IsLocked(GameplayLock.Movement), "Controle devolvido apos a piscada");
        }

        // ---------------------------------------------------------------- utilitarios

        private static Bounds CurrentFloor()
        {
            Transform moment = scenes.GetChild(blink.CurrentMoment);
            Transform floor = moment.Find("Sala_12x12/Piso");
            return floor != null ? floor.GetComponent<Renderer>().bounds : default;
        }

        private static void Teleport(Vector3 position)
        {
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            player.ResetMotion();
            Physics.SyncTransforms();
        }

        private static void AimAt(Vector3 point)
        {
            Transform cam = player.playerCamera.transform;
            Vector3 flat = point - player.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
                player.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            Vector3 toPoint = point - cam.position;
            float horizontal = new Vector2(toPoint.x, toPoint.z).magnitude;
            float pitch = Mathf.Atan2(-toPoint.y, horizontal) * Mathf.Rad2Deg;
            player.rotationX = pitch;
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static string DescribeRay()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return string.Empty;

            StringBuilder text = new(" | raio da camera " + cam.transform.position.ToString("F2") + " dir " + cam.transform.forward.ToString("F2") + " -> ");
            RaycastHit[] hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 3f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length && i < 4; i++)
                text.Append(hits[i].collider.name).Append('@').Append(hits[i].distance.ToString("F2")).Append(' ');
            return text.ToString();
        }

        /// <summary>Mira na face do objeto voltada para o jogador (como alguem clicaria na frente da gaveta).</summary>
        private static Vector3 AimPoint(Transform target, Vector3 approachDirection)
        {
            Bounds bounds;
            Collider collider = target.GetComponent<Collider>();
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                renderer = target.GetComponentInChildren<Renderer>();

            if (renderer != null)
                bounds = renderer.bounds;
            else if (collider != null && collider.enabled)
                bounds = collider.bounds;
            else
                return target.position;

            // Itens pequenos: mira no centro (a caixa envolvente de um objeto diagonal tem cantos vazios).
            if (target.GetComponent<InventoryPickup>() != null)
                return bounds.center;

            Vector3 direction = approachDirection.normalized;
            Vector3 face = bounds.center + Vector3.Scale(direction, bounds.extents) * 0.8f;
            // Um pouco acima do centro da face: o jogador olha de cima para baixo.
            return face + Vector3.up * Mathf.Min(0.05f, bounds.extents.y * 0.3f);
        }

        private static bool PlayerIsFree()
        {
            float radius = controller.radius - 0.05f;
            Vector3 center = player.transform.position + controller.center;
            float half = controller.height * 0.5f - controller.radius;
            foreach (Collider hit in Physics.OverlapCapsule(center + Vector3.down * (half - 0.1f), center + Vector3.up * half, radius,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit != controller && !hit.transform.IsChildOf(player.transform))
                {
                    report.Append("      sobreposto a: ").Append(hit.name).Append('\n');
                    return false;
                }
            }

            return true;
        }

        private static bool Has(string id)
        {
            foreach (InventoryItemDefinition item in inventory.Items)
            {
                if (item != null && item.Id == id)
                    return true;
            }

            return false;
        }

        private static int CountPickups(string id)
        {
            int count = 0;
            foreach (InventoryPickup pickup in Object.FindObjectsByType<InventoryPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup.Item != null && pickup.Item.Id == id)
                    count++;
            }

            return count;
        }

        private static Transform Find(string path)
        {
            return scenes.Find(path);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void OnThought(string thought, InteractionOutcome outcome)
        {
            thoughts.Add(thought);
            report.Append("      pensamento (").Append(outcome).Append("): ").Append(thought).Append('\n');
        }

        private static bool LastThoughtContains(string text)
        {
            return thoughts.Count > 0 &&
                   thoughts[thoughts.Count - 1].IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Check(bool condition, string description)
        {
            if (condition)
                report.Append("PASS ").Append(description).Append('\n');
            else
                Fail(description);
        }

        private static void Fail(string description)
        {
            Failures++;
            report.Append("FAIL ").Append(description).Append('\n');
        }
    }
}
