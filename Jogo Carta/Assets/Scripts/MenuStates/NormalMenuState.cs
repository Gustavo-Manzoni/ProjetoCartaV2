using UnityEngine;

public class NormalMenuState : IMenuState
{
    MainMenuManager _manager;
    public NormalMenuState(MainMenuManager manager)
    {
        _manager = manager;
        
    }
    public void Back()
    {
       
    }

    public bool CanInteractWithPhotos()
    {
        return false;
    }

    public void Enter()
    {
       _manager.GoToNormalCamera();
    }

    public void Exit()
    {
     
    }

    public void Update()
    {
      
    }
}
