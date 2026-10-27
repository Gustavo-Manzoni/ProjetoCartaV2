using System;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
public enum Language 
{
    PT, EN
}
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;
    public static event Action<Language> OnChangeLanguage;
    /// <summary>Idioma em uso (PT por padrao). Textos ativados depois da troca leem este valor.</summary>
    public static Language CurrentLanguage { get; private set; } = Language.PT;

    [SerializedDictionary]
    [SerializeField]SerializedDictionary<Language, SerializedDictionary<TextTypes, string>> translation ;

    public void LoadLanguage()
    {
        if (PlayerPrefs.HasKey("language"))
        {
            ChangeLanguage((Language)PlayerPrefs.GetInt("language"));
        }
        else
        {
            OnChangeLanguage?.Invoke(CurrentLanguage);
        }
    }
    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
            if (PlayerPrefs.HasKey("language"))
                CurrentLanguage = (Language)PlayerPrefs.GetInt("language");
            DontDestroyOnLoad(gameObject);
            return;
        }
        Destroy(gameObject);
    }
     void ChangeLanguage(Language to)
    {
        CurrentLanguage = to;
        OnChangeLanguage?.Invoke(to);
        PlayerPrefs.SetInt("language", (int)to);
    
    }
    public void ChangeLanguageToEn() 
    {
        ChangeLanguage(Language.EN);  
    }
    public void ChangeLanguageToPt()
    {
        ChangeLanguage(Language.PT);
    }
    public string GetText(Language language, TextTypes textType) 
    {
        foreach(var d in translation) 
        {
            if(d.Key == language) 
            {
                foreach (var t in d.Value)
                {
                    if(t.Key == textType) 
                    {
                        return t.Value;
                    
                    }

                }
            
            }
        
        }
        return "";
    }

}
