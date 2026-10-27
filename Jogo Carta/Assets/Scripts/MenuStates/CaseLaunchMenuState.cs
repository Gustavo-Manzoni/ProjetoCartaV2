/// <summary>
/// Estado usado enquanto a foto do caso abre a cena de gameplay (lampada piscando, fade):
/// ignora Voltar, Esc e as outras fotos para a transicao nao ser interrompida no meio.
/// </summary>
public class CaseLaunchMenuState : IMenuState
{
    public bool CanInteractWithPhotos() => false;
    public void Enter() { }
    public void Back() { }
    public void Exit() { }
    public void Update() { }
}
