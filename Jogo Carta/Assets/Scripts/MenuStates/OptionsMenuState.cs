using UnityEngine;

public class OptionsMenuState : IMenuState
{
    MainMenuManager _manager;
    public OptionsMenuState(MainMenuManager manager)
    {
        _manager = manager;
        
    }
    public void Back()
    {
        //if (_manager.LastState.Equals(_manager.NormalState))
        //{
            _manager.MoveButtonsBackWithDelay();
             _manager.ChangeCurrentState(_manager.NormalState);
        //}
        

        // if (_manager.LastState.Equals(_manager.PhotoState))
        // {          
        //     _manager.MoveBackButtonBack();
        //      _manager.ChangeCurrentState(_manager.PhotoState);
        // }
        

    }

    public bool CanInteractWithPhotos()
    {
        return true;
    }

    public void Enter()
    {
        // Vai direto ao quadro com a ficha de opcoes (MenuOptionsBoards); a parede de fotos saiu do fluxo.
        _manager.GoToOptionsBoardCamera();
    }

    public void Exit()
    {
     
    }

    public void Update()
    {
      
    }
}
