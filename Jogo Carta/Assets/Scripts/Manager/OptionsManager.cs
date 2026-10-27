using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    [SerializeField] Toggle showFps;
    [SerializeField] Toggle vSync;
    [SerializeField] TMP_Dropdown screenMode;
    [SerializeField] Slider master, music, sfx;
    
    void Start()    
    {
        showFps.onValueChanged.AddListener(delegate { SettingsManager.Instance.ShowFPS(showFps.isOn); }) ;
        vSync.onValueChanged.AddListener(delegate { SettingsManager.Instance.VSync(vSync.isOn); });
        screenMode.onValueChanged.AddListener(delegate { SettingsManager.Instance.SetScreeMode(screenMode.value); });
        master.onValueChanged.AddListener(delegate { SettingsManager.Instance.SetVolume(master.value, SliderType.Master); });
        music.onValueChanged.AddListener(delegate { SettingsManager.Instance.SetVolume(music.value, SliderType.Music); });
        sfx.onValueChanged.AddListener(delegate { SettingsManager.Instance.SetVolume(sfx.value, SliderType.Sfx); });

        if (PlayerPrefs.HasKey("showFps")){showFps.isOn = PlayerPrefs.GetInt("showFps") == 1 ? true : false; }
        if (PlayerPrefs.HasKey("vSync")) { vSync.isOn = PlayerPrefs.GetInt("vSync") == 1 ? true : false; }
        if (PlayerPrefs.HasKey("screenMode")) { screenMode.value =PlayerPrefs.GetInt("screenMode"); }
        if (PlayerPrefs.HasKey("masterVolume")) { master.value = PlayerPrefs.GetFloat("masterVolume"); }
        if (PlayerPrefs.HasKey("musicVolume")) { music.value = PlayerPrefs.GetFloat("musicVolume"); }
        if (PlayerPrefs.HasKey("sfxVolume")) { sfx.value = PlayerPrefs.GetFloat("sfxVolume"); }
    }
  
 

}
