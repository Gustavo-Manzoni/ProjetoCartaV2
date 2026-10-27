using UnityEngine;
using UnityEngine.InputSystem;

public class PhotoMenuState : IMenuState
{
    MainMenuManager _manager;
    public PhotoMenuState(MainMenuManager manager)
    {
        _manager = manager;
        
    }
    public void Back()
    {
       _manager.MoveBackButtonBack();
       _manager.LastCamera?.Prioritize();
    }

    public bool CanInteractWithPhotos()
    {
        return false;
    }

    public void Enter()
    {
       
    }

    public void Exit()
    {
      
    }

    public void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            _manager.OnOptions();
        }
    }
}
