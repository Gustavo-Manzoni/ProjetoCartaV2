using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Sons de UI e de gameplay. Novos valores devem ser adicionados sempre no FIM (o enum e serializado).
    /// </summary>
    public enum NoirSfx
    {
        Hover, Click, Back, Open, Close, Toast, Blink, Tick, Title,
        None,
        Pickup, Place, Locked, Unlock,
        WoodOpen, WoodClose, Drawer, MetalOpen, MetalClose,
        KeypadPress, KeypadFull, KeypadError, KeypadSuccess, SafeOpen,
        BucketHook, Crank, Splash,
        Paper, PaperTear, Coin, Reveal,
        Lever, PowerDown, DoorUnlatch, DoorCreak, Heartbeat,
        Thought, LampFlicker, LampBurst,
        FootWood, FootConcrete,
        Equip, Rummage, TabSwitch
    }

    /// <summary>
    /// Sons de UI e de gameplay, carregados de Resources/Audio/Noir/Sfx (arquivo com o nome do enum).
    /// Variacoes opcionais seguem o padrao Nome_1, Nome_2...: cada Play sorteia uma, sem repetir a anterior.
    /// Toca mesmo com o jogo pausado. Sai pelo grupo "Sfx" do AudioMixer, entao respeita os volumes do menu e da pausa.
    /// Origem e licenca de cada arquivo: Docs/AUDIO_CREDITOS.md.
    /// </summary>
    public sealed class NoirAudio : MonoBehaviour
    {
        private const string ResourcePath = "Audio/Noir/Sfx/";
        private const int MaxVariants = 8;

        private static NoirAudio instance;
        private AudioSource[] voices;
        private int nextVoice;
        private readonly AudioClip[][] clips = new AudioClip[Enum.GetValues(typeof(NoirSfx)).Length][];
        private readonly int[] lastVariant = new int[Enum.GetValues(typeof(NoirSfx)).Length];

        public static NoirAudio Get()
        {
            if (instance == null)
            {
                GameObject go = new("NoirAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<NoirAudio>();
                instance.Build();
            }

            return instance;
        }

        public static void Play(NoirSfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (sfx == NoirSfx.None)
                return;

            NoirAudio a = Get();
            AudioClip clip = a.PickVariant(sfx);
            if (clip == null)
                return;

            AudioSource voice = a.voices[a.nextVoice];
            a.nextVoice = (a.nextVoice + 1) % a.voices.Length;
            voice.pitch = pitch * UnityEngine.Random.Range(0.97f, 1.03f);
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.Play();
        }

        private AudioClip PickVariant(NoirSfx sfx)
        {
            AudioClip[] variants = clips[(int)sfx];
            if (variants == null || variants.Length == 0)
                return null;

            if (variants.Length == 1)
                return variants[0];

            // Sorteia entre as outras variacoes para o mesmo som nao sair duas vezes seguidas.
            int index = UnityEngine.Random.Range(0, variants.Length - 1);
            if (index >= lastVariant[(int)sfx])
                index++;

            lastVariant[(int)sfx] = index;
            return variants[index];
        }

        private void Build()
        {
            voices = new AudioSource[12];
            UnityEngine.Audio.AudioMixerGroup group = ProjetoCarta.Core.GameSettings.SfxGroup;
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource v = gameObject.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.spatialBlend = 0f;
                v.ignoreListenerPause = true;
                v.outputAudioMixerGroup = group;
                voices[i] = v;
            }

            foreach (NoirSfx sfx in Enum.GetValues(typeof(NoirSfx)))
            {
                if (sfx != NoirSfx.None)
                    clips[(int)sfx] = LoadVariants(sfx.ToString());
            }
        }

        private static AudioClip[] LoadVariants(string name)
        {
            List<AudioClip> found = new();
            AudioClip first = Resources.Load<AudioClip>(ResourcePath + name);
            if (first != null)
                found.Add(first);

            for (int i = 1; i <= MaxVariants; i++)
            {
                AudioClip variant = Resources.Load<AudioClip>(ResourcePath + name + "_" + i);
                if (variant == null)
                    break;

                found.Add(variant);
            }

            if (found.Count == 0)
                Debug.LogWarning($"[NoirAudio] Som sem arquivo em Resources/{ResourcePath}{name}.");

            return found.ToArray();
        }
    }
}
