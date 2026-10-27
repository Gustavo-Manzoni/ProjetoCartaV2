# Ciclo de jogo — Piscada Fatal (`MainMenu` → `1`)

Documento técnico do fluxo completo, do menu ao final. Atualizado em 2026-10-12 a partir do estado do repositório (commit "Ultimas alteracoes" + correções desta data) e da leitura do YAML das cenas. A versão anterior deste arquivo era a auditoria feita **antes** da refatoração (P4, Breu, manivela, cofres com o mesmo código, painel sem conclusão); ela continua no histórico do git.

O fluxo foi validado no Editor via Unity MCP com o teste automático (`Projeto Carta > Piscada Fatal > Teste automatico do fluxo`). As correções feitas depois disso, sem o Editor, estão marcadas como **(validar no Editor)**.

---

## 1. Fluxo entre as cenas

```
Build: MainMenu (0) ─► 1 (1)

MainMenu ─(Jogar)─► quadro do caso ─(foto ou "ABRIR O CASO")─► zoom ─► lâmpada pisca e queima ─► fade ─► cena 1 (P1)
   ▲                                                                                                    │
   ├──────────────────────────── pausa ▸ MENU PRINCIPAL ◄───────────────────────────────────────────────┤
   └──────────────────────────── final ▸ VOLTAR AO MENU ◄───────────────────────────────────────────────┘
```

- `CaseFileLauncher` (na foto do caso) troca o menu para o estado `CaseLaunchMenuState`, que ignora Voltar, Esc e as outras fotos durante a transição. Ele usa o mesmo zoom de câmera das fotos antigas, faz a lâmpada do diorama falhar de forma irregular, queimar e apagar, escurece a tela (`NoirAtmosphere.FadeOut`) e carrega a cena `1`. Se o fade não concluir em 2,5 s, carrega a cena mesmo assim.
- Voltar ao menu (pausa ou final) faz fade, solta o cursor (`GameplayLocks.DisableCursorManagement`) e carrega `MainMenu`. Os singletons do menu (`SettingsManager`, `LocalizationManager`, `SoundManager`) destroem a cópia duplicada, e os eventos estáticos são desassinados no `OnDestroy`/`OnDisable`.
- As opções (volume, VSync, modo de tela, idioma) ficam em `PlayerPrefs` por `GameSettings` e valem nas duas cenas. O volume da pausa e o do menu usam a mesma chave.

## 2. Menu principal

- O cenário de escritório de detetive é montado em runtime por `NoirMenuOffice`, sobre a geometria de protótipo: papel de parede, lambri, cortiça, bilhetes com fio vermelho, luz de persiana e abajur, mesa em primeiro plano e o diorama 3D da foto do caso, com a lâmpada que queima.
- Estados do menu (`MainMenuManager`): `Normal`, `Board`, `Options`, `Audio`, `Graphics`, `Language`, `Credits` e `CaseLaunch`. A câmera de créditos (`CreditsCamera`) está ligada.
- Fotos de opções com arte própria (`Menu/Arte/Foto_*`, geradas por `Projeto Carta > Menu > Gerar fotos das opcoes`).
- A mini-fase `FirstRoom` não é mais aberta pelo Jogar. Ela continua na cena porque contém as câmeras de áudio, gráficos e idioma.
- **Idioma:** foto Idioma → botões BR/EN. Os botões chamam o `LocalizationManager` (os 5 botões com `LocalizedText`), que dispara `Loc.Changed`. O `LocSceneTexts` troca ao vivo os outros textos do menu: foto e descrição do caso, rótulos das fotos, créditos, `Música` e opções do dropdown.
  - Ao voltar ao menu, os botões apontavam para o `LocalizationManager` duplicado, que é destruído no `Awake`, e paravam de funcionar. O `LocSceneTexts` os religa em runtime ao `LocalizationManager` vivo **(validar no Editor)**.
- **Modo de tela (Gráficos):** as opções do dropdown aparecem como "Tela cheia", "Tela cheia exclusiva" e "Janela maximizada" (EN: Fullscreen, Exclusive fullscreen, Maximized window). O `SettingsManager.SetScreeMode` aplicava o modo da opção anterior (índice deslocado) e agora aplica o da opção escolhida **(validar no Editor)**.
- **Inclinação da câmera** (`CameraTilt`): lê `Mouse.current.position`. Até o primeiro movimento do mouse usa o centro da tela, para o menu não abrir inclinado.

## 3. Cena `1`

### 3.1 Controles

| Entrada | Ação |
|---|---|
| WASD / mouse | Andar / olhar |
| Shift / Ctrl / Espaço | Correr / agachar / pular |
| Botão direito | Zoom |
| Clique esquerdo | Interagir com o que está na mira (até 3 m) |
| **Tab** (segurar) | Inventário radial; soltar equipa o item sob o mouse |
| **B** | Piscar: P1 → P2 → P3 → P1… |
| **Esc** | Pausa; dentro de keypad ou documento, sai dele |

### 3.2 Estrutura

```
----PLAYER----            FirstPersonController, InventoryController, PlayerRaycastInteractor
EquipmentRIg > ItemMount  item equipado
PiscadaFatal_Cenarios     BlinkSceneController (moments = CENA_1, CENA_2, CENA_3; startingMoment = 0)
  CENA_1_SENTENCA         ativo na cena salva
  CENA_2_ULTIMA_NOITE     inativo; contém Final_Simbolico (PiscadaFinale)
  CENA_3_PERDAO           inativo
```

Cada momento tem a sala 12×12 (`Sala_12x12`) e uma grade 3×3 de pontos (`Pontos_Fixos_3x3/P1..P9`) onde ficam os objetos. Os nomes `P1..P9` são **posições na sala**, não momentos.

### 3.3 Piscada (`BlinkSceneController` + `PlayerSafePlacement`)

1. Trava o jogador (`GameplayLock.Cutscene`).
2. As pálpebras descem e a tela fica preta.
3. Ativa o próximo momento.
4. Se a posição atual estiver ocupada na nova sala, `PlayerSafePlacement` procura em anéis a posição livre mais próxima dentro do piso, sem subir em móveis.
5. As pálpebras sobem e o controle volta.

O estado dos momentos persiste, porque eles só são ativados e desativados. O jogo sempre começa em `startingMoment` (P1), mesmo que outro momento tenha ficado ativo na cena salva. A piscada não funciona durante pausa, keypad, documento, final, abertura de cofre ou alavanca da cadeira.

## 4. Cadeia de puzzles

```
P1  Gaveta da mesa ───────────────► Abridor de cartas
P1  Envelope inicial + abridor ───► Chave prateada              (abridor não é consumido)
P1  Baú prateado + chave prateada ► Ímã + Chave amarela          (chave consumida)
P1  Baú amarelo + chave amarela ──► Maçã                         (chave consumida)
P3  Armário baixo (sem chave) ────► Balde (gaveta de cima) + Pé de cabra (gaveta de baixo)
P3  Bandeja de protocolo (3 documentos em ordem cronológica) ─► gaveta do arquivo ─► Pasta com arquivos + relatório de manutenção
P3  Trava de 3 discos (esquema do relatório) ─► compartimento de manutenção ─► Manivela
P3  Caixa metálica + pé de cabra ─► Olho de vidro
P2  Relógio de parede (examinar) ─► pista 23:58
P3  Cofre numérico 2358 ──────────► Caneta-tinteiro
P2  Tampa metálica + pé de cabra ─► Esquema das 5 posições (documento legível)
P2  Poço: balde no gancho + manivela no eixo ─► + pasta ─► girar ─► Envelope lacrado (a pasta fica no fundo)
P1  Mesa: envelope lacrado + abridor ─► Ficha de telefone + bilhete de Helena
P2  Poço ─► + ímã ─► girar ─► Relógio de bolso grudado no ímã (o ímã volta ao inventário)
P1  Painel do ritual: caneta, maçã, relógio, ficha, olho ─► papel com a senha de símbolos
P1  Cofre de símbolos: Vermelho → Azul → Amarelo → Cinza → Marrom ─► Chave da câmara
P2  Caixa de controle + chave da câmara ─► Alavanca ─► cadeira desliga, porta destrava
P2  Porta da câmara ─► final
```

Cada um dos 15 itens do fluxo tem **exatamente um pickup** na cena: abridor, chave prateada, ímã, chave amarela, maçã, balde, pé de cabra, pasta, olho, caneta, envelope lacrado, ficha, relógio de bolso, chave da câmara e manivela. Nenhum item é consumido antes do seu último uso: o abridor serve aos dois envelopes, o pé de cabra à tampa e à caixa, a pasta fica no fundo do poço, a manivela passa a fazer parte do guincho e o ímã volta ao inventário depois de trazer o relógio.

### 4.1 P1 — A Sentença (`CENA_1_SENTENCA`)

| Ponto | Objeto | Componente | Detalhe |
|---|---|---|---|
| P5 | `P5_Mesa_Escritorio/Gavetas/handle2_lp.003` | `DrawerInteractable` | Contém o `Abridor_de_Cartas`. As outras 5 gavetas abrem vazias. |
| P5 | `Envelope_Inicial` | `RequiredEquippedItemInteractable` (abridor) | Trigger `AppearKey` (`ChavePrataAparecer.controller`) abre a aba e ativa `Ancora_Chave_Prateada`. |
| P5 | `Mesa_Ponto_Envelope` | `SealedEnvelopeSpot` | Coloca o envelope lacrado na mesa, abre com o abridor e a `Ficha_Telefone` desliza para fora. |
| P1 | `Bau_Prateado` | `AnimatorOpenable` (chave prateada) | Estado `OpenUpperArchive`; conteúdo: `Ima`, `Chave_Amarela`. |
| P1 | `Bau_Amarelo` | `AnimatorOpenable` (chave amarela) | Estado `OpenLowerArchive`; conteúdo: `Maca`. |
| P3 | `Painel_do_Ritual` | `RitualPanel` + 5 `InventoryItemPlacement` | Mostra "Painel: n de 5". Completo, revela `Papel_Senha_Simbolos` (`ReadableDocument`) com a `Luz_Revelacao`. |
| P8 | `Cofre_Simbolos` | `SafeInteractable` + `SymbolKeypad` | Keypad 3×2: cinco símbolos coloridos e Enter no canto inferior direito, sem Apagar. Telinha com uma bolinha por símbolo; cada botão mostra a ordem escolhida. Código em `PiscadaFatal/Simbolos/CodigoCofreSimbolos.asset`. Contém a `Chave_Camara`. |

### 4.2 P2 — A Última Noite (`CENA_2_ULTIMA_NOITE`)

| Ponto | Objeto | Componente | Detalhe |
|---|---|---|---|
| P5 | `P5_Poco` | `WellController` + `WellRope` + 2 `WellPart` | Estados `NoBucket → Empty → Baited → Running → ResultReady`. O balde só prende no `Gancho_Balde` e a manivela só encaixa em `Poço/crank/Encaixe_Manivela` (ponta -X do eixo); sem manivela o balde não desce ("Guincho sem manivela"). A manivela encaixada é filha do tambor e gira com ele, junto com a corda e o balde. Pasta: fica em `Fundo_do_Poco` e o envelope sobe com o clipe vermelho. Ímã: sobe com o relógio grudado e volta ao inventário quando o relógio é pego. Cada isca funciona uma vez. Piscar no meio da descida conclui o ciclo. |
| P5 | `P5_Poco/Placa_Guincho` | `ReadableDocument` (só imagem) | Placa de pictogramas no poste direito: balde no gancho e manivela no eixo; pasta com clipe → envelope; ímã → relógio. |
| P6 | `Livro_de_Plantao` | `ReadableDocument` | Prancheta na parede direita: a noite de 20/11 minuto a minuto (relógio no poço, recado jogado no poço, telefone sem resposta, 23:58, execução, indulto às 00:21). |
| P1 | `Tampa_Metalica` | `TransformOpenable` (pé de cabra, não consome) | Revela `Esquema_Estrela_5_Posicoes` (`ReadableDocument`). |
| P3 | `Relogio` | `InspectableObject` | Ponteiros em 23:58; Tomás comenta. |
| P8 | `Caixa_Controle_Cadeira` | `TransformOpenable` (chave da câmara, consome) | Conteúdo: `Alavanca` (`ChairPowerSwitch`), que apaga as luzes da cadeira, para o zumbido e destrava a porta. |
| P8 | `P8_Cadeira_Eletrica` | `InspectableObject` | Comentário muda depois de desligada (`UseAlternateThought`). |
| P6 | `Porta_da_Camara` | `FinalDoor` | Trancada até a cadeira desligar. Com a chave na mão: "A chave não entra aqui…". |
| — | `Final_Simbolico` | `PiscadaFinale` | Ver seção 6. |

### 4.3 P3 — O Perdão (`CENA_3_PERDAO`)

| Ponto | Objeto | Componente | Detalhe |
|---|---|---|---|
| P5 | `Armario_Baixo/Gaveta_Superior` | `AnimatorOpenable` (sem chave) | Contém o `Balde`. |
| P5 | `Armario_Baixo/Gaveta_Inferior` | `AnimatorOpenable` (sem chave) | Contém o `Pe_de_Cabra`. |
| P3 | `Arquivo_Protocolo` | `ProtocolTray` + 2 `ProtocolSwapHandle` | Bandeja na mesa com três documentos (`ReadableDocument`): começa Despacho, Sentença, Pedido. As alavancas trocam vizinhos. Na ordem Sentença (14/03/1958) → Pedido (02/10/1958) → Despacho (20/11/1958 23:40), destrava a gaveta. Pista: `Protocolo_Regra`, em enigma ("Primeiro a culpa. Depois a súplica. Por fim, a graça."): culpa = sentença, súplica = pedido de indulto, graça = despacho; a ordem coincide com as datas dos papéis. |
| P3 | `Desk_Animated/Desk/Drawer_L_Big` | `TransformOpenable` (trancada por puzzle) | Gaveta do arquivo (o osso da mesa desliza 0,6 m). Contém `Pasta_Arquivos` e `Relatorio_Manutencao` (anexo, com o esquema dos discos). O tampo ganhou `Colisor_Tampo_Mesa`. |
| P3 | `Desk_Animated/Desk/Drawer_R_Upper` e `Drawer_R_Lower` | `TransformOpenable` (sem chave) | Gavetas da direita da mesa do cofre: abrem e fecham ao clicar (0,45 m), vazias. |
| P1 | `Compartimento_Manutencao` | `DialLockMechanism` + 3 `RotaryDial` + `TransformOpenable` | Na lateral do armário alto. Cada clique gira um disco um dente (8 posições, horário). Solução 2, 5, 7 (marca vermelha em 3 h, 7 h 30 e 10 h 30), igual ao esquema do relatório. Abre a `Portinhola` e libera a `Manivela`. |
| P3 | `Cofre_Numerico` | `SafeInteractable` + `NumericKeypad` | Código `2358`. Layout 1–9 em 3×3; última linha Apagar (toque apaga o último dígito, segurar limpa tudo), 0 e Enter. Aceita o teclado físico. Não aceita um 5º dígito (o painel dá um tranco). Contém a `Caneta_Tinteiro`. |
| P7 | `Caixa_Metalica_Parede` | `TransformOpenable` (pé de cabra, não consome) | Contém o `Olho_de_Vidro`. |
| P1 | `P1_Armario_Alto` | 2 `TransformOpenable` | Cada porta abre e fecha sozinha (`Dobradica_Porta_Direita`/`Esquerda`, 100° para fora). Dentro, só prateleiras vazias. |

## 4.4 Documentos e história (2026-10-20)

Helena Duarte, advogada de Tomás Ferraz, consegue o indulto às 23:40; a ordem chega à penitenciária às 00:21, depois da execução da meia-noite. Os textos ficam em `ReadableDocument.body` (traduzidos pelo `LocTable`); no mundo, os papéis mostram só título e data.

| Momento | Documento | O que conta |
|---|---|---|
| P1 | `Anotacao_Ritual` (parede, à direita do painel) | Os cinco objetos do painel: a caneta que assinou, o olho que jurou ter visto, a última refeição, o relógio das 23:58, a ficha da ligação sem resposta. O que o painel esconde abre o cofre do juiz. |
| P1 | `Bilhete_Helena` (sai do envelope do poço) | Indulto assinado às 23:40, ponte fechada pela chuva, a ficha para o telefone da guarita. "Ainda dá tempo." |
| P2 | `Placa_Guincho` | Pictogramas do poço (sem texto). |
| P2 | `Livro_de_Plantao` | Relógio caiu no poço, chave da câmara no cofre do juiz, recado jogado no poço, 23:58, execução, mensageiro às 00:21. |
| P3 | `Doc_Sentenca`, `Doc_Pedido_Indulto`, `Doc_Despacho_Indulto` | Condenação com uma testemunha (olho de vidro), pedido de Helena, despacho aprovado. |
| P3 | `Protocolo_Regra` | Regra de arquivamento (pista da bandeja). |
| P3 | `Relatorio_Manutencao` | Por que a manivela saiu do guincho, onde está e o esquema dos discos; papéis e um objeto de metal caíram no poço. |

A arte do esquema e da placa é gerada por **Projeto Carta > Piscada Fatal > Gerar arte dos documentos do caso** (`Editor/PiscadaFatal/CaseDocumentArtTools.cs`), que lê a solução do `DialLockMechanism` da cena. Mudou a solução: rode o menu de novo; o `ProjectDataTests` acusa esquema desatualizado.

## 5. Sistemas transversais

- **Pensamentos de Tomás** (`TomasThoughts` → `GameplayHud`): um clique que não executa nada em objeto interativo mostra uma frase específica do motivo: item ausente, item errado, pré-requisito ou nada a fazer. Há cooldown no mesmo objeto. Decoração não recebe foco e não fala. Pickups e conclusões podem dar a próxima pista (`pickupThought`, `solvedThought`...).
- **Cofres** (`SafeInteractable`):
  1. A câmera do cofre (Cinemachine, prioridade 100) aproxima do keypad World Space na porta.
  2. `GameplayLock.Modal` trava movimento, piscada, inventário e pausa e libera o cursor.
  3. A entrada só é aceita depois de 0,45 s.
  4. Esc sai sem resolver.
  5. Um erro limpa a telinha e Tomás diz "A sequência não está certa.".
  6. Um acerto devolve a câmera, abre a porta (trigger `OpenSafe`) com a piscada travada até o fim e libera o conteúdo.
- **HUD** (`GameplayHud`): placa do prompt, mira reativa, avisos NOVO ITEM / USADO, cartão de título a cada momento (I A SENTENÇA, II A ÚLTIMA NOITE, III O PERDÃO), dicas de controle e pensamentos.
- **Pausa** (`PauseMenu`, Esc): Continuar, Opções (volume, tela cheia), Menu principal (com confirmação) e Sair. Abaixa o volume enquanto aberta.
- **Ambiente** (`NoirEnvironment`, `NoirAmbience`): P1 escritório (chuva e tique de relógio), P2 câmara (zumbido da cadeira, que para ao desligar), P3 sala administrativa (lâmpada fluorescente e ventilação).
- **Idioma na gameplay** (`Loc`): o idioma escolhido no menu vale para tudo na cena 1:
  - prompts e pensamentos (traduzidos na exibição);
  - nomes dos itens na roda e nos avisos;
  - cartões dos momentos (THE SENTENCE, THE LAST NIGHT, THE PARDON);
  - dicas, pausa, keypads (`CLEAR`, `OPEN`, `ERROR`), documentos e final ("There's still time.").

  A lógica e o teste do fluxo continuam em PT. Aberta direto pelo Editor, a cena 1 usa o idioma salvo em `PlayerPrefs`.
- **Input** (`FirstPersonController` + `Core/LegacyInputCompat`): Input System com a mesma sensação do legado:
  - mouse a 0,2°/pixel, sem `deltaTime`;
  - WASD/setas sem normalizar a diagonal;
  - Shift corre só para frente, Espaço pula, botão direito dá zoom;
  - o analógico esquerdo de controle ou joystick também anda.

## 6. Final (`PiscadaFinale`)

Disparado uma única vez ao abrir a porta da câmara:

1. Trava o jogador.
2. O som ambiente cai e a porta hesita e abre para a escuridão (`Revelacao_Porta`: `Vazio`, `Luz_Corredor`).
3. Uma silhueta aparece por um instante, com batidas de coração, e some.
4. A tela escurece e surgem as linhas "23:58." / "Alguém chegou antes da hora." / "Ainda dá tempo.".
5. Título PISCADA FATAL — FIM e os botões VOLTAR AO MENU / SAIR DO JOGO (Enter ou Esc voltam ao menu).

A sequência roda no `CoroutineHost` e sempre termina nos botões, então nunca deixa o jogador numa tela preta.

## 7. Correções de 2026-10-12 (validar no Editor)

1. **Softlock no painel do ritual.** `InventoryItemPlacement` marcava o encaixe como preenchido na hora, mas só avisava o painel ao fim da animação de 0,45 s, e essa corrotina rodava no próprio encaixe. Piscar nesse intervalo parava a corrotina: o 5º encaixe nunca avisava, e o papel (e com ele o cofre de símbolos) ficava inacessível. Agora `WorldItemPlacement.MoveTo` roda no `CoroutineHost` (vale também para poço, envelope e `RequiredEquippedItemInteractable`). Se o painel completar com P1 inativo, `RitualPanel` toca a revelação quando P1 volta. A escala original do papel fica guardada para uma revelação interrompida não encolhê-lo.
2. **Porta do cofre reiniciava a abertura.** `SafeInteractable.OpenDoor()` dispara `OpenSafe`, e o `onSolved` dos dois cofres na cena dispara `OpenSafe` de novo 0,45 s depois. A transição `Any State → OpeningSafe` de `SafeOpening.controller` tinha `CanTransitionToSelf` ligado, então a animação recomeçava. Desligado: o segundo trigger agora não tem efeito.

3. **Localização PT/EN completa** (`Core/Loc`, `Core/LocTable`, `NoirUI/LocSceneTexts`): 212 pares PT→EN cobrindo todo texto visível das duas cenas e do código. Inclui a correção dos botões BR/EN depois de voltar ao menu.
4. **Input do build no Input System** (`Core/LegacyInputCompat`, `FirstPersonController`, `CameraTilt`, `PhotoMenuState`), com as fórmulas do `InputManager.asset`.
5. **Dropdown de modo de tela** aplicava o modo da opção anterior. Corrigido em `SettingsManager.SetScreeMode`.
6. **Testes EditMode** em `Assets/Editor/Tests` (lógica, dados do projeto, cobertura da tradução, ponte do input).

Como validar:
- rode os testes EditMode e o teste automático, em PT e em EN;
- abra os dois cofres e observe a porta;
- coloque o último objeto no painel e pisque em seguida, depois volte a P1 e veja o papel surgir;
- jogue em EN do menu ao final;
- troque BR/EN depois de voltar ao menu pela pausa;
- confira a sensibilidade do mouse.

## 8. Fora do fluxo

- Itens sem uso: `isca_improvisada.asset` e `Assets/Items/*`.
- `_backup_antes_P4/1.unity.bak`: cópia da cena antes da refatoração.
- Protótipos `SampleScene`, `DialogoTest` e `FirstRoom`: fora do build. Ver Pendências no `CLAUDE.md`.
