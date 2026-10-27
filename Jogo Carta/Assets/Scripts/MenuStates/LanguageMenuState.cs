using UnityEngine;

public class LanguageMenuState : IMenuState
{
    MainMenuManager _manager;
    public LanguageMenuState(MainMenuManager manager)
    {
       _manager = manager;
    }

    public void Back()
    {
        _manager.ChangeCurrentState(_manager.OptionsState);
    }

    public bool CanInteractWithPhotos()
    {
        return false;
    }

    public void Enter()
    {
        _manager.GoToLanguageCamera();
    }

    public void Exit()
    {
      
    }

    public void Update()
    {
     
    }
}
