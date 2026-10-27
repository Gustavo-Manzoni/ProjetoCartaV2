using UnityEngine;
using AYellowpaper.SerializedCollections;
using Unity.Cinemachine;
using System;
public enum Levels
{   
     Level1, Level2, Level3, Level4
}
[System.Serializable]
public class MapStats 
{
    [SerializeField] Levels mapLevel;
    [SerializeField] LevelStats level;
    [SerializeField] PhotoStats photo;

    public void ConfigureViewportCamera()
    {
        photo.viewport.SceneCamera = level.levelCamera;
    }

    public Levels MapLevel { get => mapLevel; set => mapLevel = value; }

    public CinemachineCamera StartLevel()
    {
        level.levelObj.SetActive(true);
        photo.levelCamera.Prioritize();
        level.levelCameraAnimator.AnimateFov();

        return photo.levelCamera;

    }
}
public class GameManager : MonoBehaviour
{
    [SerializeField] MapStats[] levels;
    CameraFovManager _cameraFovManager;
    MainMenuManager  _mainMenuManager;
    TooltipManager _tooltipManager;
    public static Action OnLevelStarted;
public void OnCaseJudged(string caseId, bool condemned)
    {
        string resultado = condemned ? "CONDENADO" : "ABSOLVIDO";
        Debug.Log($"Caso '{caseId}' julgado: {resultado}");

        // Exemplos do que você pode fazer aqui:
        // - Salvar o resultado numa lista de decisões do jogador
        // - Carregar o próximo caso: dialogueManager.LoadCase(proximoJson)
        // - Atualizar uma "reputação" do juiz dependendo do veredito
        // - Comparar com o veredito "correto" do caso pra dar feedback
    }
    public void StartLevel(Levels level)
    {
        foreach(var obj in levels)
        {
            if(obj.MapLevel == level)
            {
                OnLevelStarted?.Invoke();

                CinemachineCamera levelCam = obj.StartLevel();
                _mainMenuManager.SetLastCamera(levelCam);
                _mainMenuManager.HideBackButton();

                _cameraFovManager.ChangeZoom(CameraZoomType.Default);
                _tooltipManager.HideTooltip();
                 
            }
        }
        
    }

    void Awake()
    {
        ServiceLocator.Register(this);
        foreach (var obj in levels) 
        {
            obj.ConfigureViewportCamera();
        
        }
    }
    private void Start()
    {
        _cameraFovManager = ServiceLocator.Get<CameraFovManager>();
        _mainMenuManager = ServiceLocator.Get<MainMenuManager>();
        _tooltipManager = ServiceLocator.Get<TooltipManager>();
    }

}
