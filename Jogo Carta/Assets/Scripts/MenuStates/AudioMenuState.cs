using UnityEngine;

public class AudioMenuState : IMenuState
{
    MainMenuManager _manager;
    public AudioMenuState(MainMenuManager manager)
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
        _manager.GoToAudioCamera();
    }
    public void Exit()
    {
       
    }
   
    public void Update()
    {
       
    }

}
