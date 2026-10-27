using UnityEngine;

public interface IMenuState 
{
    bool CanInteractWithPhotos();
    void Enter();
    void Back();
    void Exit();
    void Update();

}
