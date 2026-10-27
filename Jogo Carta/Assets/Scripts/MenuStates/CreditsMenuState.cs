using UnityEngine;

public class CreditsMenuState : IMenuState
{
    MainMenuManager _manager;
    public CreditsMenuState(MainMenuManager manager)
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
        _manager.GoToCreditsCamera();
    }

    public void Exit()
    {
      
    }

   public  void Update()
    {
      
    }
}
