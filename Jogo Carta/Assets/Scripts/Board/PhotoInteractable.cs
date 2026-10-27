using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.Events;

public class PhotoInteractable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] Transform _transformToMove;
    [SerializeField] Vector3   _positionAmountOnHover;
    [SerializeField] float     _onHoverPositionTime;
    [SerializeField] float     _onExitPositionTime;
    [SerializeField] Ease      _onHoverEase, _onExitEase;
    [Space]
    [SerializeField] Vector3   _colliderScaleAmountOnHover;
    [Space]
    [SerializeField] Vector3   _finalRotationZOnHover;
    [SerializeField] float     _rotationTimeOnHover;
    [SerializeField] Ease      _rotationEaseOnHover;
    [Space]
    [SerializeField] Vector3 _cantClickRotationAmount;
    [SerializeField] float _cantClickRotationTime;
    [SerializeField] int _cantClickRotationVibrato;
    [SerializeField] float _cantClickRotationElasticity;
    [SerializeField] Ease _cantClickRotationEase;
    [Space]
    [SerializeField] Vector3  _finalVisualScaleOnHover;
    [SerializeField] float     _scaleTimeOnHover;
    [SerializeField] Ease      _scaleEaseOnHover;
    [Space]
    //[SerializeField] Camera    _photoCamera;
    [Space]
    [SerializeField] Levels    _levelToActive;
    [SerializeField] private UnityEvent<Camera> _onPhotoEnter = new UnityEvent<Camera>();
    [SerializeField] private UnityEvent _onPhotoExit = new UnityEvent();
    [SerializeField] TooltipStats 
                               _stats;
     bool                      _canClick = true;
    [SerializeField] bool      _isUnlocked;
    [SerializeField] string id;
    [SerializeField] GameObject _lockedIndicator;

    #region internal
    Vector3 _starterScale;
    Vector3                    _starterPosition;
    Vector3                    _starterVisualScale;
    Vector3                    _starterRotation;   
    Tweener                    _positionTween, _rotationTween, _scaleTween;
    MainMenuManager            _mainMenuManager;
    GameManager                _gameManager;

    CameraFovManager           _cameraFovManager;
    TooltipManager             _tooltipManager;
  

    #endregion
    void Awake()
    {
        _starterScale = transform.localScale;
        _starterPosition = _transformToMove.position;
        _starterRotation = _transformToMove.localEulerAngles;
        _starterVisualScale = _transformToMove.localScale;


    }
    void Start()
    {
        _mainMenuManager = ServiceLocator.Get<MainMenuManager>();
        _gameManager = ServiceLocator.Get<GameManager>();
        _cameraFovManager = ServiceLocator.Get<CameraFovManager>();
        _tooltipManager =ServiceLocator.Get<TooltipManager>();

    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(!_mainMenuManager.CurrentState.CanInteractWithPhotos())return;
       
        _tooltipManager.ShowTooltip(transform.position, _stats);

        _rotationTween?.Kill();
        
        if (!_canClick) 
        {
            return;
        }

        _cameraFovManager.ChangeZoom(CameraZoomType.Photo);
        #region tween
        _positionTween?.Kill();
        _positionTween = _transformToMove.DOMove(_starterPosition + _positionAmountOnHover, _onHoverPositionTime).SetEase(_onHoverEase);
        
        _rotationTween = _transformToMove.DOLocalRotate(_finalRotationZOnHover + _starterRotation, _rotationTimeOnHover).SetEase(_rotationEaseOnHover);
          
        _scaleTween?.Kill();
        _scaleTween = _transformToMove.DOScale(_finalVisualScaleOnHover + _starterVisualScale, _scaleTimeOnHover).SetEase(_scaleEaseOnHover);

       
        transform.localScale = _starterScale + _colliderScaleAmountOnHover;
        #endregion
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if(!_mainMenuManager.CurrentState.CanInteractWithPhotos())return;
        
        _tooltipManager.HideTooltip();

        if(!_canClick)return;
        #region Tween 
        _positionTween?.Kill();
        _positionTween = _transformToMove.DOMove(_starterPosition, _onExitPositionTime).SetEase(_onExitEase);

        _rotationTween?.Kill();
        _rotationTween = _transformToMove.DOLocalRotate(_starterRotation, _rotationTimeOnHover).SetEase(_rotationEaseOnHover);

        _scaleTween?.Kill();
        _scaleTween = _transformToMove.DOScale(_starterVisualScale, _scaleTimeOnHover).SetEase(_scaleEaseOnHover);

        transform.localScale = _starterScale;
        #endregion
        _cameraFovManager.ChangeZoom(CameraZoomType.Default);

    }
    public void OnPointerClick(PointerEventData eventData)
    {
         if(!_mainMenuManager.CurrentState.CanInteractWithPhotos())return;
        if (!_canClick) return;
      
        if (!_isUnlocked)
        {
            _transformToMove.localEulerAngles = _starterRotation;
            _rotationTween = _transformToMove.DOPunchRotation(_cantClickRotationAmount, _cantClickRotationTime, _cantClickRotationVibrato, _cantClickRotationElasticity);
            return;
        }
      
        
        _canClick = false;

         OnPointerExit(eventData);

        _gameManager.StartLevel(_levelToActive);

        _mainMenuManager.HideBackButton();

        
        //_onPhotoEnter?.Invoke(_photoCamera);
    }
    
    public void ExitPhoto()
    {
        _canClick = true;
        _onPhotoExit?.Invoke();
    }
    public UnityEvent<Camera> GetOnPhotoEnterEvent() => _onPhotoEnter;
    public UnityEvent GetOnPhotoExitEvent() => _onPhotoExit;
   
}
