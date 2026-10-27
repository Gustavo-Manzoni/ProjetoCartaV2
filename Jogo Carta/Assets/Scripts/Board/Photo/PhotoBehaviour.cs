using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
using UnityEngine.Events;


public class PhotoBehaviour : MonoBehaviour
{
   
    [Space]
    [SerializeField] Levels _levelToActive;
    [SerializeField] private UnityEvent<Camera> _onPhotoEnter = new UnityEvent<Camera>();
    [SerializeField] private UnityEvent _onPhotoExit = new UnityEvent();
    [SerializeField]
    TooltipStats
                               _stats;
    [SerializeField] bool _isUnlocked;

    #region internal
    MainMenuManager _mainMenuManager;
    GameManager _gameManager;

    CameraFovManager _cameraFovManager;
    TooltipManager _tooltipManager;
    #endregion
    PhotoAnimation animationHandler;
    void Start()
    {
        _mainMenuManager = ServiceLocator.Get<MainMenuManager>();
        _gameManager = ServiceLocator.Get<GameManager>();
        _cameraFovManager = ServiceLocator.Get<CameraFovManager>();
        _tooltipManager = ServiceLocator.Get<TooltipManager>();
        
        animationHandler = GetComponent<PhotoAnimation>();
        
        animationHandler.onPointerEnter.AddListener(OnPointerEnter);
        animationHandler.onPointerExit.AddListener(OnPointerExit);
        animationHandler.onPointerClick.AddListener(OnPointerClick);

    }

    public void OnPointerEnter()
    {
        //_tooltipManager.ShowTooltip(transform.position, _stats);

        _cameraFovManager.ChangeZoom(CameraZoomType.Photo);
      
    }
    public void OnPointerExit()
    {
       // _tooltipManager.HideTooltip();
        _cameraFovManager.ChangeZoom(CameraZoomType.Default);

    }
    public void OnPointerClick()
    {
         if (!_mainMenuManager.CurrentState.CanInteractWithPhotos()) return;
        OnPointerExit();

        _gameManager.StartLevel(_levelToActive);

        _mainMenuManager.HideBackButton();


        //_onPhotoEnter?.Invoke(_photoCamera);
    }

    public void ExitPhoto()
    {
        _onPhotoExit?.Invoke();
    }
    public UnityEvent<Camera> GetOnPhotoEnterEvent() => _onPhotoEnter;
    public UnityEvent GetOnPhotoExitEvent() => _onPhotoExit;
}
