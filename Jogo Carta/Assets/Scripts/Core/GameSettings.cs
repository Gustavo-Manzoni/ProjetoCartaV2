using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

namespace ProjetoCarta.Core
{
    public enum VolumeChannel
    {
        Master,
        Music,
        Sfx
    }

    /// <summary>
    /// Configuracoes salvas em PlayerPrefs, compartilhadas pelo menu principal e pela pausa.
    /// Volume geral = AudioListener (silencia tudo, inclusive sons sintetizados); musica/efeitos = AudioMixer.
    /// Aplicadas automaticamente quando o jogo abre, em qualquer cena.
    /// </summary>
    public static class GameSettings
    {
        public const string MasterKey = "masterVolume";
        public const string MusicKey = "musicVolume";
        public const string SfxKey = "sfxVolume";
        public const string ShowFpsKey = "showFps";
        public const string VSyncKey = "vSync";
        public const string ScreenModeKey = "screenMode";
        public const string LanguageKey = "language";

        // Opcoes da pausa (o menu principal vai usar as mesmas chaves).
        public const string MouseSensitivityKey = "mouseSensitivity";
        public const string InvertYKey = "invertY";
        public const string HeadBobKey = "headBob";
        public const string FovKey = "fov";
        public const string FpsLimitKey = "fpsLimit";
        public const string QualityKey = "qualityLevel";
        public const string AntiAliasingKey = "antiAliasing";
        public const string BrightnessKey = "brightness";
        public const string FilmGrainKey = "filmGrain";
        public const string ShowHintsKey = "showHints";
        public const string ShowReticleKey = "showReticle";
        public const string ThoughtSizeKey = "thoughtSize";
        public const string MuteUnfocusedKey = "muteUnfocused";

        public const float MinMouseSensitivity = 0.2f;
        public const float MaxMouseSensitivity = 3f;
        public const float MinFov = 50f;
        public const float MaxFov = 95f;

        /// <summary>Limites de FPS oferecidos; 0 = sem limite.</summary>
        public static readonly int[] FpsLimits = { 0, 30, 60, 120, 144, 240 };

        /// <summary>Escala do texto dos pensamentos de Tomas por indice de ThoughtSizeKey.</summary>
        public static readonly float[] ThoughtScales = { 0.8f, 1f, 1.3f };

        /// <summary>Disparado com a chave alterada quando uma opcao muda (HUD, jogador e atmosfera reagem ao vivo).</summary>
        public static event Action<string> Changed;

        private const string LegacyPauseMasterKey = "noir_master";
        private const string MixerResourcePath = "Audio/AudioMixer";
        private const float MinimumLinearVolume = 0.0001f;

        private static AudioMixer mixer;
        private static bool mixerSearched;
        private static float duck = 1f;

        public static AudioMixer Mixer
        {
            get
            {
                if (mixer == null && !mixerSearched)
                {
                    mixerSearched = true;
                    mixer = Resources.Load<AudioMixer>(MixerResourcePath);
                }

                return mixer;
            }
        }

        public static AudioMixerGroup SfxGroup => FindGroup("Sfx");
        public static AudioMixerGroup MusicGroup => FindGroup("Music");

        public static float GetVolume(VolumeChannel channel)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(KeyFor(channel), 1f));
        }

        public static void SetVolume(VolumeChannel channel, float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(KeyFor(channel), linear);
            ApplyVolume(channel, linear);
        }

        /// <summary>Abaixa temporariamente o volume geral (ex.: pausa), sem alterar o valor salvo.</summary>
        public static void SetDuck(float value)
        {
            duck = Mathf.Clamp01(value);
            ApplyVolume(VolumeChannel.Master, GetVolume(VolumeChannel.Master));
        }

        public static void ApplyAll()
        {
            MigrateLegacyKeys();
            ApplyVolume(VolumeChannel.Master, GetVolume(VolumeChannel.Master));
            ApplyVolume(VolumeChannel.Music, GetVolume(VolumeChannel.Music));
            ApplyVolume(VolumeChannel.Sfx, GetVolume(VolumeChannel.Sfx));

            if (PlayerPrefs.HasKey(QualityKey))
                ApplyQuality(QualityLevel);
            if (PlayerPrefs.HasKey(VSyncKey))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) == 1 ? 1 : 0;

            Application.targetFrameRate = FpsLimit > 0 ? FpsLimit : -1;
            ApplyAntiAliasing();
        }

        // ------------------------------------------------------------ opcoes genericas

        public static bool GetBool(string key, bool fallback)
        {
            return PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
        }

        public static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            Changed?.Invoke(key);
        }

        public static int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(key, fallback);
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Changed?.Invoke(key);
        }

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            Changed?.Invoke(key);
        }

        /// <summary>Apaga a opcao salva (volta ao padrao) e avisa quem escuta.</summary>
        public static void ResetKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            Changed?.Invoke(key);
        }

        // ------------------------------------------------------------ opcoes com regra propria

        /// <summary>Multiplicador da sensibilidade do mouse (1 = a sensibilidade original do jogo).</summary>
        public static float MouseSensitivity =>
            Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, 1f), MinMouseSensitivity, MaxMouseSensitivity);

        public static bool InvertY => GetBool(InvertYKey, false);
        public static bool HeadBob => GetBool(HeadBobKey, true);

        /// <summary>Campo de visao salvo, ou 0 quando o jogador nunca mexeu (a camera usa o valor da cena).</summary>
        public static float Fov => PlayerPrefs.HasKey(FovKey) ? Mathf.Clamp(PlayerPrefs.GetFloat(FovKey), MinFov, MaxFov) : 0f;

        public static int FpsLimit => Mathf.Max(0, PlayerPrefs.GetInt(FpsLimitKey, 0));

        public static int QualityLevel =>
            Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);

        /// <summary>0 = desligado, 1 = FXAA, 2 = SMAA.</summary>
        public static int AntiAliasing => Mathf.Clamp(PlayerPrefs.GetInt(AntiAliasingKey, 2), 0, 2);

        /// <summary>Brilho de 0 a 1 (0.5 = o da cena). Vira exposicao somada a de cada comodo.</summary>
        public static float Brightness => Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey, 0.5f));

        /// <summary>Exposicao extra (EV) do brilho escolhido: de -1 a +1.</summary>
        public static float BrightnessExposure => (Brightness - 0.5f) * 2f;

        /// <summary>Multiplicador do grao de filme da camada noir (0 a 1).</summary>
        public static float FilmGrain => Mathf.Clamp01(PlayerPrefs.GetFloat(FilmGrainKey, 1f));

        public static bool ShowHints => GetBool(ShowHintsKey, true);
        public static bool ShowReticle => GetBool(ShowReticleKey, true);
        public static bool ShowFps => GetBool(ShowFpsKey, false);
        public static bool MuteUnfocused => GetBool(MuteUnfocusedKey, false);

        public static float ThoughtScale =>
            ThoughtScales[Mathf.Clamp(PlayerPrefs.GetInt(ThoughtSizeKey, 1), 0, ThoughtScales.Length - 1)];

        public static bool VSync => QualitySettings.vSyncCount > 0;

        public static void SetVSync(bool on)
        {
            PlayerPrefs.SetInt(VSyncKey, on ? 1 : 0);
            QualitySettings.vSyncCount = on ? 1 : 0;
            Changed?.Invoke(VSyncKey);
        }

        public static void SetFpsLimit(int fps)
        {
            fps = Mathf.Max(0, fps);
            PlayerPrefs.SetInt(FpsLimitKey, fps);
            Application.targetFrameRate = fps > 0 ? fps : -1;
            Changed?.Invoke(FpsLimitKey);
        }

        public static void SetQuality(int level)
        {
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            PlayerPrefs.SetInt(QualityKey, level);
            ApplyQuality(level);
            Changed?.Invoke(QualityKey);
        }

        public static void SetAntiAliasing(int mode)
        {
            PlayerPrefs.SetInt(AntiAliasingKey, Mathf.Clamp(mode, 0, 2));
            ApplyAntiAliasing();
            Changed?.Invoke(AntiAliasingKey);
        }

        /// <summary>Anti-aliasing de pos-processamento na camera principal (nao mexe no asset do URP).</summary>
        public static void ApplyAntiAliasing()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.TryGetComponent(out UniversalAdditionalCameraData data))
                return;

            data.antialiasing = AntiAliasing switch
            {
                0 => AntialiasingMode.None,
                1 => AntialiasingMode.FastApproximateAntialiasing,
                _ => AntialiasingMode.SubpixelMorphologicalAntiAliasing
            };
        }

        /// <summary>Modo de tela pelo mesmo indice salvo em "screenMode" pelo menu (ver SettingsManager.TryGetScreenMode).</summary>
        public static void SetScreenMode(int index, FullScreenMode mode)
        {
            PlayerPrefs.SetInt(ScreenModeKey, index);
            Screen.fullScreenMode = mode;
            Changed?.Invoke(ScreenModeKey);
        }

        public static void SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, Screen.fullScreenMode);
            Changed?.Invoke("resolution");
        }

        private static void ApplyQuality(int level)
        {
            // Trocar de nivel reaplica o vSync do nivel; o escolhido pelo jogador vale mais.
            QualitySettings.SetQualityLevel(level, true);
            if (PlayerPrefs.HasKey(VSyncKey))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) == 1 ? 1 : 0;
        }

        private static void OnFocusChanged(bool focused)
        {
            AudioListener.pause = !focused && MuteUnfocused;
        }

        /// <summary>Converte 0..1 em decibeis sem nunca gerar -Infinity (0 vira -80 dB).</summary>
        public static float LinearToDecibels(float linear)
        {
            return Mathf.Log10(Mathf.Max(MinimumLinearVolume, Mathf.Clamp01(linear))) * 20f;
        }

        private static void ApplyVolume(VolumeChannel channel, float linear)
        {
            switch (channel)
            {
                case VolumeChannel.Master:
                    // Volume zero silencia de verdade, inclusive fontes fora do mixer.
                    AudioListener.volume = linear * duck;
                    Mixer?.SetFloat("MasterVolume", 0f);
                    break;
                case VolumeChannel.Music:
                    Mixer?.SetFloat("MusicVolume", LinearToDecibels(linear));
                    break;
                case VolumeChannel.Sfx:
                    Mixer?.SetFloat("SfxVolume", LinearToDecibels(linear));
                    break;
            }
        }

        private static string KeyFor(VolumeChannel channel)
        {
            return channel switch
            {
                VolumeChannel.Music => MusicKey,
                VolumeChannel.Sfx => SfxKey,
                _ => MasterKey
            };
        }

        private static void MigrateLegacyKeys()
        {
            // A pausa antiga salvava o volume em outra chave.
            if (!PlayerPrefs.HasKey(MasterKey) && PlayerPrefs.HasKey(LegacyPauseMasterKey))
                PlayerPrefs.SetFloat(MasterKey, PlayerPrefs.GetFloat(LegacyPauseMasterKey, 1f));

            PlayerPrefs.DeleteKey(LegacyPauseMasterKey);
        }

        private static AudioMixerGroup FindGroup(string groupName)
        {
            if (Mixer == null)
                return null;

            AudioMixerGroup[] groups = Mixer.FindMatchingGroups(groupName);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            mixer = null;
            mixerSearched = false;
            duck = 1f;
            Changed = null;
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStartup()
        {
            ApplyAll();
        }
    }
}
