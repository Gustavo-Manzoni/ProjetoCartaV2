using TMPro;
using ProjetoCarta.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public enum SliderType { Master, Sfx, Music}
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;
    [SerializeField]TMP_Text fpsText;
    [SerializeField] AudioMixer audioMixer;

    public AudioMixer Mixer => audioMixer;
    

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }
        Destroy(gameObject);
    }
    private void Start()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LoadLanguage();
        if (Instance != this)
            return;
        GameSettings.Changed += OnSettingChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshFps();
    }
    private void OnDestroy()
    {
        if (Instance != this)
            return;
        GameSettings.Changed -= OnSettingChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSettingChanged(string key)
    {
        if (key == GameSettings.ShowFpsKey)
            RefreshFps();
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshFps();
    /// <summary>
    /// O contador antigo so aparece no menu (cena 0) e so com a opcao ligada. Na gameplay quem
    /// mostra o FPS e o GameplayHud; sem isso o contador antigo ficava sempre ligado.
    /// </summary>
    private void RefreshFps()
    {
        if (fpsText != null)
            fpsText.gameObject.SetActive(GameSettings.ShowFps && SceneManager.GetActiveScene().buildIndex == 0);
    }
    public void ShowFPS(bool showFps)
    {
        PlayerPrefs.SetInt("showFps", showFps ? 1 : 0);
        RefreshFps();
    }
    public void VSync(bool active)
    {
        QualitySettings.vSyncCount = active ? 1 : 0;
        PlayerPrefs.SetInt("vSync", active ? 1 : 0);
    }
    /// <summary>
    /// Recebe o indice do dropdown "Screen Mode" (OptionsCanvas.prefab), na ordem das opcoes:
    /// 0 = FullScreenWindow, 1 = ExclusiveFullScreen, 2 = MaximizedWindow (3 = Windowed, se a opcao for adicionada).
    /// O indice e o que fica salvo em "screenMode" e o OptionsManager o devolve ao dropdown ao abrir o menu.
    /// Antes os casos comecavam em 1: o indice 0 nao fazia nada e os outros aplicavam o modo da opcao anterior.
    /// </summary>
    public void SetScreeMode(int index)
    {
        if (TryGetScreenMode(index, out FullScreenMode mode))
            Screen.fullScreenMode = mode;
        PlayerPrefs.SetInt(GameSettings.ScreenModeKey, index);
    }
    public static bool TryGetScreenMode(int index, out FullScreenMode mode)
    {
        switch (index)
        {
            case 0:
                mode = FullScreenMode.FullScreenWindow;
                return true;
            case 1:
                mode = FullScreenMode.ExclusiveFullScreen;
                return true;
            case 2:
                mode = FullScreenMode.MaximizedWindow;
                return true;
            case 3:
                mode = FullScreenMode.Windowed;
                return true;
            default:
                mode = Screen.fullScreenMode;
                return false;
        }
    }
    public void SetVolume(float to, SliderType sliderType)
    {
        // Centralizado em GameSettings: salva em PlayerPrefs e nunca gera -Infinity dB (0 = silencio).
        VolumeChannel channel = sliderType switch
        {
            SliderType.Sfx => VolumeChannel.Sfx,
            SliderType.Music => VolumeChannel.Music,
            _ => VolumeChannel.Master
        };
        GameSettings.SetVolume(channel, to);
    }
}
