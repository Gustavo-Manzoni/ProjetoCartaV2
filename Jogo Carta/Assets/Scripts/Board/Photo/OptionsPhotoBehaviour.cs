using UnityEngine;
using UnityEngine.Rendering;
public enum OptionsPhotoType { Audio, Language, Graphics, Credits }
public class OptionsPhotoBehaviour : MonoBehaviour
{
    [SerializeField] OptionsPhotoType optionType;
    PhotoAnimation animationHandler;
    MainMenuManager _manager;
    void Awake()
    {
        animationHandler = GetComponent<PhotoAnimation>();
        animationHandler.onPointerEnter.AddListener(OnPointerEnter);
        animationHandler.onPointerExit.AddListener(OnPointerExit);
        animationHandler.onPointerClick.AddListener(OnPointerClick);
    }
    private void Start()
    {
       _manager = ServiceLocator.Get<MainMenuManager>();
        
    }

    void OnPointerEnter() 
    {
    
    }
    void OnPointerExit()
    {

    }
    void OnPointerClick()
    {
        switch (optionType)
        {
            case OptionsPhotoType.Audio:
                _manager.ChangeCurrentState(_manager.AudioState);
                break;
            case OptionsPhotoType.Language:
                _manager.ChangeCurrentState(_manager.LanguageState);
                break;
            case OptionsPhotoType.Graphics:
                _manager.ChangeCurrentState(_manager.GraphicsState);
                break;
            case OptionsPhotoType.Credits:
                _manager.ChangeCurrentState(_manager.CreditState);
                break;
        }
    }
}
