using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;    
// using AYellowPaper.SerializedCollections;
using UnityEngine.Events;
using System;
using UnityEngine.UI;
using Unity.Cinemachine;
public enum ButtonTypes { Normal, BoardActive }

public enum MainMenuButtonType { Play, Options, Leave, Back, Title }
[Serializable]
public class ButtonAnimationSettings
{
    [SerializeField] float time;
    [SerializeField] Ease easeType;

    public float Time { get => time; set => time = value; }
    public Ease EaseType { get => easeType; set => easeType = value; }
}
[Serializable]
public class ButtonSetup
{
    public MainMenuButtonType type;
    [SerializeField] ButtonTypes buttonState;
    [SerializeField] RectTransform buttonTransform;
    [SerializeField] RectTransform buttonFinalPos;
    Button myButton;
    Vector2 inicialPos;

    public Button MyButton => myButton;
    public Vector2 InicialPos => inicialPos;
    public ButtonTypes ButtonState => buttonState;
    public RectTransform ButtonTransform { get => ButtonTransform1; set => ButtonTransform1 = value; }
    public RectTransform ButtonTransform1 { get => buttonTransform; set => buttonTransform = value; }

    public void Init(MainMenuManager manager)
    {
        inicialPos = ButtonTransform.anchoredPosition; 
        myButton = ButtonTransform.gameObject.AddComponent<Button>();
      
    }
    public void MoveToFinalPos(ButtonAnimationSettings animationSettings) 
    {
        buttonTransform.DOAnchorPos(buttonFinalPos.anchoredPosition, animationSettings.Time).SetEase(animationSettings.EaseType);
    }
    public void MoveToIncicialPos(ButtonAnimationSettings animationSettings)
    {
        buttonTransform.DOAnchorPos(inicialPos, animationSettings.Time).SetEase(animationSettings.EaseType);
    }
}
public class MainMenuManager : MonoBehaviour
{
    [SerializeField] ButtonAnimationSettings buttonAnimationSettings;
    [SerializeField] float delayForEachButton = 0.1f;
    Coroutine moveCoroutine;
    [SerializeField] CameraTilt normalCameraTilt, boardCameraTilt, optionsCameraTilt, creditsCameraTilt, audioCameraTilt, languageCameraTilt, graphicsCameraTilt;   
    CinemachineCamera lastCamera;
    [SerializeField] ButtonSetup[] mainMenuButtons;
    CameraTiltManager cameraTiltManager;
    IMenuState currentState;
    IMenuState lastState;

    BoardMenuState boardState;
    NormalMenuState normalState;
    PhotoMenuState photoState;
    OptionsMenuState optionsState;
    CreditsMenuState creditState;
    AudioMenuState audioState;
    LanguageMenuState languageState;
    GraphicsMenuState graphicsState;
    CaseLaunchMenuState launchState;

    public CaseLaunchMenuState LaunchState => launchState;
    public CinemachineCamera LastCamera { get => lastCamera; set => lastCamera = value; }
    public PhotoMenuState PhotoState { get => photoState; set => photoState = value; }
    public NormalMenuState NormalState { get => normalState; set => normalState = value; }
    public BoardMenuState BoardState { get => boardState; set => boardState = value; }
    public OptionsMenuState OptionsState { get => optionsState; set => optionsState = value; }
    public CreditsMenuState CreditState { get => creditState; set => creditState = value; }
    public AudioMenuState AudioState { get => audioState; set => audioState = value; }
    public LanguageMenuState LanguageState { get => languageState; set => languageState = value; }
    public GraphicsMenuState GraphicsState { get => graphicsState; set => graphicsState = value; }

    public IMenuState LastState { get => lastState; set => lastState = value; }
    public IMenuState CurrentState { get => currentState; set => currentState = value; }

    private void Awake()
    {
        ServiceLocator.Register(this);
        foreach (var button in mainMenuButtons)
        {
            button.Init(this);
        }
        NormalState = new NormalMenuState(this);
        BoardState = new BoardMenuState(this);
        PhotoState = new PhotoMenuState(this);
        optionsState = new OptionsMenuState(this);
        creditState = new CreditsMenuState(this);
        audioState = new AudioMenuState(this);
        graphicsState = new GraphicsMenuState(this);
        languageState = new LanguageMenuState(this);
        launchState = new CaseLaunchMenuState();


    }
    private void Start()
    {
        cameraTiltManager = ServiceLocator.Get<CameraTiltManager>();
        ChangeCurrentState(NormalState);

        GameManager.OnLevelStarted += ChangeStateToOnPhoto;
    }
    private void OnDestroy()
    {
        // Evento estatico: sem isso, voltar ao menu deixava handlers apontando para o menu destruido.
        GameManager.OnLevelStarted -= ChangeStateToOnPhoto;
    }
    public void ChangeCurrentState(IMenuState state)
    {
        lastState = CurrentState;
        CurrentState?.Exit();
        CurrentState = state;
        CurrentState?.Enter();

    }
    public void HideBackButton()
    {
        StartCoroutine(MoveBoardButtonsBackWithDelay());
        
    }
    public void SetLastCamera(CinemachineCamera camera)
    {
        LastCamera = camera;

    }
    void Update()
    {
        CurrentState?.Update();
    }
    void ChangeStateToOnPhoto()
    {
        ChangeCurrentState(PhotoState);
    }

    public void ChangeButtonsState(bool to)
    {
        foreach(var btn in mainMenuButtons)
        {
            if (btn.MyButton == null) continue;

            btn.MyButton.interactable = btn.ButtonState == ButtonTypes.Normal ? to : !to;
        }
    }
    ButtonSetup GetEspecificButton(MainMenuButtonType type) 
    {
        foreach (var btn in mainMenuButtons)
        {
            if (btn.type.Equals(type))
                return btn;
        }
        return null;
    }
    public void MoveBackButtonBack() 
    {
        GetEspecificButton(MainMenuButtonType.Back).MoveToIncicialPos(buttonAnimationSettings);
        
    }
    public void OnPlayStart()
    {
        if(moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(MoveButtonsWithDelay());

      
        ChangeCurrentState(BoardState);
    }
    IEnumerator MoveButtonsWithDelay()
    {
        foreach(var btn in mainMenuButtons)
        {
            btn.MoveToFinalPos(buttonAnimationSettings);
            yield return new WaitForSeconds(delayForEachButton);
        }
    }

    public void OnBack()
    {
        CurrentState?.Back();
    }
    public void MoveButtonsBackWithDelay()
    {
        if (moveCoroutine != null) 
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(MoveButtonsBackWithDelayCoroutine());

    }
                
    IEnumerator MoveButtonsBackWithDelayCoroutine()
    {
        foreach (var btn in mainMenuButtons)
        {
            btn.MoveToIncicialPos(buttonAnimationSettings);
            yield return new WaitForSeconds(delayForEachButton);
        }
    }
    IEnumerator MoveBoardButtonsBackWithDelay()
    {
       
        foreach (var btn in mainMenuButtons)
        {
            if (btn.ButtonState == ButtonTypes.BoardActive)
                btn.MoveToIncicialPos(buttonAnimationSettings);
            yield return new WaitForSeconds(delayForEachButton);
        }
    }

    public void OnQuit()
    {
      
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
     public void OnOptions()
    {
       if(moveCoroutine != null)
       StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveButtonsWithDelay());
     
        ChangeCurrentState(OptionsState);
    }
    public void GoToNormalCamera()
    {
         cameraTiltManager.ChangeCamera(normalCameraTilt);
       
    }
    public void GoToBoardCamera()
    { 
        cameraTiltManager.ChangeCamera(boardCameraTilt);
       
    }
     public void GoToOptionsCamera()
    { 
        cameraTiltManager.ChangeCamera(optionsCameraTilt);
        
    }
    /// <summary>Quadro unico de opcoes: a camera do antigo quadro de Som, onde fica a ficha do MenuOptionsBoards.</summary>
    public void GoToOptionsBoardCamera()
    {
        cameraTiltManager.ChangeCamera(audioCameraTilt);
    }
    public void GoToAudioCamera()
    {
        cameraTiltManager.ChangeCamera(audioCameraTilt);
       
    }
    public void GoToGraphicsCamera()
    {
        cameraTiltManager.ChangeCamera(graphicsCameraTilt);
    
    }
    public void GoToLanguageCamera()
    {
        cameraTiltManager.ChangeCamera(languageCameraTilt);
        
    }
    public void GoToCreditsCamera()
    {
        if (creditsCameraTilt == null)
        {
            Debug.LogWarning("MainMenuManager: camera de creditos nao configurada.");
            return;
        }
        cameraTiltManager.ChangeCamera(creditsCameraTilt);

    }

}
