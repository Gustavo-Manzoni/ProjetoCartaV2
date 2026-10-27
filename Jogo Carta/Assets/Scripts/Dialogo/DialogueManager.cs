using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ================================================================
// DialogueManager
// ----------------------------------------------------------------
// Fluxo:
//   1) Intro     -> mostra as introLines uma a uma (efeito de máquina de escrever)
//   2) Opções    -> mostra um botão pra cada CaseOption
//   3) Resposta  -> ao clicar numa opção, escreve a resposta e depois volta pra (2)
//   4) Quando todas as opções já foram vistas, aparece o botão "Julgar o caso"
//   5) Julgamento-> mostra o verdictText e depois os botões Condenar / Absolver
//   6) Ao escolher, chama gameManager.OnCaseJudged(caseId, condenado)
//
// Pra trocar ou adicionar diálogo: edite só o arquivo .json, nada de código.
// ================================================================

public enum DialogueState
{
    Intro,
    ShowingOptions,
    ShowingResponse,
    ShowingVerdictText,
    ShowingVerdictChoice
}

public class DialogueManager : MonoBehaviour
{
    [Header("Dados do caso")]
    [Tooltip("Arraste o arquivo .json do caso aqui")]
    public TextAsset caseJsonFile;

    [Header("Referências de UI")]
    public TMP_Text dialogueText;         // texto principal (intro / resposta / veredito)
    public Transform optionsContainer;    // pai onde os botões de opção são instanciados
    public GameObject optionButtonPrefab; // prefab com componente Button + TMP_Text filho

    [Header("Painel de veredito")]
    public GameObject verdictChoicePanel; // painel com os botões Condenar / Absolver (desativado por padrão)
    public Button condemnButton;
    public Button absolveButton;

    [Header("Configuração")]
    public float typingSpeed = 0.03f;
    [Tooltip("Se true, um clique na tela durante a digitação completa o texto instantaneamente")]
    public bool allowSkipTyping = true;

    [Header("Integração")]
    public GameManager gameManager;

    private CaseData currentCase;
    private DialogueState state;
    private Coroutine typingCoroutine;
    private string fullTextBeingTyped;
    private int introIndex = 0;
    private HashSet<string> answeredOptions = new HashSet<string>();
    private List<GameObject> spawnedButtons = new List<GameObject>();

    void Start()
    {
        if (caseJsonFile != null)
            LoadCase(caseJsonFile);
    }

    void Update()
    {
        // Durante a intro, um clique do mouse (ou toque na tela / barra de
        // espaço) avança para a próxima frase automaticamente.
        // Se preferir controlar isso só por um botão de UI, pode apagar
        // este método e chamar dialogueManager.OnIntroAdvance() a partir
        // do OnClick() do botão no Inspector.
        if (state == DialogueState.Intro)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                OnIntroAdvance();
            }
        }
    }

    // ---------------------------------------------------------------
    // Carregamento
    // ---------------------------------------------------------------

    public void LoadCase(TextAsset json)
    {
        currentCase = JsonUtility.FromJson<CaseData>(json.text);
        answeredOptions.Clear();
        introIndex = 0;

        if (verdictChoicePanel != null)
            verdictChoicePanel.SetActive(false);

        ClearOptionButtons();
        StartIntro();
    }

    // ---------------------------------------------------------------
    // Intro
    // ---------------------------------------------------------------

    void StartIntro()
    {
        state = DialogueState.Intro;
        ClearOptionButtons();
        ShowNextIntroLine();
    }

    void ShowNextIntroLine()
    {
        if (introIndex < currentCase.introLines.Count)
        {
            TypeText(currentCase.introLines[introIndex]);
            introIndex++;
        }
        else
        {
            ShowOptions();
        }
    }

    /// <summary>
    /// Chame isto a partir de um botão "Continuar" ou de um clique na tela
    /// enquanto a intro está sendo exibida.
    /// </summary>
    public void OnIntroAdvance()
    {
        if (state != DialogueState.Intro) return;

        if (IsTyping())
        {
            if (allowSkipTyping) FinishTypingInstantly();
            return;
        }
        ShowNextIntroLine();
    }

    // ---------------------------------------------------------------
    // Opções (perguntas sobre o caso)
    // ---------------------------------------------------------------

    void ShowOptions()
    {
        state = DialogueState.ShowingOptions;
        dialogueText.text = "";
        ClearOptionButtons();

        foreach (var option in currentCase.options)
        {
            CreateOptionButton(option.label, () => OnOptionSelected(option));
        }

        // só libera "Julgar o caso" quando o jogador já viu todas as opções
        if (AllOptionsAnswered())
        {
            CreateOptionButton("Julgar o caso", OnJudgeSelected);
        }
    }

    bool AllOptionsAnswered()
    {
        return answeredOptions.Count >= currentCase.options.Count;
    }

    void OnOptionSelected(CaseOption option)
    {
        state = DialogueState.ShowingResponse;
        answeredOptions.Add(option.id);
        ClearOptionButtons();

        TypeText(option.response);
        CreateOptionButton("Continuar", OnContinueFromResponse);
    }

    void OnContinueFromResponse()
    {
        if (IsTyping())
        {
            if (allowSkipTyping) FinishTypingInstantly();
        }
        else
        {
            ShowOptions();
        }
    }

    // ---------------------------------------------------------------
    // Julgamento
    // ---------------------------------------------------------------

    void OnJudgeSelected()
    {
        state = DialogueState.ShowingVerdictText;
        ClearOptionButtons();
        TypeText(currentCase.verdictText, onComplete: ShowVerdictChoices);
    }

    void ShowVerdictChoices()
    {
        state = DialogueState.ShowingVerdictChoice;
        verdictChoicePanel.SetActive(true);

        condemnButton.GetComponentInChildren<TMP_Text>().text = currentCase.verdictOptions.condemnLabel;
        absolveButton.GetComponentInChildren<TMP_Text>().text = currentCase.verdictOptions.absolveLabel;

        condemnButton.onClick.RemoveAllListeners();
        absolveButton.onClick.RemoveAllListeners();

        condemnButton.onClick.AddListener(() => OnVerdictChosen(true));
        absolveButton.onClick.AddListener(() => OnVerdictChosen(false));
    }

    void OnVerdictChosen(bool condemned)
    {
        verdictChoicePanel.SetActive(false);

        if (gameManager != null)
        {
            gameManager.OnCaseJudged(currentCase.caseId, condemned);
        }
        else
        {
            Debug.LogWarning("DialogueManager: nenhum GameManager atribuído — o veredito não foi repassado a ninguém.");
        }
    }

    // ---------------------------------------------------------------
    // Botões dinâmicos
    // ---------------------------------------------------------------

    void CreateOptionButton(string label, UnityEngine.Events.UnityAction onClick)
    {
        if (optionButtonPrefab == null)
        {
            Debug.LogError("DialogueManager: 'Option Button Prefab' não foi atribuído no Inspector.");
            return;
        }

        GameObject go = Instantiate(optionButtonPrefab, optionsContainer);
        go.SetActive(true); // garante que o clone não venha desativado (caso o prefab original esteja inativo na cena)

        // includeInactive: true evita falha caso o filho de texto esteja desativado no prefab
        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            Debug.LogError("DialogueManager: nenhum componente TMP_Text (TextMeshPro) foi encontrado dentro do 'Option Button Prefab'. Verifique se o prefab tem um objeto de texto TextMeshPro como filho.");
            Destroy(go);
            return;
        }
        text.text = label;

        Button btn = go.GetComponentInChildren<Button>();
        if (btn == null)
        {
            Debug.LogError("DialogueManager: o 'Option Button Prefab' não tem um componente Button na raiz.");
            Destroy(go);
            return;
        }
        btn.onClick.AddListener(onClick);

        spawnedButtons.Add(go);
    }

    void ClearOptionButtons()
    {
        foreach (var go in spawnedButtons)
        {
            Destroy(go);
        }
        spawnedButtons.Clear();
    }

    // ---------------------------------------------------------------
    // Efeito de máquina de escrever
    // ---------------------------------------------------------------

    void TypeText(string text, System.Action onComplete = null)
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        fullTextBeingTyped = text;
        typingCoroutine = StartCoroutine(TypeTextRoutine(text, onComplete));
    }

    IEnumerator TypeTextRoutine(string text, System.Action onComplete)
    {
        dialogueText.text = "";
        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        typingCoroutine = null;
        onComplete?.Invoke();
    }

    bool IsTyping() => typingCoroutine != null;

    void FinishTypingInstantly()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        dialogueText.text = fullTextBeingTyped;
    }
}
