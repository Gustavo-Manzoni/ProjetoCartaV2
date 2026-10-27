# Sistema de Diálogo em JSON — Jogo do Juiz

## Arquivos

- `CaseData.cs` — classes que representam a estrutura do JSON.
- `DialogueManager.cs` — controla todo o fluxo: intro → opções → resposta → julgamento.
- `GameManager.cs` — exemplo de quem recebe o veredito escolhido pelo jogador.
- `case_001.json` — um caso de exemplo, pronto pra testar.

## Como editar ou adicionar diálogos

Você **nunca precisa mexer em código** pra isso. Basta editar o `.json`:

- `introLines`: lista de frases mostradas em sequência no início (o texto de contexto do caso).
- `options`: lista de perguntas. Cada uma tem:
  - `id`: identificador único (não aparece pro jogador, só é usado internamente).
  - `label`: texto do botão.
  - `response`: o que aparece quando o jogador clica.
- `verdictText`: texto mostrado quando o jogador clica em "Julgar o caso".
- `verdictOptions`: rótulos dos botões finais.

Pra adicionar uma nova pergunta, basta copiar um bloco dentro de `options` e mudar `id`, `label` e `response`. Pra criar um caso novo, duplique o `case_001.json`, mude os textos e o `caseId`, e arraste o novo arquivo no campo `Case Json File` do `DialogueManager` (ou carregue via código com `dialogueManager.LoadCase(novoJson)`).

## Montagem da cena no Unity

1. Importe o **TextMeshPro** (Window > TextMeshPro > Import TMP Essential Resources), caso ainda não tenha.
2. Crie um Canvas com:
   - Um `TMP_Text` para o texto principal → arraste no campo `Dialogue Text`.
   - Um objeto vazio (ex: `OptionsContainer`) com um `Vertical Layout Group` → arraste no campo `Options Container`.
   - Um painel (`VerdictChoicePanel`) com dois botões (`CondemnButton`, `AbsolveButton`), desativado por padrão → arraste tudo nos campos correspondentes.
3. Crie um **prefab de botão de opção** (`OptionButtonPrefab`):
   - Um `Button` com um `TMP_Text` filho (que recebe o `label`).
   - Salve como prefab e arraste no campo `Option Button Prefab`.
4. Crie um objeto vazio `DialogueManager`, adicione o script `DialogueManager.cs` e preencha todos os campos no Inspector (incluindo o `case_001.json` em `Case Json File`).
5. Crie um objeto `GameManager` com o script `GameManager.cs` e arraste-o no campo `Game Manager` do `DialogueManager`.
6. Dê Play. As frases de intro aparecem (efeito de máquina de escrever); clique na tela ou num botão "Continuar" vinculado a `OnIntroAdvance()` pra avançar.

## Fluxo em resumo

1. **Intro**: `introLines` aparecem uma a uma.
2. **Opções**: um botão por pergunta em `options`.
3. **Resposta**: ao clicar, o texto da resposta aparece; um botão "Continuar" volta pra lista de opções.
4. Quando **todas** as opções já foram vistas, um botão extra **"Julgar o caso"** aparece na lista.
5. **Julgamento**: mostra `verdictText`, depois os botões `Condenar` / `Absolver`.
6. Ao escolher, `DialogueManager` chama `gameManager.OnCaseJudged(caseId, condenado)` — é aí que você decide o que fazer com a decisão do jogador (salvar, carregar próximo caso, etc).

## Possíveis extensões

- Guardar as respostas já vistas junto com o save do jogo, pra retomar um caso de onde parou.
- Adicionar um veredito "correto" no JSON (`correctVerdict: true/false`) pra dar feedback ou pontuação ao jogador.
- Trocar `JsonUtility` por `Newtonsoft.Json` se quiser JSON com estruturas mais flexíveis (ex: opções condicionais que só aparecem depois de outra ter sido vista).
