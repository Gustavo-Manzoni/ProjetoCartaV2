using ProjetoCarta.Blink;
using UnityEngine;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Fundo sonoro de cada momento, em loops gravados de Resources/Audio/Noir/Ambience:
    /// P1 escritorio (chuva ouvida de dentro + tique de relogio), P2 camara (tom de sala com gotas e o zumbido
    /// eletrico da cadeira), P3 sala administrativa (lampada fluorescente + ventilacao). No menu toca
    /// Amb_Menu (chuva na vidraca, trovao distante e chiado de vitrola).
    /// Faz crossfade quando o momento muda. Sai pelo grupo "Music" do mixer.
    /// </summary>
    public sealed class NoirAmbience : MonoBehaviour
    {
        private const string ResourcePath = "Audio/Noir/Ambience/";

        private static NoirAmbience instance;

        private BlinkSceneController blink;
        private AudioSource[] beds;
        private AudioSource chairHum;
        private float[] targets;
        private float chairTarget = 1f;
        private float masterFade = 1f;
        private float masterFadeTarget = 1f;
        private float masterFadeSpeed = 1f;
        private int lastMoment = -1;

        // Volumes por momento (somados ao zumbido da cadeira no P2).
        private static readonly float[] BedVolumes = { 0.22f, 0.12f, 0.2f };
        private const float ChairHumVolume = 0.18f;

        public static void SetChairPowered(bool powered)
        {
            NoirDread.SetChairPowered(powered);
            if (instance != null)
                instance.chairTarget = powered ? 1f : 0f;
        }

        /// <summary>Abaixa todo o fundo (ex.: final) em <paramref name="seconds"/>.</summary>
        public static void FadeAll(float target, float seconds)
        {
            // A camada de suspense visual acompanha (no final ela se desliga de vez).
            NoirDread.FadeAll(target < 1f ? 0f : 1f, seconds);
            if (instance == null)
                return;

            instance.masterFadeTarget = Mathf.Clamp01(target);
            instance.masterFadeSpeed = 1f / Mathf.Max(0.05f, seconds);
        }

        private void Awake()
        {
            instance = this;
            blink = FindFirstObjectByType<BlinkSceneController>(FindObjectsInactive.Include);

            beds = new AudioSource[3];
            targets = new float[3];
            // Amb_Escritorio tem um tique de relogio gravado que soava do nada; o escritorio usa a chuva do menu.
            beds[0] = CreateLoop("Amb_Menu");
            beds[1] = CreateLoop("Amb_Camara");
            beds[2] = CreateLoop("Amb_Administrativo");
            chairHum = CreateLoop("Amb_Cadeira");
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            int moment = blink != null ? blink.CurrentMoment : 0;
            if (moment != lastMoment)
            {
                lastMoment = moment;
                for (int i = 0; i < targets.Length; i++)
                    targets[i] = i == moment ? BedVolumes[i] : 0f;
            }

            float dt = Time.unscaledDeltaTime;
            masterFade = Mathf.MoveTowards(masterFade, masterFadeTarget, masterFadeSpeed * dt);

            for (int i = 0; i < beds.Length; i++)
                beds[i].volume = Mathf.MoveTowards(beds[i].volume, targets[i] * masterFade, dt * 0.6f);

            float hum = moment == 1 ? ChairHumVolume * chairTarget : 0f;
            chairHum.volume = Mathf.MoveTowards(chairHum.volume, hum * masterFade, dt * (chairTarget > 0f ? 0.5f : 0.12f));
        }

        private AudioSource CreateLoop(string clipName)
        {
            AudioClip clip = Resources.Load<AudioClip>(ResourcePath + clipName);
            if (clip == null)
                Debug.LogWarning($"[NoirAmbience] Ambiente sem arquivo em Resources/{ResourcePath}{clipName}.");

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = ProjetoCarta.Core.GameSettings.MusicGroup;
            if (clip != null)
            {
                // Cada loop comeca num ponto diferente para os momentos nao soarem sincronizados.
                source.time = Random.Range(0f, clip.length * 0.9f);
                source.Play();
            }

            return source;
        }
    }
}
