using UnityEngine;

public class BoardMenuState : IMenuState
{
    MainMenuManager _manager;
    public BoardMenuState(MainMenuManager manager)
    {
        _manager = manager;
        
    }
    public void Back()
    {
        _manager.ChangeCurrentState(_manager.NormalState);
        _manager.MoveButtonsBackWithDelay();
        
    }

    public bool CanInteractWithPhotos()
    {
       return true;
    }

    public void Enter()
    {
       _manager.GoToBoardCamera();  
    }

    public void Exit()
    {
     
    }

    public void Update()
    {
       
    }
}
