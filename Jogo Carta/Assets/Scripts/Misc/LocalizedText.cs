using TMPro;
using UnityEngine;
public enum TextTypes
{
    Jogar, Opcoes, Sair, Voltar, Fps


}
public class LocalizedText : MonoBehaviour
{
    [SerializeField] TextTypes textType;
    [SerializeField]TMP_Text myText;
    private void Awake()
    {
        myText = GetComponent<TMP_Text>();
    }
    private void OnEnable()
    {
        LocalizationManager.OnChangeLanguage += UpdateText;
        // Aplica o idioma atual tambem quando a cena e recarregada (o evento so dispara na troca).
        UpdateText(LocalizationManager.CurrentLanguage);
    }
    private void OnDisable()
    {
        LocalizationManager.OnChangeLanguage -= UpdateText;
    }
    void UpdateText(Language to)
    {
        if (myText == null || LocalizationManager.Instance == null) return;
        string text = LocalizationManager.Instance.GetText(to, textType);
        if (!string.IsNullOrEmpty(text))
            myText.text = text;
    }
}
