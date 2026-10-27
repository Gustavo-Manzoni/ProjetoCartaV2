using UnityEngine;
public class GraphicsMenuState : IMenuState
{
    MainMenuManager _manager;
    public GraphicsMenuState(MainMenuManager manager)
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
        _manager.GoToGraphicsCamera();
    }

    public void Exit()
    {
      
    }

   
    public void Update()
    {
       
    }
}
